// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

package cmd

import (
	"fmt"
	"go/ast"
	"regexp"
	"sort"
	"strings"
	"unicode"

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
	paramTypes []string
	// typeParamNames lists the func's type parameters name
	typeParamNames []string
	// typeParamConstraints lists the func's type parameters constraint
	typeParamConstraints []string
}

func NewFunc(pkg Pkg, f *ast.FuncDecl, imports map[string]string) Func {
	fn := newFunc(pkg, f.Type, imports)
	fn.name = f.Name.Name
	sig := ""
	if f.Recv != nil {
		fn.ReceiverType = pkg.getText(f.Recv.List[0].Type.Pos(), f.Recv.List[0].Type.End())
		if len(f.Recv.List[0].Names) != 0 {
			fn.ReceiverName = f.Recv.List[0].Names[0].Name
		}
		if fn.ReceiverName != "" {
			sig = fmt.Sprintf("(%s %s) ", fn.ReceiverName, fn.ReceiverType)
		} else {
			sig = fmt.Sprintf("(%s) ", fn.ReceiverType)
		}
	}
	fn.exported = !isOnUnexportedMember(sig) && unicode.IsUpper(rune(fn.name[0]))
	sig += f.Name.Name
	fn.id = pkg.Name() + "-" + sig
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
	fn := Func{}
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
		pkg.translateFieldList(f.Params.List, func(n *string, t string) {
			if n != nil {
				fn.paramNames = append(fn.paramNames, *n)
			} else {
				fn.paramNames = append(fn.paramNames, "")
			}
			fn.paramTypes = append(fn.paramTypes, pkg.translateType(t, imports))
		})
	}
	if f.Results != nil {
		fn.Returns = make([]string, 0, len(f.Results.List))
		pkg.translateFieldList(f.Results.List, func(n *string, t string) {
			fn.Returns = append(fn.Returns, pkg.translateType(t, imports))
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

func (f Func) ForAlias(pkg string) Func {
	clone := f
	// replace everything to the left of - with the new package name
	i := strings.Index(clone.id, "-")
	if i < 0 {
		panic("missing sig separator in id")
	}
	clone.id = pkg + clone.id[i:]
	return clone
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
		Kind: TokenKindTypeName,
		// TODO: is this necessary?
		// NavigationDisplayName: f.name,
		// TODO: what should this value be? LineID of the target?
		// NavigateToID:          f.ID(),
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
				Kind:  TokenKindMemberName,
				Value: p,
			})
			tks = append(tks, parseAndMakeTypeTokens(f.typeParamConstraints[i])...)
		}
		tks = append(tks, ReviewToken{
			Kind:  TokenKindPunctuation,
			Value: "]",
		})
	}
	tks = append(tks, ReviewToken{
		Kind:  TokenKindPunctuation,
		Value: "(",
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
	tks = append(tks, ReviewToken{
		HasSuffixSpace: true,
		Kind:           TokenKindPunctuation,
		Value:          ")",
	})
	if len(f.Returns) > 0 {
		if len(f.Returns) > 1 {
			tks = append(tks, ReviewToken{
				HasPrefixSpace: true,
				Kind:           TokenKindPunctuation,
				Value:          "(",
			})
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
	if !f.embedded {
		// TODO: newline?
	}
	// TODO: newline?
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
}

func NewInterface(source Pkg, name, packageName string, n *ast.InterfaceType, imports map[string]string) Interface {
	in := Interface{
		name:               name,
		embeddedInterfaces: []string{},
		methods:            map[string]Func{},
		id:                 packageName + "." + name,
	}
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
			{
				Kind:  TokenKindKeyword,
				Value: "interface",
			},
		},
	}

	for _, name := range i.embeddedInterfaces {
		if exportedFieldRgx.MatchString(name) {
			// TODO: navigation link
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
	tks = append(tks, parseAndMakeTypeTokens(s.underlyingType)...)
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
	typeParams []string
	pkgName    string
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
				// TODO: navigation link
				Tokens: []ReviewToken{
					{
						Kind:  TokenKindTypeName,
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
				maxLen = len(name) + 1
			}
			exported = append(exported, name)
		}
	}
	if len(exported) > 0 {
		sort.Strings(exported)
		for _, name := range exported {
			fieldLine := ReviewLine{
				LineID: s.id + "-" + name,
				// TODO: semantics; is this just for e.g. docs?
				RelatedToLine: structLine.LineID,
				Tokens: []ReviewToken{
					{
						Kind:  TokenKindText,
						Value: name,
					},
					{
						Kind:  TokenKindPunctuation,
						Value: strings.Repeat(" ", maxLen-len(name)),
					},
				},
			}
			typeTks := parseAndMakeTypeTokens(s.fields[name])
			fieldLine.Tokens = append(fieldLine.Tokens, typeTks...)
			structLine.Children = append(structLine.Children, fieldLine)
		}
	}
	// add a blank line after fields
	if len(structLine.Children) > 0 {
		structLine.Children = append(structLine.Children, ReviewLine{})
	}
	return structLine
}

func NewStruct(source Pkg, name, packageName string, ts *ast.TypeSpec, imports map[string]string) Struct {
	s := Struct{name: name, id: packageName + "." + name, pkgName: source.Name()}
	if ts.TypeParams != nil {
		s.typeParams = make([]string, 0, len(ts.TypeParams.List))
		source.translateFieldList(ts.TypeParams.List, func(param *string, constraint string) {
			s.typeParams = append(s.typeParams, strings.TrimRight(*param+" "+source.translateType(constraint, imports), " "))
		})
	}
	source.translateFieldList(ts.Type.(*ast.StructType).Fields.List, func(n *string, t string) {
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
	rts = append(rts, ReviewToken{
		HasPrefixSpace: true,
		Kind:           TokenKindKeyword,
		Value:          "struct",
	})
	return rts
}

func (s Struct) Name() string {
	return s.name
}

var _ TokenMaker = (*Struct)(nil)

// makeToken builds the Token to be added to the Token slice that is passed in as a parameter.
// defID and navID components can be passed in as nil to indicate that there is no definition ID or
// navigation ID that is related to that token.
// val is the value of the token and it was will be visible in the API view tool.
// kind is the TokenType that will be assigned to the value and will determine how the value is
// represented in the API view tool.
// list is the slice of tokens that will be parsed in the API view tool, the new token will be appended to list.
// TODO improve makeToken and make more similar to append
func makeToken(defID, navID *string, val string, kind TokenKind, list *[]ReviewToken) {
	tok := ReviewToken{Value: val, Kind: kind}
	if navID != nil {
		tok.NavigateToID = *navID
	}
	*list = append(*list, tok)
}

// TODO: this makes more tokens than necessary
func parseAndMakeTypeTokens(val string) []ReviewToken {
	toks := []ReviewToken{}
	now := ""
	for _, r := range val {
		switch s := string(r); s {
		case "*", "[", "]", " ", "(", ")", "{", "}", ",":
			if now != "" {
				toks = append(toks, makeTypeSectionToken(now))
				now = ""
			}
			if s == " " && len(toks) > 0 {
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

func parseAndMakeTypeToken_old(val string, list *[]ReviewToken) {
	now := ""
	for _, ch := range val {
		switch string(ch) {
		case "*", "[", "]", " ", "(", ")", "{", "}", ",":
			if now != "" {
				makeTypeSectionToken_old(now, list)
				now = ""
			}
			if string(ch) == " " {
				makeToken(nil, nil, " ", TokenKindText, list)
			} else {
				makeToken(nil, nil, string(ch), TokenKindPunctuation, list)
			}
		case ".":
			if now == ".." {
				makeToken(nil, nil, "...", TokenKindPunctuation, list)
				now = ""
			} else {
				now = now + "."
			}
		default:
			now = now + string(ch)
		}
	}
	if now != "" {
		makeTypeSectionToken_old(now, list)
	}
}

var keywords = []string{"interface", "map", "any", "func"}
var internalTypes = []string{"bool", "uint8", "uint16", "uint32", "uint64", "uint", "int8", "int16", "int32", "int64", "int", "float32", "float64", "complex64", "complex128", "byte", "rune", "string", "error", "uintptr", "nil"}

func makeTypeSectionToken_old(section string, list *[]ReviewToken) {
	switch {
	case slices.Contains(keywords, section):
		makeToken(nil, nil, section, TokenKindKeyword, list)
	case slices.Contains(internalTypes, section):
		makeToken(nil, nil, section, TokenKindTypeName, list)
	default:
		if strings.HasPrefix(section, "<") {
			splits := strings.Split(section[1:], ">")
			makeToken(nil, &splits[0], splits[1], TokenKindTypeName, list)
		} else {
			makeToken(nil, nil, section, TokenKindTypeName, list)
		}
	}
}

func makeTypeSectionToken(section string) ReviewToken {
	switch {
	case slices.Contains(keywords, section):
		return ReviewToken{Kind: TokenKindKeyword, Value: section}
	case slices.Contains(internalTypes, section):
		return ReviewToken{Kind: TokenKindTypeName, Value: section}
	default:
		if strings.HasPrefix(section, "<") {
			splits := strings.Split(section[1:], ">")
			return ReviewToken{Kind: TokenKindTypeName, NavigateToID: splits[0], Value: splits[1]}
		}
		return ReviewToken{Kind: TokenKindTypeName, Value: section}
	}
}
