// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

package cmd

import (
	"sort"
	"strings"
)

var reservedNames = map[string]struct{}{
	"string":     {},
	"byte":       {},
	"int":        {},
	"int8":       {},
	"int16":      {},
	"int32":      {},
	"int64":      {},
	"float32":    {},
	"float64":    {},
	"rune":       {},
	"bool":       {},
	"map":        {},
	"uint":       {},
	"uint8":      {},
	"uint16":     {},
	"uint32":     {},
	"uint64":     {},
	"complex64":  {},
	"complex128": {},
	"error":      {},
}

type TokenType int

// these values are determined by APIView
const (
	text          TokenType = 0
	newline       TokenType = 1
	whitespace    TokenType = 2
	punctuation   TokenType = 3
	keyword       TokenType = 6
	lineIDMarker  TokenType = 5
	typeName      TokenType = 4
	memberName    TokenType = 7
	stringLiteral TokenType = 8
	literal       TokenType = 9
	comment       TokenType = 10
)

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

func makeStructTokens(name *string, anonFields []string, fields map[string]string, list *[]Token) {
	n := *name
	makeToken(nil, nil, "type", keyword, list)
	makeToken(nil, nil, " ", whitespace, list)
	makeToken(&n, &n, *name, typeName, list)
	makeToken(nil, nil, " ", whitespace, list)
	makeToken(nil, nil, "struct", keyword, list)
	makeToken(nil, nil, " ", whitespace, list)
	makeToken(nil, nil, "{", punctuation, list)
	if anonFields == nil && fields == nil {
		makeToken(nil, nil, "", newline, list)
		makeToken(nil, nil, "\t", whitespace, list)
		makeToken(nil, nil, "// no exported fields", comment, list)
	} else {
		sort.Strings(anonFields)
		for _, v1 := range anonFields {
			v := v1 + "-" + *name
			makeToken(nil, nil, "", newline, list)
			makeToken(nil, nil, "\t", whitespace, list)
			makeToken(&v, nil, v1, typeName, list)
		}
		keys := []string{}
		for k := range fields {
			keys = append(keys, k)
		}
		sort.Strings(keys)
		for _, field := range keys {
			typ := fields[field]
			defID := field + "-" + *name
			makeToken(nil, nil, "", newline, list)
			makeToken(nil, nil, "\t", whitespace, list)
			makeToken(&defID, nil, field, typeName, list)
			makeToken(nil, nil, " ", whitespace, list)
			makeToken(nil, nil, typ, memberName, list)
		}
	}
	makeToken(nil, nil, "", newline, list)
	makeToken(nil, nil, "}", punctuation, list)
	makeToken(nil, nil, "", newline, list)
	makeToken(nil, nil, "", newline, list)
}

func makeInterfaceTokens(name *string, embeddedInterfaces []string, methods map[string]Func, list *[]Token) {
	n := *name
	makeToken(nil, nil, "type", keyword, list)
	makeToken(nil, nil, " ", whitespace, list)
	makeToken(&n, &n, *name, typeName, list)
	makeToken(nil, nil, " ", whitespace, list)
	makeToken(nil, nil, "interface", keyword, list)
	makeToken(nil, nil, " ", whitespace, list)
	makeToken(nil, nil, "{", punctuation, list)
	makeToken(nil, nil, "", newline, list)
	for _, v1 := range embeddedInterfaces {
		v := v1 + "-" + n
		makeToken(nil, nil, "\t", whitespace, list)
		makeToken(&v, nil, v1, typeName, list)
		makeToken(nil, nil, "", newline, list)
	}
	if len(methods) > 0 {
		keys := []string{}
		for k1 := range methods {
			keys = append(keys, k1)
		}
		sort.Strings(keys)
		for _, k1 := range keys {
			makeToken(nil, nil, "\t", whitespace, list)
			makeIntMethodTokens(&k1, methods[k1].Params, methods[k1].Returns, list)
			makeToken(nil, nil, "", newline, list)
		}
	}
	makeToken(nil, nil, "}", punctuation, list)
	makeToken(nil, nil, "", newline, list)
	makeToken(nil, nil, "", newline, list)
}

func makeFuncTokens(name *string, params, returns *string, returnCount int, list *[]Token) {
	if isOnUnexportedMember(*name) || isExampleOrTest(*name) {
		return
	}
	n := *name
	makeToken(nil, nil, "func", keyword, list)
	makeToken(nil, nil, " ", whitespace, list)
	makeToken(&n, nil, *name, typeName, list)
	makeToken(nil, nil, "(", punctuation, list)
	if params != nil {
		p := *params
		tok := strings.Split(p, ",")
		for id, i := range tok {
			temp := strings.Split(i, " ")
			makeToken(nil, nil, temp[0], typeName, list)
			makeToken(nil, nil, " ", whitespace, list)
			makeToken(nil, nil, temp[1], memberName, list)
			if id < len(tok)-1 {
				makeToken(nil, nil, ",", punctuation, list)
				makeToken(nil, nil, " ", whitespace, list)
			}
		}
	}
	makeToken(nil, nil, ")", punctuation, list)
	if returns != nil {
		makeToken(nil, nil, " ", whitespace, list)
		r := *returns
		tok := strings.Split(r, ",")
		if len(tok) > 1 {
			makeToken(nil, nil, "(", punctuation, list)
		}
		for id, i := range tok {
			temp := strings.Split(i, " ")
			makeToken(nil, nil, temp[0], memberName, list)
			if id < len(tok)-1 {
				makeToken(nil, nil, ",", punctuation, list)
				makeToken(nil, nil, " ", whitespace, list)
			}
		}
		if len(tok) > 1 {
			makeToken(nil, nil, ")", punctuation, list)
		}
	}
	makeToken(nil, nil, "", newline, list)
	makeToken(nil, nil, "", newline, list)
}

