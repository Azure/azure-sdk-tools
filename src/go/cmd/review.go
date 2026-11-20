// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

package cmd

import (
	"bytes"
	"encoding/json"
	"fmt"
	"go/ast"
	"path"
	"path/filepath"
	"slices"
	"sort"
	"strings"

	"golang.org/x/mod/modfile"
	"golang.org/x/mod/module"
)

// Review represents an apiview review of an Azure SDK for Go module
type Review struct {
	// modules maps module paths to Modules implicated in this API review. It
	// contains only the reviewed module in most cases, however it contains
	// more when the reviewed module exports types defined in another module.
	modules         map[string]*Module
	externalModules map[string]*Module
	selectedModules map[string]module.Version
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
		modules:         map[string]*Module{},
		externalModules: map[string]*Module{},
		selectedModules: map[string]module.Version{},
		name:            getPackageNameFromModPath(m.ModFile.Module.Mod.Path),
		path:            p,
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
		def, terminal, err := r.resolveExternalType(ta.SourceMod, ta.QualifiedName, map[string]bool{})
		if err != nil {
			return fmt.Errorf("resolving alias %s.%s: %w", ta.Package.Name(), ta.Name, err)
		}
		if terminal != "" {
			ta.QualifiedName = terminal
		}
		err = ta.Resolve(def)
		if err != nil {
			return (err)
		}
	}
	return r.resolveLocalTypes()
}

func (r *Review) resolveExternalType(mod module.Version, qualifiedName string, seen map[string]bool) (typeDef, string, error) {
	var err error
	mod, err = r.selectedModuleVersion(mod)
	if err != nil {
		return typeDef{}, "", err
	}
	key := mod.Path + "@" + mod.Version + ":" + qualifiedName
	if seen[key] {
		return typeDef{}, "", fmt.Errorf("cyclic external type alias %s", qualifiedName)
	}
	seen[key] = true
	defer delete(seen, key)
	if mod.Path == r.reviewed.ModFile.Module.Mod.Path {
		return r.resolveReviewedType(qualifiedName, seen)
	}
	m, err := r.externalModule(mod)
	if err != nil {
		return typeDef{}, "", err
	}
	importPath, typeName, ok := strings.Cut(qualifiedName, ".")
	if !ok {
		return typeDef{}, "", fmt.Errorf("invalid qualified type %q", qualifiedName)
	}
	// Import paths contain dots; split at the last dot separating package and type.
	dot := strings.LastIndex(qualifiedName, ".")
	importPath, typeName = qualifiedName[:dot], qualifiedName[dot+1:]
	importPath = r.replacementImportPath(mod, m.ModFile.Module.Mod.Path, importPath)
	modulePath := m.ModFile.Module.Mod.Path
	if versionReg.MatchString(modulePath) {
		importPath = strings.ReplaceAll(importPath, modulePath, path.Dir(modulePath))
	}
	p, ok := m.Packages[importPath]
	if !ok {
		return typeDef{}, "", fmt.Errorf("couldn't find package %s for %s", importPath, qualifiedName)
	}
	def, found, err := findTypeDef(typeName, p, m.Packages, map[typeDefRef]bool{}, true)
	if err != nil {
		return typeDef{}, "", err
	}
	if found {
		return r.resolveTypeDefinition(m, def, seen)
	}
	for _, alias := range p.TypeAliases {
		if alias.Name != typeName {
			continue
		}
		if !alias.isAlias {
			def := alias.definition
			def.methodPackage = p
			def.methodName = alias.Name
			return def, "", nil
		}
		if alias.SourceMod == (module.Version{}) {
			aliasDot := strings.LastIndex(alias.QualifiedName, ".")
			if aliasDot < 1 {
				return typeDef{}, "", fmt.Errorf("invalid qualified type %q", alias.QualifiedName)
			}
			aliasImportPath := alias.QualifiedName[:aliasDot]
			modulePath := m.ModFile.Module.Mod.Path
			if versionReg.MatchString(modulePath) {
				modulePath = path.Dir(modulePath)
			}
			if aliasImportPath == modulePath || strings.HasPrefix(aliasImportPath, modulePath+"/") {
				return r.resolveModuleType(m, alias.QualifiedName, seen)
			}
			if isStandardLibraryImportPath(aliasImportPath) {
				return typeDef{}, alias.QualifiedName, nil
			}
			return typeDef{}, "", fmt.Errorf("couldn't resolve dependency for alias %s", alias.QualifiedName)
		}
		return r.resolveExternalType(alias.SourceMod, alias.QualifiedName, seen)
	}
	return typeDef{}, "", fmt.Errorf("couldn't resolve type %s", qualifiedName)
}

