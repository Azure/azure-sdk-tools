// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

package cmd

import (
	"fmt"
	"go/ast"
	"go/token"
	"sort"
	"strings"
)

func validateCompositeType(expr ast.Expr, source, target *Pkg, imports map[string]string) error {
	var walk func(ast.Expr) error
	var fields func(*ast.FieldList) error
	fields = func(list *ast.FieldList) error {
		if list != nil {
			for _, field := range list.List {
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
			if _, found := source.types[n.Name]; found && (source != target || !ast.IsExported(n.Name)) {
				return fmt.Errorf("hidden component type %s.%s", source.Name(), n.Name)
			}
			for _, alias := range source.TypeAliases {
				if alias.Name == n.Name && (source != target || !ast.IsExported(n.Name)) {
					return fmt.Errorf("hidden component type %s.%s", source.Name(), n.Name)
				}
			}
		case *ast.SelectorExpr:
			if name, ok := n.X.(*ast.Ident); ok {
				if imp := imports[name.Name]; imp != "" && (strings.HasSuffix(imp, "/internal") || strings.Contains(imp, "/internal/")) {
					return fmt.Errorf("hidden component type %s.%s", imp, n.Sel.Name)
				}
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
			if err := fields(n.Params); err != nil {
				return err
			}
			return fields(n.Results)
		case *ast.StructType:
			return fields(n.Fields)
		case *ast.InterfaceType:
			return fields(n.Methods)
		case *ast.IndexExpr, *ast.IndexListExpr:
			return fmt.Errorf("generic instantiation %s cannot be represented", source.getText(expr.Pos(), expr.End()))
		default:
			return fmt.Errorf("unsupported component type %T", n)
		}
		return nil
	}
	return walk(expr)
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
		result.WriteString(strings.TrimSuffix(translated, original) + qualified)
		last = selector.End()
	}
	result.WriteString(def.p.translateType(def.p.getText(last, expr.End()), def.imports))
	return result.String(), nil
}
