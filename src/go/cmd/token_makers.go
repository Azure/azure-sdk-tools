// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

package cmd

import (
	"bytes"
	"fmt"
	"go/ast"
	"go/format"
	"go/parser"
	"go/token"
	"regexp"
	"sort"
	"strings"
	"unicode"
	"unicode/utf8"

	"golang.org/x/exp/slices"
)

// exportedFieldRgx matches exported field names like "policy.ClientOptions", "Transport", and "GetToken(...)"
var exportedFieldRgx = regexp.MustCompile(`^(?:[a-zA-Z]*\.)?[A-Z]+[a-zA-Z]*`)

type ReviewLineMaker interface {
	MakeReviewLine() ReviewLine
}

type TokenMaker interface {
	Exported() bool
	ID() string
	MakeTokens() []ReviewToken
	Name() string
}

// Declaration is a const or var declaration.
type Declaration struct {
	Type string

	id    string
	name  string
	value string
}

func NewDeclaration(pkg Pkg, vs *ast.ValueSpec, imports map[string]string) Declaration {
	v := skip
	if len(vs.Values) > 0 {
		v = getExprValue(pkg, vs.Values[0])
	}
	decl := Declaration{id: pkg.Name() + "." + vs.Names[0].Name, name: vs.Names[0].Name, value: v}
	// Type is nil for untyped consts
	if vs.Type != nil {
		switch x := vs.Type.(type) {
		case *ast.Ident:
			// const ETagAny ETag = "*"
			// const PeekLock ReceiveMode = internal.PeekLock
			decl.Type = pkg.translateType(x.Name, imports)
		case *ast.MapType:
			// var nullables map[reflect.Type]interface{} = map[reflect.Type]interface{}{}
			decl.Type = pkg.translateType(pkg.getText(vs.Type.Pos(), vs.Type.End()), imports)
		case *ast.SelectorExpr:
			// const LogCredential log.Classification = "Credential"
			decl.Type = pkg.translateType(x.Sel.Name, imports)
		case *ast.StarExpr:
			switch xX := x.X.(type) {
			case *ast.Ident:
				// var SomeTypeValue *SomeType
				decl.Type = pkg.translateType("*"+xX.Name, imports)
			case *ast.SelectorExpr:
				// var defaultHTTPClient *http.Client
				decl.Type = pkg.translateType(fmt.Sprintf("*%s.%s", xX.X, xX.Sel.Name), imports)
			default:
				fmt.Printf("unhandled declaration type %T for %s\n", xX, pkg.getText(vs.Type.Pos(), vs.Type.End()))
			}
		default:
			fmt.Println("unhandled declaration " + pkg.getText(vs.Type.Pos(), vs.Type.End()))
		}
	} else if len(vs.Values) == 1 {
		switch t := vs.Values[0].(type) {
		case *ast.CallExpr:
			// const FooConst = Foo("value")
			// var Foo = NewFoo()
			// TODO: determining the type here requires finding the definition of the called function. We may
			// not have encountered that yet, so we would need to set the types of these declarations after
			// traversing the entire AST.
		case *ast.CompositeLit:
			// var AzureChina = Configuration{ ... }
			decl.Type = pkg.translateType(pkg.getText(t.Type.Pos(), t.Type.End()), imports)
		}
	} else {
		// implicitly typed const
		decl.Type = skip
	}
	return decl
}

func (d Declaration) Exported() bool {
	return unicode.IsUpper(rune(d.name[0]))
}

func (d Declaration) ID() string {
	return d.id
}

func (d Declaration) MakeTokens() []ReviewToken {
	rts := []ReviewToken{
		{
			HasSuffixSpace:        true,
			Kind:                  TokenKindTypeName,
			NavigationDisplayName: d.Name(),
			NavigateToID:          d.ID(),
			Value:                 d.Name(),
		},
	}
	if d.Type != skip {
		rts = append(rts, parseAndMakeTypeTokens(d.Type)...)
	}
	rts = append(rts, ReviewToken{
		HasPrefixSpace: true,
		HasSuffixSpace: true,
		Kind:           TokenKindPunctuation,
		Value:          "=",
	})
	rts = append(rts, ReviewToken{
		Kind:  TokenKindStringLiteral,
		Value: d.value,
	})
	return rts
}

