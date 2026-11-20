// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

package cmd

import (
	"fmt"
	"go/ast"
	"go/token"
	"path"
	"sort"
	"strings"
)

func validateCompositeType(expr ast.Expr, source, target *Pkg, imports map[string]string) error {
	var walk func(ast.Expr) error
	var fields func(*ast.FieldList, bool) error
	fields = func(list *ast.FieldList, includeUnexported bool) error {
		if list != nil {
			for _, field := range list.List {
				if !includeUnexported && len(field.Names) > 0 {
					exported := false
					for _, name := range field.Names {
						exported = exported || ast.IsExported(name.Name)
					}
					if !exported {
						continue
					}
				}
				if err := walk(field.Type); err != nil {
					return err
				}
			}
		}
		return nil
	}
	walk = func(expr ast.Expr) error {
		switch n := expr.(type) {
		case *ast.ParenExpr:
			return walk(n.X)
		case *ast.Ident:
			reachable := ast.IsExported(n.Name) && !isInternalPackage(source)
			reachable = reachable || targetExportsType(target, source, n.Name)
			for _, alias := range source.TypeAliases {
				if alias.Name == n.Name && alias.isAlias {
					reachable = reachable || targetExportsQualifiedType(target, alias.QualifiedName)
				}
			}
			_, found := source.types[n.Name]
			if (found || hasTypeAlias(source, n.Name)) && !reachable {
				return fmt.Errorf("hidden component type %s.%s", source.Name(), n.Name)
			}
		case *ast.SelectorExpr:
			name, ok := n.X.(*ast.Ident)
			if !ok {
				return fmt.Errorf("unsupported selector expression %s", source.getText(n.Pos(), n.End()))
			}
			imp := imports[name.Name]
			if imp == "" {
				return fmt.Errorf("unknown import name %s in selector %s", name.Name, source.getText(n.Pos(), n.End()))
			}
			if (strings.HasSuffix(imp, "/internal") || strings.Contains(imp, "/internal/")) &&
				!targetExportsQualifiedType(target, imp+"."+n.Sel.Name) {
				return fmt.Errorf("hidden component type %s.%s", imp, n.Sel.Name)
			}
			if source.modulePath != target.modulePath && strings.HasPrefix(imp, source.unversionedModulePath()+"/") {
				return fmt.Errorf("component package %s is not included in the review", imp)
			}
		case *ast.StarExpr:
			return walk(n.X)
		case *ast.Ellipsis:
			return walk(n.Elt)
		case *ast.ArrayType:
			if n.Len != nil {
				literal, ok := unparenType(n.Len).(*ast.BasicLit)
				if !ok || literal.Kind != token.INT {
					return fmt.Errorf("array length expression %q cannot be represented", source.getText(n.Len.Pos(), n.Len.End()))
				}
			}
			return walk(n.Elt)
		case *ast.MapType:
			if err := walk(n.Key); err != nil {
				return err
			}
			return walk(n.Value)
		case *ast.ChanType:
			return walk(n.Value)
		case *ast.FuncType:
			if err := fields(n.Params, true); err != nil {
				return err
			}
			return fields(n.Results, true)
		case *ast.StructType:
			return fields(n.Fields, false)
		case *ast.InterfaceType:
			return fields(n.Methods, false)
		case *ast.IndexExpr:
			if err := walk(n.X); err != nil {
				return err
			}
			return walk(n.Index)
		case *ast.IndexListExpr:
			if err := walk(n.X); err != nil {
				return err
			}
			for _, index := range n.Indices {
				if err := walk(index); err != nil {
					return err
				}
			}
		default:
			return fmt.Errorf("unsupported component type %T", n)
		}
		return nil
	}
	return walk(expr)
}

func (p *Pkg) importPath() string {
	modulePath := p.unversionedModulePath()
	rel := strings.TrimPrefix(p.relName, path.Base(modulePath))
	rel = strings.TrimPrefix(rel, "/")
	if rel == "" {
		return modulePath
	}
	return path.Join(modulePath, rel)
}

