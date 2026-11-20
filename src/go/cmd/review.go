// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

package cmd

import (
	"fmt"
	"go/ast"
	"path/filepath"
	"slices"
	"sort"
	"strings"

	"golang.org/x/mod/module"
)

// Review represents an apiview review of an Azure SDK for Go module
type Review struct {
	// modules maps module paths to Modules implicated in this API review. It
	// contains only the reviewed module in most cases, however it contains
	// more when the reviewed module exports types defined in another module.
	modules map[string]*Module
	// name of the APIView review e.g. "sdk/azcore"
	name string
	// path on disk to the reviewed module
	path string
	// reviewed is the module being reviewed
	reviewed *Module
}

// NewReview creates a Review for the module at path p
func NewReview(p string) (*Review, error) {
	m, err := NewModule(p)
	if err != nil {
		return nil, err
	}
	r := &Review{
		modules: map[string]*Module{},
		name:    getPackageNameFromModPath(m.ModFile.Module.Mod.Path),
		path:    p,
	}
	err = r.AddModule(m)
	return r, err
}

// AddModule adds a module to the review. Call this to add a module that exports
// a type the reviewed module exports by alias.
func (r *Review) AddModule(m *Module) error {
	if _, ok := r.modules[m.ModFile.Module.Mod.Path]; ok {
		return fmt.Errorf("module %s already exists in index", m.ModFile.Module.Mod.Path)
	}
	if len(r.modules) == 0 {
		r.reviewed = m
	}
	r.modules[m.ModFile.Module.Mod.Path] = m
	return nil
}

func (r *Review) Review() (CodeFile, error) {
	if err := r.resolveAliases(); err != nil {
		return CodeFile{}, err
	}

	lines := []ReviewLine{}
	nav := []NavigationItem{}
	diagnostics := []CodeDiagnostic{}
	packageNames := []string{}
	for name, p := range r.reviewed.Packages {
		// we use a prefixed path separator so that we can handle the "internal" module.
		//  internal/dig
		//  internal/errorinfo
		//  etc.
		// for other modules, we skip /internal subdirectories
		//  azcore/internal/...
		if strings.Contains(p.relName, "/internal") || p.c.isEmpty() {
			continue
		}
		packageNames = append(packageNames, name)
	}
	sort.Strings(packageNames)
	for i, name := range packageNames {
		p := r.reviewed.Packages[name]
		n := p.relName
		line := ReviewLine{
			Children: []ReviewLine{},
			LineID:   n,
			Tokens: []ReviewToken{
				{
					HasSuffixSpace: true,
					Kind:           TokenKindKeyword,
					Value:          "package",
				},
				{
					Kind:                  TokenKindText,
					NavigationDisplayName: n,
					Value:                 n,
				},
			},
		}
		// TODO: reordering these calls reorders APIView output and can omit content
		line.Children = append(line.Children, p.c.parseInterface()...)
		line.Children = append(line.Children, p.c.parseStructs()...)
		line.Children = append(line.Children, p.c.parseSimpleType()...)
		line.Children = append(line.Children, p.c.parseVar()...)
		line.Children = append(line.Children, p.c.parseConst()...)
		line.Children = append(line.Children, p.c.parseFunc()...)
		navItems := p.c.generateNavChildItems()
		nav = append(nav, NavigationItem{
			Text:         n,
			NavigationID: n,
			ChildItems:   navItems,
			Tags: &map[string]string{
				"TypeKind": "namespace",
			},
		})
		diagnostics = append(diagnostics, p.diagnostics...)
		slices.SortFunc(diagnostics, func(a CodeDiagnostic, b CodeDiagnostic) int {
			targetCmp := strings.Compare(a.TargetID, b.TargetID)
			if targetCmp != 0 {
				return targetCmp
			}
			// if the target IDs are the same then fall back to the text.
			// this accounts for cases where there are multiple diagnostics
			// for the same target ID.
			return strings.Compare(a.Text, b.Text)
		})
		for _, n := range nav {
			recursiveSortNavigation(n)
		}
		lines = append(lines, line)
		var tks []ReviewToken
		if i < len(packageNames)-1 {
			tks = append(tks, ReviewToken{
				Kind:     TokenKindText,
				SkipDiff: true,
				Value:    strings.Repeat("━", 160),
			})
		}
		lines = append(lines, ReviewLine{IsContextEndLine: true, Tokens: tks})
	}

	// Any ReviewToken having a nonempty NavigateToID that doesn't match some ReviewLine's
	// LineID will be clickable in API View but won't navigate to anything when clicked. It
	// would be best simply not to assign such values, but parseAndMakeTypeTokens() does so
	// in some uncommon cases and preventing that is difficult in the current implementation.
	// So, we instead remove invalid NavigateToID values here.
	lineIDs := map[string]bool{}
	forAll(lines, func(ln ReviewLine) {
		lineIDs[ln.LineID] = true
	})
	forAll(lines, func(ln ReviewLine) {
		for i, tk := range ln.Tokens {
			if !lineIDs[tk.NavigateToID] {
				ln.Tokens[i].NavigateToID = ""
			}
		}
	})

	return CodeFile{
		Diagnostics: diagnostics,
		Language:    "Go",
		Name:        r.reviewed.Name,
		Navigation:  nav,
		// this must match the value in src/dotnet/APIView/APIViewWeb/Languages/GoLanguageService.cs
		ParserVersion:  "0.1",
		ReviewLines:    lines,
		PackageName:    r.name,
		PackageVersion: r.reviewed.Version,
	}, nil
}