func (d Declaration) Name() string {
	return d.name
}

type Func struct {
	ReceiverName string
	ReceiverType string
	rawType      *ast.FuncType
	imports      map[string]string
	// Returns lists the func's return types
	Returns []string

	embedded bool
	exported bool
	id       string
	// name includes the function's receiver, if any
	name string
	// paramNames lists the func's parameters name
	paramNames []string
	// paramTypes lists the func's parameters type
	paramTypes        []string
	paramTypeSources  []string
	returnTypeSources []string
	// typeParamNames lists the func's type parameters name
	typeParamNames []string
	// typeParamConstraints lists the func's type parameters constraint
	typeParamConstraints []string
}

func NewFunc(pkg Pkg, f *ast.FuncDecl, imports map[string]string) Func {
	fn := newFunc(pkg, f.Type, imports)
	fn.name = f.Name.Name
	if f.Recv != nil {
		var receiver bytes.Buffer
		if err := format.Node(&receiver, pkg.fs, f.Recv.List[0].Type); err != nil {
			panic(fmt.Errorf("formatting receiver for %s: %w", fn.name, err))
		}
		fn.ReceiverType = receiver.String()
		if len(f.Recv.List[0].Names) != 0 {
			fn.ReceiverName = f.Recv.List[0].Names[0].Name
		}
	}
	fn.exported = (fn.ReceiverType == "" || ast.IsExported(fn.receiverTypeName())) && unicode.IsUpper(rune(fn.name[0]))
	fn.id = pkg.Name() + "-" + fn.signature()
	return fn
}

func NewFuncForInterfaceMethod(pkg Pkg, interfaceName string, f *ast.Field, imports map[string]string) Func {
	fn := newFunc(pkg, f.Type.(*ast.FuncType), imports)
	fn.name = f.Names[0].Name
	fn.exported = unicode.IsUpper(rune(fn.name[0]))
	fn.id = pkg.Name() + "-" + interfaceName + "-" + fn.name
	fn.embedded = true
	return fn
}

func newFunc(pkg Pkg, f *ast.FuncType, imports map[string]string) Func {
	fn := Func{rawType: f, imports: imports}
	if f.TypeParams != nil {
		fn.typeParamNames = make([]string, 0, len(f.TypeParams.List))
		fn.typeParamConstraints = make([]string, 0, len(f.TypeParams.List))
		pkg.translateFieldList(f.TypeParams.List, func(param *string, constraint string) {
			fn.typeParamNames = append(fn.typeParamNames, *param)
			fn.typeParamConstraints = append(fn.typeParamConstraints, pkg.translateType(strings.TrimRight(constraint, " "), imports))
		})
	}
	if f.Params.List != nil {
		fn.paramNames = make([]string, 0, len(f.Params.List))
		fn.paramTypes = make([]string, 0, len(f.Params.List))
		fn.paramTypeSources = make([]string, 0, len(f.Params.List))
		pkg.translateFieldList(f.Params.List, func(n *string, t string) {
			if n != nil {
				fn.paramNames = append(fn.paramNames, *n)
			} else {
				fn.paramNames = append(fn.paramNames, "")
			}
			fn.paramTypes = append(fn.paramTypes, pkg.translateSignatureType(t, imports))
			fn.paramTypeSources = append(fn.paramTypeSources, t)
		})
	}
	if f.Results != nil {
		fn.Returns = make([]string, 0, len(f.Results.List))
		fn.returnTypeSources = make([]string, 0, len(f.Results.List))
		pkg.translateFieldList(f.Results.List, func(n *string, t string) {
			fn.Returns = append(fn.Returns, pkg.translateSignatureType(t, imports))
			fn.returnTypeSources = append(fn.returnTypeSources, t)
		})
	}
	return fn
}

func (f Func) Exported() bool {
	return f.exported
}

func (f Func) ID() string {
	return f.id
}

