// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

package cmd

import (
	"errors"
	"fmt"
	"path/filepath"
	"slices"
	"sort"
	"strings"

	"golang.org/x/mod/module"
)

var errExternalModule = errors.New("reviewed module exports a type defined in a different repository")

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

func (r *Review) Review() (PackageReview, error) {
	if err := r.resolveAliases(); err != nil {
		return PackageReview{}, err
	}

	tokenList := &[]Token{}
	nav := []Navigation{}
	diagnostics := []Diagnostic{}
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
	for _, name := range packageNames {
		p := r.reviewed.Packages[name]
		n := p.relName
		makeToken(nil, nil, "package", TokenTypeMemberName, tokenList)
		makeToken(nil, nil, " ", TokenTypeWhitespace, tokenList)
		makeToken(&n, nil, n, TokenTypeTypeName, tokenList)
		makeToken(nil, nil, "", TokenTypeNewline, tokenList)
		makeToken(nil, nil, "", TokenTypeNewline, tokenList)
		// TODO: reordering these calls reorders APIView output and can omit content
		p.c.parseInterface(tokenList)
		p.c.parseStruct(tokenList)
		p.c.parseSimpleType(tokenList)
		p.c.parseVar(tokenList)
		p.c.parseConst(tokenList)
		p.c.parseFunc(tokenList)
		navItems := p.c.generateNavChildItems()
		nav = append(nav, Navigation{
			Text:         n,
			NavigationId: n,
			ChildItems:   navItems,
			Tags: &map[string]string{
				"TypeKind": "namespace",
			},
		})
		diagnostics = append(diagnostics, p.diagnostics...)
		slices.SortFunc(diagnostics, func(a Diagnostic, b Diagnostic) int {
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
	}
	return PackageReview{
		Diagnostics: diagnostics,
		Language:    "Go",
		Name:        r.reviewed.Name,
		Navigation:  nav,
		Tokens:      *tokenList,
		PackageName: r.name,
	}, nil
}

// findLocalModule tries to find the source module defining a type in the same repository as
// the reviewed module. Returns errExternalModule if the source module is in a different repository.
func (r *Review) findLocalModule(ta TypeAlias) (*Module, error) {
	// localModulePath could be inlined but is instead separate for easier testing
	if dir := localModulePath(ta.SourceMod, r.path); dir != "" {
		return NewModule(dir)
	}
	return nil, errExternalModule
}

// localModulePath tries to find a file path for the given module.Version assuming that
// module is in the same repository as dir. If it is, the two paths must have a common
// segment implying a disk location for the module. For example:
//
//   - mod.Path "github.com/Azure/azure-sdk-for-go/sdk/internal"
//   - dir "/home/me/azsdk/sdk/azcore"
//   - return "/home/me/azsdk/sdk/internal"
//
// It returns an empty string when no such segment exists. Also, it ignores the module
// version, using only the module path.
func localModulePath(mod module.Version, dir string) string {
	mp := strings.Split(mod.Path, "/")
	// find the rightmost common segment of mod.Path and dir, ignoring the final
	// segment of mod.Path because it's a package name that may be used in both modules
	i, j := -1, -1
	for m := len(mp) - 2; m > 0; m-- {
		if n := strings.LastIndex(dir, mp[m]); n > i {
			i = n
			j = m
		}
	}
	if i == -1 || j == -1 {
		return ""
	}
	root := dir[:i]
	modDir := filepath.Join(mp[j:]...)
	return filepath.Join(root, modDir)
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
			m, err = r.findLocalModule(*ta)
			if errors.Is(err, errExternalModule) {
				m, err = GetExternalModule(ta.SourceMod)
			}
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
			if d, ok := recursiveFindTypeDef(sourceName, p, m.Packages); ok {
				def = d
			}
		}
		err = ta.Resolve(def)
		if err != nil {
			return (err)
		}
	}
	return nil
}