func (r *Review) resolveReviewedType(qualifiedName string, seen map[string]bool) (typeDef, string, error) {
	return r.resolveModuleType(r.reviewed, qualifiedName, seen)
}

func (r *Review) resolveModuleType(m *Module, qualifiedName string, seen map[string]bool) (typeDef, string, error) {
	key := "module:" + m.ModFile.Module.Mod.Path + ":" + qualifiedName
	if seen[key] {
		return typeDef{}, "", fmt.Errorf("cyclic module type alias %s", qualifiedName)
	}
	seen[key] = true
	defer delete(seen, key)
	dot := strings.LastIndex(qualifiedName, ".")
	if dot < 1 {
		return typeDef{}, "", fmt.Errorf("invalid qualified type %q", qualifiedName)
	}
	importPath, typeName := qualifiedName[:dot], qualifiedName[dot+1:]
	if versionReg.MatchString(m.ModFile.Module.Mod.Path) {
		importPath = strings.ReplaceAll(importPath, m.ModFile.Module.Mod.Path, path.Dir(m.ModFile.Module.Mod.Path))
	}
	p, ok := m.Packages[importPath]
	if !ok {
		return typeDef{}, "", fmt.Errorf("couldn't find package %s for %s", importPath, qualifiedName)
	}
	def, found, err := findTypeDef(typeName, p, m.Packages, map[typeDefRef]bool{}, true)
	if err != nil {
		return typeDef{}, "", err
	}
	if found {
		return r.resolveTypeDefinition(m, def, seen)
	}
	for _, alias := range p.TypeAliases {
		if alias.Name != typeName {
			continue
		}
		if !alias.isAlias {
			def := alias.definition
			def.methodPackage = p
			def.methodName = alias.Name
			return def, "", nil
		}
		if alias.SourceMod != (module.Version{}) {
			return r.resolveExternalType(alias.SourceMod, alias.QualifiedName, seen)
		}
		aliasDot := strings.LastIndex(alias.QualifiedName, ".")
		if aliasDot < 1 {
			return typeDef{}, "", fmt.Errorf("invalid qualified type %q", alias.QualifiedName)
		}
		aliasImportPath := alias.QualifiedName[:aliasDot]
		modulePath := m.ModFile.Module.Mod.Path
		if versionReg.MatchString(modulePath) {
			modulePath = path.Dir(modulePath)
		}
		if strings.HasPrefix(aliasImportPath, modulePath+"/") || aliasImportPath == modulePath {
			return r.resolveModuleType(m, alias.QualifiedName, seen)
		}
		if isStandardLibraryImportPath(aliasImportPath) {
			return typeDef{}, alias.QualifiedName, nil
		}
		return typeDef{}, "", fmt.Errorf("couldn't resolve dependency for alias %s", alias.QualifiedName)
	}
	return typeDef{}, "", fmt.Errorf("couldn't resolve internal type %s", qualifiedName)
}