func (f Func) receiverTypeName() string {
	expr, err := parser.ParseExpr(f.ReceiverType)
	if err != nil {
		return ""
	}
	for {
		switch n := expr.(type) {
		case *ast.ParenExpr:
			expr = n.X
		case *ast.StarExpr:
			expr = n.X
		case *ast.IndexExpr:
			expr = n.X
		case *ast.IndexListExpr:
			expr = n.X
		case *ast.Ident:
			return n.Name
		default:
			return ""
		}
	}
}

func (pkg Pkg) translateSignatureType(text string, imports map[string]string) string {
	if strings.HasPrefix(text, "...") {
		return "..." + pkg.translateSignatureType(text[3:], imports)
	}
	fset := token.NewFileSet()
	expr, err := parser.ParseExprFrom(fset, "", text, 0)
	if err != nil {
		return pkg.translateType(text, imports)
	}
	var names []*ast.Ident
	ast.Inspect(expr, func(n ast.Node) bool {
		if field, ok := n.(*ast.Field); ok {
			names = append(names, field.Names...)
		}
		return true
	})
	sort.Slice(names, func(i, j int) bool { return names[i].Pos() < names[j].Pos() })
	var result strings.Builder
	last := 0
	for _, name := range names {
		start, end := fset.Position(name.Pos()).Offset, fset.Position(name.End()).Offset
		result.WriteString(pkg.translateType(text[last:start], imports))
		result.WriteString(text[start:end])
		last = end
	}
	result.WriteString(pkg.translateType(text[last:], imports))
	return result.String()
}

func (f Func) signature() string {
	if f.ReceiverType == "" {
		return f.name
	}
	if f.ReceiverName == "" {
		return fmt.Sprintf("(%s) %s", f.ReceiverType, f.name)
	}
	return fmt.Sprintf("(%s %s) %s", f.ReceiverName, f.ReceiverType, f.name)
}

func (f Func) ForAlias(source, target *Pkg, typeName, aliasName string, sourceNames []string) Func {
	clone := f
	prefix := ""
	receiver, pointer := normalizeReceiverType(clone.ReceiverType)
	if pointer {
		prefix = "*"
	}
	clone.ReceiverType = prefix + aliasName + strings.TrimPrefix(receiver, typeName)
	clone.id = target.Name() + "-" + clone.signature()
	clone.exported = ast.IsExported(aliasName) && ast.IsExported(clone.name)
	clone.paramTypes = append([]string(nil), f.paramTypes...)
	for i, typ := range clone.paramTypes {
		if i < len(f.paramTypeSources) {
			clone.paramTypes[i] = f.translateAliasSignatureType(source, target, f.paramTypeSources[i], typ, aliasName, sourceNames)
		}
	}
	clone.Returns = append([]string(nil), f.Returns...)
	for i, typ := range clone.Returns {
		if i < len(f.returnTypeSources) {
			clone.Returns[i] = f.translateAliasSignatureType(source, target, f.returnTypeSources[i], typ, aliasName, sourceNames)
		}
	}
	return clone
}