func targetExportsType(target, source *Pkg, name string) bool {
	qualifiedName := normalizeAliasTarget(target.modulePackages, source.importPath()+"."+name, map[string]bool{})
	if source == target && ast.IsExported(name) && !isInternalPackage(source) {
		return true
	}
	for _, pkg := range target.modulePackages {
		if isInternalPackage(pkg) {
			continue
		}
		if aliasPackageExports(pkg, qualifiedName, map[string]bool{}) {
			return true
		}
	}
	return false
}

func targetExportsQualifiedType(target *Pkg, qualifiedName string) bool {
	qualifiedName = normalizeAliasTarget(target.modulePackages, qualifiedName, map[string]bool{})
	for _, pkg := range target.modulePackages {
		if isInternalPackage(pkg) {
			continue
		}
		if aliasPackageExports(pkg, qualifiedName, map[string]bool{}) {
			return true
		}
	}
	return false
}

func isInternalPackage(pkg *Pkg) bool {
	return pkg != nil && (strings.HasSuffix(pkg.relName, "/internal") || strings.Contains(pkg.relName, "/internal/"))
}

func aliasPackageExports(pkg *Pkg, qualifiedName string, seen map[string]bool) bool {
	key := typeIdentityKey(pkg.modulePackages, qualifiedName)
	if seen[key] {
		return false
	}
	seen[key] = true
	for _, alias := range pkg.TypeAliases {
		if alias.isAlias && ast.IsExported(alias.Name) &&
			sameTypeIdentity(pkg.modulePackages,
				normalizeAliasTarget(pkg.modulePackages, aliasTarget(pkg, alias.Name, map[string]bool{}), map[string]bool{}),
				qualifiedName) {
			return true
		}
	}
	for name, def := range pkg.types {
		if !ast.IsExported(name) || def.n == nil || !def.n.Assign.IsValid() {
			continue
		}
		targetName := aliasTarget(pkg, name, map[string]bool{})
		if sameTypeIdentity(pkg.modulePackages, normalizeAliasTarget(pkg.modulePackages, targetName, map[string]bool{}), qualifiedName) {
			return true
		}
	}
	return false
}

func aliasTarget(pkg *Pkg, name string, seen map[string]bool) string {
	key := pkg.importPath() + "." + name
	if seen[key] {
		return ""
	}
	seen[key] = true
	for _, alias := range pkg.TypeAliases {
		if alias.Name == name && alias.isAlias {
			return alias.QualifiedName
		}
	}
	def, ok := pkg.types[name]
	if !ok || def.n == nil || !def.n.Assign.IsValid() {
		return ""
	}
	switch typ := unparenType(def.n.Type).(type) {
	case *ast.Ident:
		if target, ok := pkg.types[typ.Name]; ok {
			if target.n != nil && target.n.Assign.IsValid() {
				return aliasTarget(pkg, typ.Name, seen)
			}
			if target.n != nil {
				return pkg.importPath() + "." + typ.Name
			}
			return aliasTarget(pkg, typ.Name, seen)
		}
		if ast.IsExported(typ.Name) {
			return pkg.importPath() + "." + typ.Name
		}
	case *ast.SelectorExpr:
		if selector, ok := typ.X.(*ast.Ident); ok {
			if importPath := def.imports[selector.Name]; importPath != "" {
				return importPath + "." + typ.Sel.Name
			}
		}
	}
	return ""
}

func normalizeAliasTarget(packages map[string]*Pkg, qualifiedName string, seen map[string]bool) string {
	dot := strings.LastIndex(qualifiedName, ".")
	key := typeIdentityKey(packages, qualifiedName)
	if dot < 1 || seen[key] {
		return qualifiedName
	}
	seen[key] = true
	pkg := packageForTypeImport(packages, qualifiedName[:dot])
	if pkg == nil {
		return qualifiedName
	}
	name := qualifiedName[dot+1:]
	if _, ok := pkg.types[name]; ok {
		if target := aliasTarget(pkg, name, map[string]bool{}); target != "" && target != qualifiedName {
			return normalizeAliasTarget(packages, target, seen)
		}
	}
	for _, alias := range pkg.TypeAliases {
		if alias.Name == name && alias.isAlias {
			return normalizeAliasTarget(packages, alias.QualifiedName, seen)
		}
	}
	return qualifiedName
}