// resolveAliases resolves type aliases in the reviewed module that refer to types in other modules
func (r *Review) resolveAliases() error {
	for _, ta := range r.reviewed.ExternalAliases {
		var (
			err error
			m   *Module
			ok  bool
		)
		if m, ok = r.modules[ta.SourceMod.Path]; !ok {
			m, err = r.externalModule(ta.SourceMod)
			if err == nil {
				err = r.AddModule(m)
			}
			if err != nil {
				return err
			}
		}
		def := typeDef{}
		if m != nil {
			dot := strings.LastIndex(ta.QualifiedName, ".")
			if len(ta.QualifiedName)-2 < dot || dot < 1 {
				// there must be at least one rune before and after the dot
				panic(fmt.Sprintf("alias %q refers to an invalid qualified name %q", ta.Name, ta.QualifiedName))
			}
			impPath := ta.QualifiedName[:dot]
			p, ok := m.Packages[impPath]
			if !ok {
				return fmt.Errorf("couldn't find definition for " + ta.Name)
			}
			sourceName := ta.QualifiedName[dot+1:]
			d, found, err := recursiveFindTypeDef(sourceName, p, m.Packages)
			if err != nil {
				return fmt.Errorf("resolving alias %s.%s: %w", ta.Package.Name(), ta.Name, err)
			}
			if found {
				def = d
			}
		}
		err = ta.Resolve(def)
		if err != nil {
			return (err)
		}
	}
	return r.resolveLocalTypes()
}

func (r *Review) externalModule(mod module.Version) (*Module, error) {
	for _, replacement := range r.reviewed.ModFile.Replace {
		if replacement.Old.Path != mod.Path || (replacement.Old.Version != "" && replacement.Old.Version != mod.Version) {
			continue
		}
		if replacement.New.Version != "" {
			return GetExternalModule(replacement.New)
		}
		dir := filepath.FromSlash(replacement.New.Path)
		if !filepath.IsAbs(dir) {
			dir = filepath.Join(r.path, dir)
		}
		return NewModule(dir)
	}
	return GetExternalModule(mod)
}

func (r *Review) resolveLocalTypes() error {
	for _, p := range r.reviewed.Packages {
		if strings.Contains(p.relName, "/internal") {
			continue
		}
		names := []string{}
		for name, def := range p.types {
			if def.p == p && def.n.Name.Name == name && ast.IsExported(name) {
				switch typ := unparenType(def.n.Type).(type) {
				case *ast.Ident:
					if def.n.Assign.IsValid() || !ast.IsExported(typ.Name) {
						names = append(names, name)
					}
				case *ast.StarExpr:
					if def.n.Assign.IsValid() {
						// Pointer aliases need different method-set handling.
						return fmt.Errorf("unsupported type %T for alias %s.%s", typ, p.Name(), name)
					}
				}
			}
		}
		sort.Strings(names)
		for _, name := range names {
			original := p.types[name]
			ident := unparenType(original.n.Type).(*ast.Ident)
			def, found, err := recursiveFindTypeDef(name, p, r.reviewed.Packages)
			if err != nil {
				return fmt.Errorf("resolving type %s.%s: %w", p.Name(), name, err)
			}
			if !found || (def.p == p && def.n == original.n) {
				continue
			}
			alias := TypeAlias{
				Name:          name,
				Package:       p,
				QualifiedName: p.unversionedModulePath() + "." + ident.Name,
				isAlias:       original.n.Assign.IsValid(),
			}
			if !ast.IsExported(ident.Name) {
				if err := alias.Resolve(def); err != nil {
					return fmt.Errorf("resolving alias %s.%s: %w", p.Name(), name, err)
				}
			} else if alias.isAlias {
				// The public target already has a review line; only copy its method set.
				alias.hoistMethods(def)
			}
		}
	}
	return nil
}

// forAll recursively applies a function to all lines and their children
func forAll(lines []ReviewLine, fn func(ReviewLine)) {
	for _, ln := range lines {
		fn(ln)
		forAll(ln.Children, fn)
	}
}