func (f Func) translateAliasSignatureType(source, target *Pkg, sourceType, translated, aliasName string, sourceNames []string) string {
	replacements := []string{}
	type selectorReplacement struct {
		source string
		target string
	}
	selectorReplacements := []selectorReplacement{}
	sourceAliases := make(map[string]bool, len(sourceNames))
	for _, name := range sourceNames {
		sourceAliases[name] = true
	}

	localNames := make(map[string]bool, len(source.types)+len(source.TypeAliases))
	for name := range source.types {
		localNames[name] = true
	}
	for _, alias := range source.TypeAliases {
		localNames[alias.Name] = true
	}
	names := make([]string, 0, len(localNames))
	for name := range localNames {
		names = append(names, name)
	}
	sort.Strings(names)
	for _, name := range names {
		endpoint, exposedName, found := findExposedSignatureType(target, source.importPath()+"."+name)
		if sourceAliases[name] {
			endpoint, exposedName, found = target, aliasName, true
		}
		if !found {
			continue
		}
		displayName := exposedName
		if endpoint != target {
			displayName = endpoint.p.Name + "." + exposedName
		}
		replacements = append(replacements,
			fmt.Sprintf("<%s.%s>%s", source.Name(), name, name),
			fmt.Sprintf("<%s.%s>%s", endpoint.Name(), exposedName, displayName))
	}

	parseType := sourceType
	offset := 0
	if strings.HasPrefix(parseType, "...") {
		parseType = strings.TrimPrefix(parseType, "...")
		offset = 3
	}
	fset := token.NewFileSet()
	expr, err := parser.ParseExprFrom(fset, "", parseType, 0)
	if err == nil {
		ast.Inspect(expr, func(n ast.Node) bool {
			selector, ok := n.(*ast.SelectorExpr)
			if !ok {
				return true
			}
			packageName, ok := selector.X.(*ast.Ident)
			if !ok {
				return true
			}
			importPath, ok := f.imports[packageName.Name]
			if !ok {
				return true
			}
			start := fset.Position(selector.Pos()).Offset + offset
			end := fset.Position(selector.End()).Offset + offset
			original := sourceType[start:end]
			sourceExpr := source.translateType(original, f.imports)
			targetExpr := importPath + "." + selector.Sel.Name
			if endpoint, exposedName, found := findExposedSignatureType(target, targetExpr); found {
				if packageForTypeImport(target.modulePackages, importPath) == endpoint && exposedName == selector.Sel.Name {
					targetExpr = sourceExpr
				} else {
					displayName := exposedName
					if endpoint != target {
						displayName = endpoint.p.Name + "." + exposedName
					}
					targetExpr = fmt.Sprintf("<%s.%s>%s", endpoint.Name(), exposedName, displayName)
				}
			}
			selectorReplacements = append(selectorReplacements, selectorReplacement{source: sourceExpr, target: targetExpr})
			return true
		})
	}
	if len(replacements) == 0 {
		for _, replacement := range selectorReplacements {
			translated = replaceCompleteTypeExpression(translated, replacement.source, replacement.target)
		}
		return translated
	}
	translated = strings.NewReplacer(replacements...).Replace(translated)
	for _, replacement := range selectorReplacements {
		translated = replaceCompleteTypeExpression(translated, replacement.source, replacement.target)
	}
	return translated
}

func replaceCompleteTypeExpression(text, source, target string) string {
	var result strings.Builder
	last := 0
	for offset := 0; offset < len(text); {
		relative := strings.Index(text[offset:], source)
		if relative < 0 {
			break
		}
		start := offset + relative
		end := start + len(source)
		previous := previousRune(text, start)
		hasLeftBoundary := start == 0 || !isTypeIdentifierRune(previous)
		if !hasLeftBoundary && previous == '.' && start >= 3 && text[start-3:start] == "..." {
			hasLeftBoundary = true
		}
		hasRightBoundary := end == len(text) || !isTypeIdentifierRune(nextRune(text, end))
		if hasLeftBoundary && hasRightBoundary {
			result.WriteString(text[last:start])
			result.WriteString(target)
			last = end
			offset = end
		} else {
			offset = start + 1
		}
	}
	if last == 0 {
		return text
	}
	result.WriteString(text[last:])
	return result.String()
}

func previousRune(text string, offset int) rune {
	r, _ := utf8.DecodeLastRuneInString(text[:offset])
	return r
}

func nextRune(text string, offset int) rune {
	r, _ := utf8.DecodeRuneInString(text[offset:])
	return r
}

func isTypeIdentifierRune(r rune) bool {
	return r == '_' || r == '.' || unicode.IsLetter(r) || unicode.IsDigit(r)
}

func findExposedSignatureType(target *Pkg, qualifiedName string) (*Pkg, string, bool) {
	if pkg, name, found := findExposedQualifiedType(target, qualifiedName); found {
		return pkg, name, true
	}
	dot := strings.LastIndex(qualifiedName, ".")
	if dot < 1 {
		return nil, "", false
	}
	importPath, name := qualifiedName[:dot], qualifiedName[dot+1:]
	packages := target.modulePackages
	if packages == nil {
		packages = map[string]*Pkg{target.importPath(): target}
	}
	pkg := packageForTypeImport(packages, importPath)
	if pkg == nil || isInternalPackage(pkg) || !ast.IsExported(name) {
		return nil, "", false
	}
	def, found := pkg.types[name]
	if !found || def.n == nil {
		return nil, "", false
	}
	return pkg, name, true
}