func packageForTypeImport(packages map[string]*Pkg, importPath string) *Pkg {
	if pkg, ok := packages[importPath]; ok {
		return pkg
	}
	for _, pkg := range packages {
		if versionedPackageImportPath(pkg) == importPath {
			return pkg
		}
	}
	return nil
}

func versionedPackageImportPath(pkg *Pkg) string {
	if !versionReg.MatchString(pkg.modulePath) {
		return pkg.importPath()
	}
	relativePath := strings.TrimPrefix(pkg.importPath(), pkg.unversionedModulePath())
	return pkg.modulePath + relativePath
}

func typeIdentityKey(packages map[string]*Pkg, qualifiedName string) string {
	dot := strings.LastIndex(qualifiedName, ".")
	if dot < 1 {
		return qualifiedName
	}
	if pkg := packageForTypeImport(packages, qualifiedName[:dot]); pkg != nil {
		return fmt.Sprintf("%p.%s", pkg, qualifiedName[dot+1:])
	}
	return qualifiedName
}

func sameTypeIdentity(packages map[string]*Pkg, left, right string) bool {
	return typeIdentityKey(packages, left) == typeIdentityKey(packages, right)
}

func (a *TypeAlias) compositeType(def typeDef, expr ast.Expr) (string, error) {
	if err := validateCompositeType(expr, def.p, a.Package, def.imports); err != nil {
		return "", fmt.Errorf("unsupported composite type for %s.%s: %w", a.Package.Name(), a.Name, err)
	}
	references := []ast.Expr{}
	collectTypeReferences(expr, def.p, &references)
	sort.Slice(references, func(i, j int) bool { return references[i].Pos() < references[j].Pos() })
	var result strings.Builder
	last := expr.Pos()
	for _, reference := range references {
		result.WriteString(def.p.getText(last, reference.Pos()))
		translated, err := a.translateComponentReference(def, reference)
		if err != nil {
			return "", err
		}
		result.WriteString(translated)
		last = reference.End()
	}
	result.WriteString(def.p.getText(last, expr.End()))
	return result.String(), nil
}

func collectTypeReferences(expr ast.Expr, source *Pkg, references *[]ast.Expr) {
	switch n := unparenType(expr).(type) {
	case *ast.Ident:
		if _, ok := source.types[n.Name]; ok || hasTypeAlias(source, n.Name) {
			*references = append(*references, n)
		}
	case *ast.SelectorExpr:
		*references = append(*references, n)
	case *ast.StarExpr:
		collectTypeReferences(n.X, source, references)
	case *ast.Ellipsis:
		collectTypeReferences(n.Elt, source, references)
	case *ast.ArrayType:
		collectTypeReferences(n.Elt, source, references)
	case *ast.MapType:
		collectTypeReferences(n.Key, source, references)
		collectTypeReferences(n.Value, source, references)
	case *ast.ChanType:
		collectTypeReferences(n.Value, source, references)
	case *ast.FuncType:
		collectFieldTypes(n.Params, source, references)
		collectFieldTypes(n.Results, source, references)
	case *ast.StructType:
		for _, field := range n.Fields.List {
			if len(field.Names) == 0 || ast.IsExported(field.Names[0].Name) {
				collectTypeReferences(field.Type, source, references)
			}
		}
	case *ast.InterfaceType:
		for _, field := range n.Methods.List {
			if len(field.Names) == 0 || ast.IsExported(field.Names[0].Name) {
				collectTypeReferences(field.Type, source, references)
			}
		}
	case *ast.IndexExpr:
		collectTypeReferences(n.X, source, references)
		collectTypeReferences(n.Index, source, references)
	case *ast.IndexListExpr:
		collectTypeReferences(n.X, source, references)
		for _, index := range n.Indices {
			collectTypeReferences(index, source, references)
		}
	}
}

func collectFieldTypes(fields *ast.FieldList, source *Pkg, references *[]ast.Expr) {
	if fields == nil {
		return
	}
	for _, field := range fields.List {
		collectTypeReferences(field.Type, source, references)
	}
}

