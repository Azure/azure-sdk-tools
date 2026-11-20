// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

package cmd

import (
	"fmt"
	"go/ast"
	"go/token"
	"path"
	"path/filepath"
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
			reachable := !strings.Contains(source.relName, "/internal") && ast.IsExported(n.Name)
			if targetExportsType(target, source, n.Name) {
				reachable = true
			}
			for targetName, def := range target.types {
				if ast.IsExported(targetName) && def.p == source && def.n != nil && def.n.Name.Name == n.Name {
					reachable = true
					break
				}
			}
			if _, found := source.types[n.Name]; found && !reachable {
				return fmt.Errorf("hidden component type %s.%s", source.Name(), n.Name)
			}
			for _, alias := range source.TypeAliases {
				if alias.Name == n.Name && !reachable {
					return fmt.Errorf("hidden component type %s.%s", source.Name(), n.Name)
				}
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
		case *ast.IndexExpr, *ast.IndexListExpr:
			return fmt.Errorf("generic instantiation %s cannot be represented", source.getText(expr.Pos(), expr.End()))
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
	return targetExportsTypeSeen(target, source, name, map[string]bool{})
}

func targetExportsTypeSeen(target, source *Pkg, name string, seen map[string]bool) bool {
	qualifiedName := source.importPath() + "." + name
	if seen[qualifiedName] {
		return false
	}
	seen[qualifiedName] = true
	if targetExportsQualifiedType(target, qualifiedName) {
		return true
	}
	for _, alias := range source.TypeAliases {
		if ast.IsExported(alias.Name) && alias.QualifiedName == qualifiedName &&
			targetExportsTypeSeen(target, source, alias.Name, seen) {
			return true
		}
	}
	for aliasName, def := range source.types {
		if !ast.IsExported(aliasName) || def.n == nil || !def.n.Assign.IsValid() {
			continue
		}
		if ident, ok := unparenType(def.n.Type).(*ast.Ident); ok && ident.Name == name &&
			targetExportsTypeSeen(target, source, aliasName, seen) {
			return true
		}
	}
	return false
}

func targetExportsQualifiedType(target *Pkg, qualifiedName string) bool {
	for _, alias := range target.TypeAliases {
		if ast.IsExported(alias.Name) && alias.QualifiedName == qualifiedName {
			return true
		}
	}
	return false
}

func (a *TypeAlias) compositeType(def typeDef, expr ast.Expr) (string, error) {
	if err := validateCompositeType(expr, def.p, a.Package, def.imports); err != nil {
		return "", fmt.Errorf("unsupported composite type for %s.%s: %w", a.Package.Name(), a.Name, err)
	}
	var selectors []*ast.SelectorExpr
	ast.Inspect(expr, func(n ast.Node) bool {
		if selector, ok := n.(*ast.SelectorExpr); ok {
			if name, ok := selector.X.(*ast.Ident); ok && def.imports[name.Name] != "" {
				selectors = append(selectors, selector)
				return false
			}
		}
		return true
	})
	sort.Slice(selectors, func(i, j int) bool { return selectors[i].Pos() < selectors[j].Pos() })
	var result strings.Builder
	last := expr.Pos()
	for _, selector := range selectors {
		result.WriteString(def.p.translateType(def.p.getText(last, selector.Pos()), def.imports))
		original := def.p.getText(selector.Pos(), selector.End())
		qualified := def.imports[selector.X.(*ast.Ident).Name] + "." + selector.Sel.Name
		translated := def.p.translateType(original, def.imports)
		if def.p.modulePath == a.Package.modulePath &&
			strings.HasPrefix(def.imports[selector.X.(*ast.Ident).Name], def.p.unversionedModulePath()+"/") {
			rel := strings.TrimPrefix(def.imports[selector.X.(*ast.Ident).Name], def.p.unversionedModulePath())
			packageName := path.Base(def.p.unversionedModulePath()) + filepath.ToSlash(rel)
			translated = fmt.Sprintf("<%s.%s>%s", packageName, selector.Sel.Name, qualified)
		}
		prefix := strings.TrimSuffix(translated, original)
		if prefix == translated {
			result.WriteString(qualified)
		} else {
			result.WriteString(prefix + qualified)
		}
		last = selector.End()
	}
	result.WriteString(def.p.translateType(def.p.getText(last, expr.End()), def.imports))
	return result.String(), nil
}