func (r *Review) resolveTypeDefinition(m *Module, def typeDef, seen map[string]bool) (typeDef, string, error) {
	if def.n == nil || def.p == nil {
		return def, "", nil
	}
	if def.underlyingType != "" {
		return def, "", nil
	}
	isDefined := !def.n.Assign.IsValid()
	methodPackage, methodName := def.methodPackage, def.methodName
	if isDefined && methodPackage == nil {
		methodPackage, methodName = def.p, def.n.Name.Name
	}
	selector, ok := unparenType(def.n.Type).(*ast.SelectorExpr)
	if !ok {
		return def, "", nil
	}
	name, ok := selector.X.(*ast.Ident)
	if !ok {
		return typeDef{}, "", fmt.Errorf("unsupported selector in type %s", def.n.Name.Name)
	}
	importPath, ok := def.imports[name.Name]
	if !ok {
		return typeDef{}, "", fmt.Errorf("unknown import name %s in type %s", name.Name, def.n.Name.Name)
	}
	qualifiedName := importPath + "." + selector.Sel.Name
	mod := sourceModuleForAlias(qualifiedName, m.ModFile.Require)
	var resolved typeDef
	var terminal string
	var err error
	if mod != (module.Version{}) {
		resolved, terminal, err = r.resolveExternalType(mod, qualifiedName, seen)
	} else if importPath == m.ModFile.Module.Mod.Path ||
		strings.HasPrefix(importPath, strings.TrimSuffix(m.ModFile.Module.Mod.Path, "/")+"/") {
		resolved, terminal, err = r.resolveModuleType(m, qualifiedName, seen)
	} else if isStandardLibraryImportPath(importPath) {
		if isDefined {
			def.underlyingType = qualifiedName
			def.methodPackage = methodPackage
			def.methodName = methodName
			return def, "", nil
		}
		return typeDef{}, qualifiedName, nil
	} else {
		return typeDef{}, "", fmt.Errorf("couldn't resolve dependency for type %s", qualifiedName)
	}
	if err != nil {
		return typeDef{}, "", err
	}
	if isDefined {
		if terminal != "" {
			def.underlyingType = terminal
			def.methodPackage = methodPackage
			def.methodName = methodName
			return def, "", nil
		}
		resolved.methodPackage = methodPackage
		resolved.methodName = methodName
	}
	return resolved, terminal, nil
}

func isStandardLibraryImportPath(importPath string) bool {
	first, _, _ := strings.Cut(importPath, "/")
	return first != "" && !strings.Contains(first, ".")
}

func (r *Review) selectedModuleVersion(mod module.Version) (module.Version, error) {
	if mod.Path == "" || mod.Path == r.reviewed.ModFile.Module.Mod.Path {
		return mod, nil
	}
	if selected, ok := r.selectedModules[mod.Path]; ok {
		return selected, nil
	}
	cmd := newGoCommand(r.path, "list", "-mod=readonly", "-m", "-json", mod.Path)
	var stderr bytes.Buffer
	cmd.Stderr = &stderr
	output, err := cmd.Output()
	if err != nil {
		return module.Version{}, fmt.Errorf("resolving selected version for module %s: %w: %s", mod.Path, err, strings.TrimSpace(stderr.String()))
	}
	var selected struct {
		Path    string
		Version string
	}
	if err := json.Unmarshal(output, &selected); err != nil {
		return module.Version{}, fmt.Errorf("decoding selected module %s: %w", mod.Path, err)
	}
	if selected.Path != mod.Path || selected.Version == "" {
		return module.Version{}, fmt.Errorf("go list returned no selected version for module %s", mod.Path)
	}
	resolved := module.Version{Path: selected.Path, Version: selected.Version}
	r.selectedModules[mod.Path] = resolved
	return resolved, nil
}

func (r *Review) replacementImportPath(mod module.Version, resolvedModulePath, importPath string) string {
	if replacement := r.findReplacement(mod); replacement != nil {
		return strings.Replace(importPath, mod.Path, resolvedModulePath, 1)
	}
	return importPath
}

func (r *Review) externalModule(mod module.Version) (*Module, error) {
	key := mod.Path + "@" + mod.Version
	if m, ok := r.externalModules[key]; ok {
		return m, nil
	}
	replacement := r.findReplacement(mod)
	var m *Module
	var err error
	if replacement != nil {
		if replacement.New.Version != "" {
			m, err = getExternalModuleForDependencyInContext(replacement.New, r.path)
		} else {
			dir := filepath.FromSlash(replacement.New.Path)
			if !filepath.IsAbs(dir) {
				dir = filepath.Join(r.path, dir)
			}
			m, err = newModuleForDependencyInContext(dir, r.path)
		}
	} else {
		m, err = getExternalModuleForDependencyInContext(mod, r.path)
	}
	if err != nil {
		return nil, err
	}
	r.externalModules[key] = m
	return m, nil
}