func (f Func) MakeReviewLine() ReviewLine {
	line := ReviewLine{
		LineID: f.ID(),
		Tokens: f.MakeTokens(),
	}
	return line
}

func (f Func) MakeTokens() []ReviewToken {
	tks := []ReviewToken{}
	// prefix with "func" if f isn't embedded in an interface
	if !f.embedded {
		tks = append(tks, ReviewToken{
			HasSuffixSpace: true,
			Kind:           TokenKindKeyword,
			Value:          "func",
		})
	}
	if f.ReceiverType != "" {
		tks = append(tks, ReviewToken{
			Kind:  TokenKindText,
			Value: "(",
		})
		tks = append(tks, parseAndMakeTypeTokens(f.ReceiverType)...)
		tks = append(tks, ReviewToken{
			HasSuffixSpace: true,
			Kind:           TokenKindPunctuation,
			Value:          ")",
		})
	}
	tks = append(tks, ReviewToken{
		Kind:  TokenKindTypeName,
		Value: f.name,
	})
	if len(f.typeParamNames) > 0 {
		tks = append(tks, ReviewToken{
			Kind:  TokenKindPunctuation,
			Value: "[",
		})
		for i, p := range f.typeParamNames {
			if i > 0 {
				tks = append(tks, ReviewToken{
					HasSuffixSpace: true,
					Kind:           TokenKindPunctuation,
					Value:          ",",
				})
			}
			tks = append(tks, ReviewToken{
				HasSuffixSpace: true,
				Kind:           TokenKindMemberName,
				Value:          p,
			})
			tks = append(tks, parseAndMakeTypeTokens(f.typeParamConstraints[i])...)
		}
		tks = append(tks, ReviewToken{
			Kind:  TokenKindPunctuation,
			Value: "]",
		})
	}
	paren := "("
	if len(f.paramNames) == 0 {
		paren += ")"
	}
	tks = append(tks, ReviewToken{
		Kind:  TokenKindPunctuation,
		Value: paren,
	})
	for i, p := range f.paramNames {
		if p != "" {
			tks = append(tks, ReviewToken{
				HasSuffixSpace: true,
				Kind:           TokenKindMemberName,
				Value:          p,
			})
			tks = append(tks, parseAndMakeTypeTokens(f.paramTypes[i])...)
		} else {
			// parameter names are optional
			tks = append(tks, parseAndMakeTypeTokens(f.paramTypes[i])...)
		}
		if i < len(f.paramNames)-1 {
			tks = append(tks, ReviewToken{
				HasSuffixSpace: true,
				Kind:           TokenKindPunctuation,
				Value:          ",",
			})
		}
	}
	if len(f.paramNames) > 0 {
		tks = append(tks, ReviewToken{
			Kind:  TokenKindPunctuation,
			Value: ")",
		})
	}
	if len(f.Returns) > 0 {
		if len(f.Returns) > 1 {
			tks = append(tks, ReviewToken{
				HasPrefixSpace: true,
				Kind:           TokenKindPunctuation,
				Value:          "(",
			})
		} else {
			tks[len(tks)-1].HasSuffixSpace = true
		}
		for i, t := range f.Returns {
			tks = append(tks, parseAndMakeTypeTokens(t)...)
			if i < len(f.Returns)-1 {
				tks = append(tks, ReviewToken{
					HasSuffixSpace: true,
					Kind:           TokenKindPunctuation,
					Value:          ",",
				})
			}
		}
		if len(f.Returns) > 1 {
			tks = append(tks, ReviewToken{
				Kind:  TokenKindPunctuation,
				Value: ")",
			})
		}
	}
	return tks
}

func (f Func) Name() string {
	return f.name
}

var _ TokenMaker = (*Func)(nil)

type Interface struct {
	TokenMaker
	// Sealed indicates whether users can implement the interface i.e. whether it has an unexported method
	Sealed             bool
	embeddedInterfaces []string
	id                 string
	methods            map[string]Func
	name               string
	isAlias            bool
	typeParams         []string
	aliasTarget        string
}