func (a *TypeAlias) translateComponentReference(def typeDef, reference ast.Expr) (string, error) {
	switch n := reference.(type) {
	case *ast.Ident:
		endpoint, name, ok := findExposedType(a.Package, def.p, n.Name)
		if !ok {
			if ast.IsExported(n.Name) && !isInternalPackage(def.p) {
				return def.p.importPath() + "." + n.Name, nil
			}
			return "", fmt.Errorf("hidden component type %s.%s", def.p.Name(), n.Name)
		}
		if endpoint == a.Package {
			return fmt.Sprintf("<%s.%s>%s", endpoint.Name(), name, name), nil
		}
		text := endpoint.Name() + "." + name
		return fmt.Sprintf("<%s.%s>%s", endpoint.Name(), name, text), nil
	case *ast.SelectorExpr:
		x, ok := n.X.(*ast.Ident)
		if !ok {
			return "", fmt.Errorf("unsupported selector expression %s", def.p.getText(n.Pos(), n.End()))
		}
		importPath := def.imports[x.Name]
		qualified := importPath + "." + n.Sel.Name
		if endpoint, name, ok := findExposedQualifiedType(a.Package, qualified); ok {
			text := endpoint.Name() + "." + name
			if endpoint == a.Package {
				text = name
			}
			return fmt.Sprintf("<%s.%s>%s", endpoint.Name(), name, text), nil
		}
		original := def.p.getText(n.Pos(), n.End())
		translated := def.p.translateType(original, def.imports)
		prefix := strings.TrimSuffix(translated, original)
		if prefix != translated {
			return prefix + qualified, nil
		}
		return qualified, nil
	}
	return def.p.getText(reference.Pos(), reference.End()), nil
}

func findExposedType(target, source *Pkg, name string) (*Pkg, string, bool) {
	for _, alias := range source.TypeAliases {
		if alias.Name == name && alias.isAlias {
			if ast.IsExported(name) && !isInternalPackage(source) && target.modulePackages[source.importPath()] == source {
				return source, name, true
			}
			return findExposedQualifiedType(target, alias.QualifiedName)
		}
	}
	if ast.IsExported(name) && !isInternalPackage(source) && target.modulePackages[source.importPath()] == source {
		return source, name, true
	}
	qualifiedName := source.importPath() + "." + name
	return findExposedQualifiedType(target, qualifiedName)
}

func hasTypeAlias(pkg *Pkg, name string) bool {
	for _, alias := range pkg.TypeAliases {
		if alias.Name == name {
			return true
		}
	}
	return false
}

func findExposedQualifiedType(target *Pkg, qualifiedName string) (*Pkg, string, bool) {
	packages := target.modulePackages
	if packages == nil {
		packages = map[string]*Pkg{target.importPath(): target}
	}
	keys := make([]string, 0, len(packages))
	for key := range packages {
		keys = append(keys, key)
	}
	sort.Strings(keys)
	for _, key := range keys {
		pkg := packages[key]
		if isInternalPackage(pkg) {
			continue
		}
		aliases := append([]*TypeAlias(nil), pkg.TypeAliases...)
		sort.Slice(aliases, func(i, j int) bool { return aliases[i].Name < aliases[j].Name })
		for _, alias := range aliases {
			if alias.isAlias && ast.IsExported(alias.Name) &&
				sameTypeIdentity(packages,
					normalizeAliasTarget(packages, aliasTarget(pkg, alias.Name, map[string]bool{}), map[string]bool{}),
					normalizeAliasTarget(packages, qualifiedName, map[string]bool{})) {
				return pkg, alias.Name, true
			}
		}
		names := make([]string, 0, len(pkg.types))
		for name := range pkg.types {
			names = append(names, name)
		}
		sort.Strings(names)
		for _, name := range names {
			def := pkg.types[name]
			if ast.IsExported(name) && def.n != nil && def.n.Assign.IsValid() &&
				sameTypeIdentity(packages,
					normalizeAliasTarget(packages, aliasTarget(pkg, name, map[string]bool{}), map[string]bool{}),
					normalizeAliasTarget(packages, qualifiedName, map[string]bool{})) {
				return pkg, name, true
			}
		}
	}
	return nil, "", false
}