func makeMethodTokens(receiverVar, receiver string, isPointer bool, name string, params, returns *string, returnCount int, list *[]Token) {
	if isOnUnexportedMember(name) || isExampleOrTest(name) {
		return
	}
	makeToken(nil, nil, "func", keyword, list)
	makeToken(nil, nil, " ", whitespace, list)
	makeToken(nil, nil, "(", punctuation, list)
	if receiverVar != "" {
		makeToken(nil, nil, receiverVar, memberName, list)
		makeToken(nil, nil, " ", whitespace, list)
	}
	if isPointer {
		makeToken(nil, nil, "*", memberName, list)
	}
	makeToken(nil, nil, receiver, memberName, list)
	makeToken(nil, nil, ")", punctuation, list)
	makeToken(nil, nil, " ", whitespace, list)
	defID := name + "-" + receiver
	makeToken(&defID, nil, name, typeName, list)
	makeToken(nil, nil, "(", punctuation, list)
	if params != nil {
		p := *params
		tok := strings.Split(p, ",")
		for id, i := range tok {
			temp := strings.Split(i, " ")
			makeToken(nil, nil, temp[0], typeName, list)
			if len(temp) == 2 {
				makeToken(nil, nil, " ", whitespace, list)
				makeToken(nil, nil, temp[1], getTypeClassification(temp[1]), list)
			}
			if id < len(tok)-1 {
				makeToken(nil, nil, ",", punctuation, list)
				makeToken(nil, nil, " ", whitespace, list)
			}
		}
	}
	makeToken(nil, nil, ")", punctuation, list)
	makeToken(nil, nil, " ", whitespace, list)
	if returns != nil {
		r := *returns
		tok := strings.Split(r, ",")
		if len(tok) > 1 {
			makeToken(nil, nil, "(", punctuation, list)
		}
		for id, i := range tok {
			temp := strings.Split(i, " ")
			makeToken(nil, nil, temp[0], memberName, list)
			if id < len(tok)-1 {
				makeToken(nil, nil, ",", punctuation, list)
				makeToken(nil, nil, " ", whitespace, list)
			}
		}
		if len(tok) > 1 {
			makeToken(nil, nil, ")", punctuation, list)
		}
	}
	makeToken(nil, nil, "", newline, list)
	makeToken(nil, nil, "", newline, list)
}

// interface method definitions vary from regular method definitions slightly so they have their own independent generation function
func makeIntMethodTokens(name *string, params, results *string, list *[]Token) {
	n := *name
	makeToken(&n, nil, *name, typeName, list)
	makeToken(nil, nil, "(", punctuation, list)
	if params != nil {
		p := *params
		tok := strings.Split(p, ",")
		for id, i := range tok {
			temp := strings.Split(i, " ")
			tokenType := typeName
			if len(temp) == 2 {
				tokenType = getTypeClassification(temp[0])
			}
			makeToken(nil, nil, temp[0], tokenType, list)
			if len(temp) == 2 {
				makeToken(nil, nil, " ", whitespace, list)
				makeToken(nil, nil, temp[1], getTypeClassification(temp[1]), list)
			}
			if id < len(tok)-1 {
				makeToken(nil, nil, ",", punctuation, list)
				makeToken(nil, nil, " ", whitespace, list)
			}

		}
	}
	makeToken(nil, nil, ")", punctuation, list)
	makeToken(nil, nil, " ", whitespace, list)
	if results != nil && len(*results) > 0 {
		r := *results
		tok := strings.Split(r, ",")
		if len(tok) > 1 {
			makeToken(nil, nil, "(", punctuation, list)
		}
		for id, i := range tok {
			temp := strings.Split(i, " ")
			makeToken(nil, nil, temp[0], getTypeClassification(temp[0]), list)
			if id < len(tok)-1 {
				makeToken(nil, nil, ",", punctuation, list)
				makeToken(nil, nil, " ", whitespace, list)
			}

		}
		if len(tok) > 1 {
			makeToken(nil, nil, ")", punctuation, list)
		}
	}
}

// TODO can improve how BinaryExpr consts are represented with different colors
func makeConstTokens(name *string, c Const, list *[]Token) {
	n := *name
	makeToken(nil, nil, "\t", whitespace, list)
	makeToken(&n, nil, *name, typeName, list)
	makeToken(nil, nil, " ", whitespace, list)
	if c.Type != skip {
		makeToken(nil, nil, c.Type, memberName, list)
	}
	makeToken(nil, nil, " ", whitespace, list)
	makeToken(nil, nil, "=", punctuation, list)
	makeToken(nil, nil, " ", whitespace, list)
	makeToken(nil, nil, c.Value, stringLiteral, list)
	makeToken(nil, nil, "", newline, list)
}

// getTypeClassification will return the token type for the text that is passed in
func getTypeClassification(s string) TokenType {
	if strings.HasPrefix(s, "*") {
		s = s[1:]
	}
	if reservedNames[s] != struct{}{} {
		return keyword
	}
	return literal
}