func NewInterface(source Pkg, name, packageName string, ts *ast.TypeSpec, imports map[string]string) Interface {
	in := Interface{
		name:               name,
		embeddedInterfaces: []string{},
		methods:            map[string]Func{},
		id:                 packageName + "." + name,
		typeParams:         typeParameters(source, ts, imports),
	}
	n := unparenType(ts.Type).(*ast.InterfaceType)
	if n.Methods != nil {
		for _, m := range n.Methods.List {
			if len(m.Names) > 0 {
				n := m.Names[0].Name
				if unicode.IsLower(rune(n[0])) {
					in.Sealed = true
				}
				f := NewFuncForInterfaceMethod(source, name, m, imports)
				in.methods[n] = f
			} else {
				n := source.getText(m.Type.Pos(), m.Type.End())
				in.embeddedInterfaces = append(in.embeddedInterfaces, source.translateType(n, imports))
			}
		}
	}
	sort.Strings(in.embeddedInterfaces)
	return in
}

func (i Interface) Exported() bool {
	return unicode.IsUpper(rune(i.name[0]))
}

func (i Interface) ID() string {
	return i.id
}

func (i Interface) MakeReviewLine() ReviewLine {
	interfaceLine := ReviewLine{
		Children: []ReviewLine{},
		LineID:   i.id,
		Tokens: []ReviewToken{
			{
				Kind:  TokenKindKeyword,
				Value: "type",
			},
			{
				HasPrefixSpace:        true,
				HasSuffixSpace:        true,
				Kind:                  TokenKindTypeName,
				NavigationDisplayName: i.id,
				Value:                 i.name,
			},
		},
	}
	if len(i.typeParams) > 0 {
		interfaceLine.Tokens[len(interfaceLine.Tokens)-1].HasSuffixSpace = false
		interfaceLine.Tokens = append(interfaceLine.Tokens, ReviewToken{Kind: TokenKindPunctuation, Value: "["})
		interfaceLine.Tokens = append(interfaceLine.Tokens, parseAndMakeTypeTokens(strings.Join(i.typeParams, ", "))...)
		interfaceLine.Tokens = append(interfaceLine.Tokens, ReviewToken{Kind: TokenKindPunctuation, Value: "]", HasSuffixSpace: true})
	}
	if i.isAlias {
		interfaceLine.Tokens = append(interfaceLine.Tokens, aliasToken())
	}
	interfaceLine.Tokens = append(interfaceLine.Tokens, ReviewToken{Kind: TokenKindKeyword, Value: "interface"})
	if i.aliasTarget != "" {
		interfaceLine.Tokens = append(interfaceLine.Tokens, aliasTargetToken(i.aliasTarget))
	}

	for _, name := range i.embeddedInterfaces {
		if exportedFieldRgx.MatchString(name) {
			// TODO: set NavigateToID to create a navigation link. Need the type's LineID i.e. qualified
			// name but we don't have it here; similar to https://github.com/Azure/azure-sdk-tools/issues/6150
			interfaceLine.Children = append(interfaceLine.Children, ReviewLine{
				Tokens: parseAndMakeTypeTokens(name),
			})
		}
	}

	if len(i.methods) > 0 {
		keys := []string{}
		for k := range i.methods {
			if unicode.IsUpper(rune(k[0])) {
				keys = append(keys, k)
			}
		}
		sort.Strings(keys)
		for _, k := range keys {
			methodLine := ReviewLine{
				LineID: i.id + "-" + k,
				Tokens: i.methods[k].MakeTokens(),
			}
			interfaceLine.Children = append(interfaceLine.Children, methodLine)
		}
	}
	interfaceLine.Children = append(interfaceLine.Children, ReviewLine{})
	return interfaceLine
}

func (i Interface) Name() string {
	return i.name
}

var _ TokenMaker = (*Interface)(nil)

type SimpleType struct {
	id             string
	name           string
	underlyingType string
	typeParams     []string
	isAlias        bool
	aliasTarget    string
}

func NewSimpleType(name, packageName, underlyingType string) SimpleType {
	return SimpleType{id: packageName + "." + name, name: name, underlyingType: underlyingType}
}