func (r *Review) findReplacement(mod module.Version) *modfile.Replace {
	var wildcard, exact *modfile.Replace
	for _, replacement := range r.reviewed.ModFile.Replace {
		if replacement.Old.Path != mod.Path {
			continue
		}
		if replacement.Old.Version == mod.Version {
			exact = replacement
		} else if replacement.Old.Version == "" {
			wildcard = replacement
		}
	}
	if exact != nil {
		return exact
	}
	return wildcard
}

func (r *Review) resolveLocalTypes() error {
	for _, p := range r.reviewed.Packages {
		if strings.Contains(p.relName, "/internal") {
			continue
		}
		for name, def := range p.types {
			if def.p != p || def.n == nil || def.n.Name.Name != name || !ast.IsExported(name) {
				continue
			}
			typ := unparenType(def.n.Type)
			if def.n.Assign.IsValid() {
				switch typ.(type) {
				case *ast.IndexExpr, *ast.IndexListExpr:
					return fmt.Errorf("generic instantiation alias %s.%s cannot be represented", p.Name(), name)
				case *ast.StarExpr:
					continue
				}
			}
			switch typ.(type) {
			case *ast.ArrayType, *ast.ChanType, *ast.FuncType, *ast.MapType, *ast.StarExpr, *ast.IndexExpr, *ast.IndexListExpr:
				if err := validateCompositeType(def.n.Type, p, p, def.imports); err != nil {
					return fmt.Errorf("unsupported composite type for %s.%s: %w", p.Name(), name, err)
				}
			case *ast.InterfaceType, *ast.StructType:
				if def.n.Assign.IsValid() {
					if err := validateCompositeType(def.n.Type, p, p, def.imports); err != nil {
						return fmt.Errorf("unsupported composite type for %s.%s: %w", p.Name(), name, err)
					}
				}
			}
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
					if err := validateCompositeType(typ, p, p, def.imports); err != nil {
						return fmt.Errorf("unsupported composite type for %s.%s: %w", p.Name(), name, err)
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
				if target := p.unresolvedPrivateAlias(ident.Name); target != "" {
					alias := TypeAlias{Name: name, Package: p, QualifiedName: target, isAlias: original.n.Assign.IsValid()}
					if err := alias.Resolve(typeDef{}); err != nil {
						return fmt.Errorf("resolving alias %s.%s: %w", p.Name(), name, err)
					}
				}
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
				if err := alias.hoistMethods(def); err != nil {
					return fmt.Errorf("resolving alias %s.%s: %w", p.Name(), name, err)
				}
			}
		}
		names = names[:0]
		for name, def := range p.types {
			if ast.IsExported(name) && def.p == p && def.n != nil && def.n.Name.Name == name {
				names = append(names, name)
			}
		}
		sort.Strings(names)
		for _, name := range names {
			if err := hoistMethodsForType(p, name, p, name); err != nil {
				return fmt.Errorf("resolving methods for %s.%s: %w", p.Name(), name, err)
			}
		}
	}
	return nil
}

func (p *Pkg) unresolvedPrivateAlias(name string) string {
	seen := map[string]bool{}
	for !ast.IsExported(name) && !seen[name] {
		seen[name] = true
		if def, ok := p.types[name]; ok && def.p == p && def.n != nil && def.n.Assign.IsValid() {
			if ident, ok := unparenType(def.n.Type).(*ast.Ident); ok {
				name = ident.Name
				continue
			}
		}
		for _, alias := range p.TypeAliases {
			if alias.Name == name && alias.isAlias && alias.resolved {
				return alias.QualifiedName
			}
		}
		break
	}
	return ""
}

// forAll recursively applies a function to all lines and their children
func forAll(lines []ReviewLine, fn func(ReviewLine)) {
	for _, ln := range lines {
		fn(ln)
		forAll(ln.Children, fn)
	}
}
