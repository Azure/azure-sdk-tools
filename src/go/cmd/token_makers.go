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
)

// exportedFieldRgx matches exported field names like "policy.ClientOptions", "Transport", and "GetToken(...)"
var exportedFieldRgx = regexp.MustCompile(`^(?:[a-zA-Z]*\.)?[A-Z]+[a-zA-Z]*`)

type TokenMaker interface {
	Exported() bool
	ID() string
	MakeTokens() []Token
	Name() string
}

// Declaration is a const or var declaration.
type Declaration struct {
	Type string

	id    string
	name  string
	value string
}

func NewDeclaration(pkg Pkg, vs *ast.ValueSpec) Declaration {
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
			decl.Type = x.Name
		case *ast.MapType:
			// var nullables map[reflect.Type]interface{} = map[reflect.Type]interface{}{}
			decl.Type = pkg.getText(vs.Type.Pos(), vs.Type.End())
		case *ast.SelectorExpr:
			// const LogCredential log.Classification = "Credential"
			decl.Type = x.Sel.Name
		case *ast.StarExpr:
			// var defaultHTTPClient *http.Client
			decl.Type = x.X.(*ast.SelectorExpr).Sel.Name
		default:
			fmt.Println("unhandled constant type " + pkg.getText(vs.Type.Pos(), vs.Type.End()))
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
			decl.Type = pkg.getText(t.Type.Pos(), t.Type.End())
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

func (d Declaration) MakeTokens() []Token {
	list := &[]Token{}
	ID := d.ID()
	makeToken(&ID, nil, d.Name(), TokenTypeTypeName, list)
	makeToken(nil, nil, " ", TokenTypeWhitespace, list)
	if d.Type != skip {
		makeToken(nil, nil, d.Type, TokenTypeMemberName, list)
	}
	makeToken(nil, nil, " ", TokenTypeWhitespace, list)
	makeToken(nil, nil, "=", TokenTypePunctuation, list)
	makeToken(nil, nil, " ", TokenTypeWhitespace, list)
	makeToken(nil, nil, d.value, TokenTypeStringLiteral, list)
	makeToken(nil, nil, "", TokenTypeNewline, list)
	return *list
}

func (d Declaration) Name() string {
	return d.name
}

type Func struct {
	Receiver string
	// Returns lists the func's return types
	Returns []string

	embedded bool
	exported bool
	id       string
	// name includes the function's receiver, if any
	name string
	// params lists the func's parameters as strings of the form "name type"
	params []string
	// typeParams lists the func's type parameters as strings of the form "name constraint"
	typeParams []string
}

func NewFunc(pkg Pkg, f *ast.FuncDecl) Func {
	fn := newFunc(pkg, f.Type)
	fn.name = f.Name.Name
	sig := ""
	if f.Recv != nil {
		typ := pkg.getText(f.Recv.List[0].Type.Pos(), f.Recv.List[0].Type.End())
		name := ""
		if len(f.Recv.List[0].Names) != 0 {
			name = f.Recv.List[0].Names[0].Name + " "
		}
		fn.Receiver = name + typ
		sig = fmt.Sprintf("(%s) ", fn.Receiver)
	}
	fn.exported = !isOnUnexportedMember(sig) && unicode.IsUpper(rune(fn.name[0]))
	sig += f.Name.Name
	fn.id = pkg.Name() + "-" + sig
	return fn
}

func NewFuncForInterfaceMethod(pkg Pkg, interfaceName string, f *ast.Field) Func {
	fn := newFunc(pkg, f.Type.(*ast.FuncType))
	fn.name = f.Names[0].Name
	fn.exported = unicode.IsUpper(rune(fn.name[0]))
	fn.id = pkg.Name() + "-" + interfaceName + "-" + fn.name
	fn.embedded = true
	return fn
}

func newFunc(pkg Pkg, f *ast.FuncType) Func {
	fn := Func{}
	if f.TypeParams != nil {
		fn.typeParams = make([]string, 0, len(f.TypeParams.List))
		pkg.translateFieldList(f.TypeParams.List, func(param *string, constraint string) {
			// constraint = "" when the parameter is unconstrained
			fn.typeParams = append(fn.typeParams, strings.TrimRight(*param+" "+constraint, " "))
		})
	}
	if f.Params.List != nil {
		fn.params = make([]string, 0, len(f.Params.List))
		pkg.translateFieldList(f.Params.List, func(n *string, t string) {
			p := ""
			if n != nil {
				p = *n + " "
			}
			p += t
			fn.params = append(fn.params, p)
		})
	}
	if f.Results != nil {
		fn.Returns = make([]string, 0, len(f.Results.List))
		pkg.translateFieldList(f.Results.List, func(n *string, t string) {
			fn.Returns = append(fn.Returns, t)
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

func (f Func) MakeTokens() []Token {
	list := &[]Token{}
	if f.embedded {
		makeToken(nil, nil, "\t", TokenTypeWhitespace, list)
	} else {
		makeToken(nil, nil, "func", TokenTypeKeyword, list)
		makeToken(nil, nil, " ", TokenTypeWhitespace, list)
	}
	if f.Receiver != "" {
		makeToken(nil, nil, "(", TokenTypePunctuation, list)
		makeToken(nil, nil, f.Receiver, TokenTypeMemberName, list)
		makeToken(nil, nil, ")", TokenTypePunctuation, list)
		makeToken(nil, nil, " ", TokenTypeWhitespace, list)
	}
	ID := f.ID()
	makeToken(&ID, nil, f.name, TokenTypeTypeName, list)
	if len(f.typeParams) > 0 {
		makeToken(nil, nil, "[", TokenTypePunctuation, list)
		makeToken(nil, nil, strings.Join(f.typeParams, ", "), TokenTypeMemberName, list)
		makeToken(nil, nil, "]", TokenTypePunctuation, list)
	}
	makeToken(nil, nil, "(", TokenTypePunctuation, list)
	for i, p := range f.params {
		if name, typ, found := strings.Cut(p, " "); found {
			makeToken(nil, nil, name, TokenTypeMemberName, list)
			makeToken(nil, nil, " ", TokenTypeWhitespace, list)
			makeToken(nil, nil, typ, TokenTypeTypeName, list)
		} else {
			// parameter names are optional
			makeToken(nil, nil, p, TokenTypeTypeName, list)
		}
		if i < len(f.params)-1 {
			makeToken(nil, nil, ",", TokenTypePunctuation, list)
			makeToken(nil, nil, " ", TokenTypeWhitespace, list)
		}
	}
	makeToken(nil, nil, ")", TokenTypePunctuation, list)
	if len(f.Returns) > 0 {
		makeToken(nil, nil, " ", TokenTypeWhitespace, list)
		if len(f.Returns) > 1 {
			makeToken(nil, nil, "(", TokenTypePunctuation, list)
		}
		for i, t := range f.Returns {
			makeToken(nil, nil, t, TokenTypeMemberName, list)
			if i < len(f.Returns)-1 {
				makeToken(nil, nil, ",", TokenTypePunctuation, list)
				makeToken(nil, nil, " ", TokenTypeWhitespace, list)
			}
		}
		if len(f.Returns) > 1 {
			makeToken(nil, nil, ")", TokenTypePunctuation, list)
		}
	}
	if !f.embedded {
		makeToken(nil, nil, "", TokenTypeNewline, list)
	}
	makeToken(nil, nil, "", TokenTypeNewline, list)
	return *list
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

func NewInterface(source Pkg, name, packageName string, n *ast.InterfaceType) Interface {
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
				f := NewFuncForInterfaceMethod(source, name, m)
				in.methods[n] = f
			} else {
				n := source.getText(m.Type.Pos(), m.Type.End())
				in.embeddedInterfaces = append(in.embeddedInterfaces, n)
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

func (i Interface) MakeTokens() []Token {
	ID := i.id
	list := &[]Token{}
	makeToken(nil, nil, "type", TokenTypeKeyword, list)
	makeToken(nil, nil, " ", TokenTypeWhitespace, list)
	makeToken(&ID, nil, i.name, TokenTypeTypeName, list)
	makeToken(nil, nil, " ", TokenTypeWhitespace, list)
	makeToken(nil, nil, "interface", TokenTypeKeyword, list)
	makeToken(nil, nil, " ", TokenTypeWhitespace, list)
	makeToken(nil, nil, "{", TokenTypePunctuation, list)
	for _, name := range i.embeddedInterfaces {
		if exportedFieldRgx.MatchString(name) {
			defID := ID + "-" + name
			makeToken(nil, nil, "", TokenTypeNewline, list)
			makeToken(nil, nil, "\t", TokenTypeWhitespace, list)
			makeToken(&defID, nil, name, TokenTypeTypeName, list)
		}
	}
	if len(i.methods) > 0 {
		makeToken(nil, nil, "", TokenTypeNewline, list)
		keys := []string{}
		for k := range i.methods {
			if unicode.IsUpper(rune(k[0])) {
				keys = append(keys, k)
			}
		}
		sort.Strings(keys)
		for _, k := range keys {
			*list = append(*list, i.methods[k].MakeTokens()...)
		}
	}
	makeToken(nil, nil, "}", TokenTypePunctuation, list)
	makeToken(nil, nil, "", TokenTypeNewline, list)
	makeToken(nil, nil, "", TokenTypeNewline, list)
	return *list
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

func NewSimpleType(source Pkg, name, packageName, underlyingType string) SimpleType {
	return SimpleType{id: packageName + "." + name, name: name, underlyingType: underlyingType}
}

func (s SimpleType) Exported() bool {
	return unicode.IsUpper(rune(s.name[0]))
}

func (s SimpleType) ID() string {
	return s.id
}

func (s SimpleType) MakeTokens() []Token {
	tokenList := &[]Token{}
	ID := s.id
	makeToken(nil, nil, "type", TokenTypeKeyword, tokenList)
	makeToken(nil, nil, " ", TokenTypeWhitespace, tokenList)
	makeToken(&ID, nil, s.name, TokenTypeTypeName, tokenList)
	makeToken(nil, nil, " ", TokenTypeWhitespace, tokenList)
	makeToken(nil, nil, s.underlyingType, TokenTypeText, tokenList)
	makeToken(nil, nil, "", TokenTypeNewline, tokenList)
	makeToken(nil, nil, "", TokenTypeNewline, tokenList)
	return *tokenList
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
}

func NewStruct(source Pkg, name, packageName string, ts *ast.TypeSpec) Struct {
	s := Struct{name: name, id: packageName + "." + name}
	if ts.TypeParams != nil {
		s.typeParams = make([]string, 0, len(ts.TypeParams.List))
		source.translateFieldList(ts.TypeParams.List, func(param *string, constraint string) {
			s.typeParams = append(s.typeParams, strings.TrimRight(*param+" "+constraint, " "))
		})
	}
	source.translateFieldList(ts.Type.(*ast.StructType).Fields.List, func(n *string, t string) {
		if n == nil {
			s.AnonymousFields = append(s.AnonymousFields, t)
		} else {
			if s.fields == nil {
				s.fields = map[string]string{}
			}
			s.fields[*n] = t
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

func (s Struct) MakeTokens() []Token {
	list := &[]Token{}
	ID := s.id
	makeToken(nil, nil, "type", TokenTypeKeyword, list)
	makeToken(nil, nil, " ", TokenTypeWhitespace, list)
	makeToken(&ID, nil, s.name, TokenTypeTypeName, list)
	if len(s.typeParams) > 0 {
		makeToken(nil, nil, "[", TokenTypePunctuation, list)
		makeToken(nil, nil, strings.Join(s.typeParams, ", "), TokenTypeMemberName, list)
		makeToken(nil, nil, "]", TokenTypePunctuation, list)
	}
	makeToken(nil, nil, " ", TokenTypeWhitespace, list)
	makeToken(nil, nil, "struct", TokenTypeKeyword, list)
	makeToken(nil, nil, " ", TokenTypeWhitespace, list)
	makeToken(nil, nil, "{", TokenTypePunctuation, list)
	exportedFields := false
	for _, name := range s.AnonymousFields {
		if exportedFieldRgx.MatchString(name) {
			defID := name + "-" + s.id
			makeToken(nil, nil, "", TokenTypeNewline, list)
			makeToken(nil, nil, "\t", TokenTypeWhitespace, list)
			makeToken(&defID, nil, name, TokenTypeTypeName, list)
			exportedFields = true
		}
	}
	keys := make([]string, 0, len(s.fields))
	for k := range s.fields {
		if exportedFieldRgx.MatchString(k) {
			keys = append(keys, k)
		}
	}
	sort.Strings(keys)
	for _, field := range keys {
		typ := s.fields[field]
		defID := field + "-" + s.id
		makeToken(nil, nil, "", TokenTypeNewline, list)
		makeToken(nil, nil, "\t", TokenTypeWhitespace, list)
		makeToken(&defID, nil, field, TokenTypeTypeName, list)
		makeToken(nil, nil, " ", TokenTypeWhitespace, list)
		makeToken(nil, nil, typ, TokenTypeMemberName, list)
		exportedFields = true
	}
	if exportedFields {
		makeToken(nil, nil, "", TokenTypeNewline, list)
	}
	makeToken(nil, nil, "}", TokenTypePunctuation, list)
	makeToken(nil, nil, "", TokenTypeNewline, list)
	makeToken(nil, nil, "", TokenTypeNewline, list)
	return *list
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
func makeToken(defID, navID *string, val string, kind TokenType, list *[]Token) {
	tok := Token{DefinitionID: defID, NavigateToID: navID, Value: val, Kind: kind}
	*list = append(*list, tok)
}