func (s SimpleType) Exported() bool {
	return unicode.IsUpper(rune(s.name[0]))
}

func (s SimpleType) ID() string {
	return s.id
}

func (s SimpleType) MakeTokens() []ReviewToken {
	tks := []ReviewToken{
		{
			Kind:  TokenKindKeyword,
			Value: "type",
		},
		{
			HasPrefixSpace:        true,
			HasSuffixSpace:        true,
			Kind:                  TokenKindTypeName,
			NavigationDisplayName: s.id,
			Value:                 s.name,
		},
	}
	if len(s.typeParams) > 0 {
		tks[len(tks)-1].HasSuffixSpace = false
		tks = append(tks, ReviewToken{Kind: TokenKindPunctuation, Value: "["})
		tks = append(tks, parseAndMakeTypeTokens(strings.Join(s.typeParams, ", "))...)
		tks = append(tks, ReviewToken{Kind: TokenKindPunctuation, Value: "]", HasSuffixSpace: true})
	}
	if s.isAlias {
		tks = append(tks, aliasToken())
	}
	tks = append(tks, parseAndMakeTypeTokens(s.underlyingType)...)
	if s.aliasTarget != "" {
		tks = append(tks, aliasTargetToken(s.aliasTarget))
	}
	return tks
}

func (s SimpleType) Name() string {
	return s.name
}

var _ TokenMaker = (*SimpleType)(nil)

type Struct struct {
	AnonymousFields []string
	// fields maps a field's name to the name of its type
	fields map[string]string
	id     string
	name   string
	// typeParams lists the func's type parameters as strings of the form "name constraint"
	typeParams  []string
	pkgName     string
	isAlias     bool
	aliasTarget string
}

func (s Struct) MakeReviewLine() ReviewLine {
	structLine := ReviewLine{
		Children: []ReviewLine{},
		LineID:   s.id,
		Tokens:   s.MakeTokens(),
	}
	for _, field := range s.AnonymousFields {
		if exportedFieldRgx.MatchString(field) {
			structLine.Children = append(structLine.Children, ReviewLine{
				Tokens: []ReviewToken{
					{
						Kind: TokenKindTypeName,
						// TODO: set NavigateToID to create a navigation link. Need the type's LineID i.e. qualified
						// name but we don't have it here https://github.com/Azure/azure-sdk-tools/issues/6150
						Value: field,
					},
				},
			})
		}
	}
	exported := []string{}
	maxLen := 0
	for name := range s.fields {
		if exportedFieldRgx.MatchString(name) {
			if len(name) > maxLen {
				maxLen = len(name)
			}
			exported = append(exported, name)
		}
	}
	if len(exported) > 0 {
		sort.Strings(exported)
		for _, name := range exported {
			fieldLine := ReviewLine{
				LineID: s.id + "-" + name,
				Tokens: []ReviewToken{
					{
						Kind:  TokenKindText,
						Value: name,
					},
					{
						Kind:     TokenKindText,
						SkipDiff: true,
						// The +2 aligns type names with and without a preceding *. It's hacky
						// but much simpler than considering * when calculating the max length
						Value: strings.Repeat(" ", maxLen-len(name)+2),
					},
				},
			}
			typeTks := parseAndMakeTypeTokens(s.fields[name])
			fieldLine.Tokens = append(fieldLine.Tokens, typeTks...)
			structLine.Children = append(structLine.Children, fieldLine)
		}
	}
	return structLine
}

func NewStruct(source Pkg, name, packageName string, ts *ast.TypeSpec, imports map[string]string) Struct {
	s := Struct{name: name, id: packageName + "." + name, pkgName: source.Name(), typeParams: typeParameters(source, ts, imports)}
	source.translateFieldList(unparenType(ts.Type).(*ast.StructType).Fields.List, func(n *string, t string) {
		if n == nil {
			s.AnonymousFields = append(s.AnonymousFields, t)
		} else {
			if s.fields == nil {
				s.fields = map[string]string{}
			}
			s.fields[*n] = source.translateType(t, imports)
		}
	})
	sort.Strings(s.AnonymousFields)
	return s
}

func typeParameters(source Pkg, ts *ast.TypeSpec, imports map[string]string) []string {
	var params []string
	if ts.TypeParams != nil {
		source.translateFieldList(ts.TypeParams.List, func(param *string, constraint string) {
			params = append(params, strings.TrimRight(*param+" "+source.translateType(constraint, imports), " "))
		})
	}
	return params
}

func aliasToken() ReviewToken {
	return ReviewToken{Kind: TokenKindPunctuation, Value: "=", HasSuffixSpace: true}
}

func aliasTargetToken(target string) ReviewToken {
	return ReviewToken{Kind: TokenKindComment, Value: "/* alias of " + target + " */", HasPrefixSpace: true}
}

func (s Struct) Exported() bool {
	return unicode.IsUpper(rune(s.name[0]))
}

func (s Struct) ID() string {
	return s.id
}

func (s Struct) MakeTokens() []ReviewToken {
	rts := []ReviewToken{
		{
			HasSuffixSpace: true,
			Kind:           TokenKindKeyword,
			Value:          "type",
		},
		{
			Kind:                  TokenKindTypeName,
			NavigationDisplayName: s.id,
			Value:                 s.name,
		},
	}
	if len(s.typeParams) > 0 {
		rts = append(rts, ReviewToken{Kind: TokenKindPunctuation, Value: "["})
		rts = append(rts, ReviewToken{Kind: TokenKindTypeName, Value: strings.Join(s.typeParams, ", ")})
		rts = append(rts, ReviewToken{Kind: TokenKindPunctuation, Value: "]"})
	}
	if s.isAlias {
		token := aliasToken()
		token.HasPrefixSpace = true
		token.HasSuffixSpace = false
		rts = append(rts, token)
	}
	rts = append(rts, ReviewToken{
		HasPrefixSpace: true,
		Kind:           TokenKindKeyword,
		Value:          "struct",
	})
	if s.aliasTarget != "" {
		rts = append(rts, aliasTargetToken(s.aliasTarget))
	}
	return rts
}

func (s Struct) Name() string {
	return s.name
}

var _ TokenMaker = (*Struct)(nil)

func parseAndMakeTypeTokens(val string) []ReviewToken {
	toks := []ReviewToken{}
	now := ""
	for _, r := range val {
		switch s := string(r); s {
		case "*", "[", "]", " ", "\t", "\r", "\n", ";", "(", ")", "{", "}", ",":
			if now != "" {
				toks = append(toks, makeTypeSectionToken(now))
				now = ""
			}
			if (s == " " || s == "\t" || s == "\r" || s == "\n") && len(toks) > 0 {
				toks[len(toks)-1].HasSuffixSpace = true
			} else {
				toks = append(toks, ReviewToken{Kind: TokenKindPunctuation, Value: s})
			}
		case ".":
			if now == ".." {
				toks = append(toks, ReviewToken{Kind: TokenKindPunctuation, Value: "..."})
				now = ""
			} else {
				now += "."
			}
		default:
			now += s
		}
	}
	if now != "" {
		toks = append(toks, makeTypeSectionToken(now))
	}
	return toks
}

var keywords = []string{"interface", "map", "any", "func"}
var internalTypes = []string{"bool", "uint8", "uint16", "uint32", "uint64", "uint", "int8", "int16", "int32", "int64", "int", "float32", "float64", "complex64", "complex128", "byte", "rune", "string", "error", "uintptr", "nil"}

func makeTypeSectionToken(section string) ReviewToken {
	switch {
	case slices.Contains(keywords, section):
		return ReviewToken{Kind: TokenKindKeyword, Value: section}
	case slices.Contains(internalTypes, section):
		return ReviewToken{Kind: TokenKindTypeName, Value: section}
	default:
		if strings.HasPrefix(section, "<") && strings.Contains(section, ">") {
			splits := strings.Split(section[1:], ">")
			return ReviewToken{Kind: TokenKindTypeName, NavigateToID: splits[0], Value: splits[1]}
		}
		return ReviewToken{Kind: TokenKindTypeName, Value: section}
	}
}
