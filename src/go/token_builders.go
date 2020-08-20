// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

package main

import (
	"strings"
)

func addTab() string {
	return "     "
}

func makeToken(defID, navID *string, val string, kind int, list *[]Token) {
	tok := Token{DefinitionID: defID, NavigateToID: navID, Value: val, Kind: kind}
	*list = append(*list, tok)
}

func makeStructTokens(name *string, anonFields []string, fields map[string]string, list *[]Token) {
	n := *name
	makeToken(nil, nil, "", newline, list)
	makeToken(nil, nil, " ", whitespace, list)
	makeToken(nil, nil, "", newline, list)
	makeToken(nil, nil, "type", keyword, list)
	makeToken(nil, nil, " ", whitespace, list)
	makeToken(&n, &n, *name, typeName, list)
	makeToken(nil, nil, " ", whitespace, list)
	makeToken(nil, nil, "struct", keyword, list)
	makeToken(nil, nil, " ", whitespace, list)
	makeToken(nil, nil, "{", punctuation, list)
	if anonFields != nil || fields != nil {
		for _, v1 := range anonFields {
			makeToken(nil, nil, "", newline, list)
			makeToken(nil, nil, addTab(), whitespace, list)
			makeToken(&v1, nil, v1, typeName, list)
		}
		for k1, v1 := range fields {
			makeToken(nil, nil, "", newline, list)
			makeToken(nil, nil, addTab(), whitespace, list)
			makeToken(&k1, nil, k1, typeName, list)
			makeToken(nil, nil, " ", whitespace, list)
			makeToken(nil, nil, v1, getTypeClassification(v1), list)
		}
	}
	if anonFields == nil && fields == nil {
		makeToken(nil, nil, "", newline, list)
		makeToken(nil, nil, addTab(), whitespace, list)
		makeToken(nil, nil, "// All fields are unexported", comment, list)
		makeToken(nil, nil, "", newline, list)
	}
	makeToken(nil, nil, "", newline, list)
	makeToken(nil, nil, "}", punctuation, list)
	makeToken(nil, nil, "", newline, list)
}

func makeInterfaceTokens(name *string, methods map[string]Func, list *[]Token) {
	n := *name
	makeToken(nil, nil, "type", keyword, list)
	makeToken(nil, nil, " ", whitespace, list)
	makeToken(&n, &n, *name, typeName, list)
	makeToken(nil, nil, " ", whitespace, list)
	makeToken(nil, nil, "interface", keyword, list)
	makeToken(nil, nil, " ", whitespace, list)
	makeToken(nil, nil, "{", punctuation, list)
	makeToken(nil, nil, "", newline, list)
	if methods != nil {
		for k1, v1 := range methods {
			makeIntFuncTokens(&k1, v1, list)
			makeToken(nil, nil, "", newline, list)
		}
	}
	makeToken(nil, nil, "}", punctuation, list)
	makeToken(nil, nil, "", newline, list)
	makeToken(nil, nil, " ", whitespace, list)
	makeToken(nil, nil, "", newline, list)
}

func makeIntFuncTokens(name *string, funcs Func, list *[]Token) {
	makeToken(nil, nil, addTab(), whitespace, list)
	makeIntMethodTokens(name, funcs.Params, funcs.Returns, list)
}

func makeFuncTokens(name *string, params, results *string, returnCount int, list *[]Token) {
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
			makeToken(nil, nil, temp[1], getTypeClassification(temp[1]), list)
			if id < len(tok)-1 {
				makeToken(nil, nil, ",", punctuation, list)
				makeToken(nil, nil, " ", whitespace, list)
			}
		}
	}
	makeToken(nil, nil, ")", punctuation, list)
	makeToken(nil, nil, " ", whitespace, list)
	if results != nil {
		r := *results
		tok := strings.Split(r, ",")
		if len(tok) > 1 {
			makeToken(nil, nil, "(", punctuation, list)
		}
		for id, i := range tok {
			temp := strings.Split(i, " ")
			makeToken(nil, nil, temp[0], getTypeClassification(temp[0]), list)
			// makeToken(nil, nil, " ",  whitespace, list)
			// makeToken(nil, nil, temp[1],  keyword, list)
			if id < len(tok)-1 {
				makeToken(nil, nil, ",", punctuation, list)
				makeToken(nil, nil, " ", whitespace, list)
			}
		}
		if len(tok) > 1 {
			makeToken(nil, nil, ")", punctuation, list)
		}
		makeToken(nil, nil, "", newline, list)
	}
	makeToken(nil, nil, "", newline, list)
}

func makeMethodTokens(receiverVar, receiver string, isPointer bool, name string, params, results *string, returnCount int, list *[]Token) {
	if isOnUnexportedMember(name) || isExampleOrTest(name) {
		return
	}
	makeToken(nil, nil, "func", keyword, list)
	makeToken(nil, nil, " ", whitespace, list)
	makeToken(nil, nil, "(", punctuation, list)
	makeToken(nil, nil, receiverVar, typeName, list)
	makeToken(nil, nil, " ", whitespace, list)
	if isPointer {
		makeToken(nil, nil, "*", punctuation, list)
	}
	makeToken(nil, nil, receiver, typeName, list)
	makeToken(nil, nil, ")", punctuation, list)
	makeToken(nil, nil, " ", whitespace, list)
	makeToken(&name, nil, name, typeName, list)
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
	if results != nil {
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
		makeToken(nil, nil, "", newline, list)
	}
	makeToken(nil, nil, "", newline, list)
}

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
		makeToken(nil, nil, "", newline, list)
	}
	makeToken(nil, nil, "", newline, list)
}

func makeConstTokens(name *string, c Const, list *[]Token) {
	n := *name
	makeToken(nil, nil, addTab(), whitespace, list)
	makeToken(&n, nil, *name, typeName, list)
	makeToken(nil, nil, " ", whitespace, list)
	makeToken(nil, nil, c.Type, getTypeClassification(c.Type), list)
	makeToken(nil, nil, " ", whitespace, list)
	makeToken(nil, nil, "=", punctuation, list)
	makeToken(nil, nil, " ", whitespace, list)
	makeToken(nil, nil, "\"", stringLiteral, list)
	makeToken(nil, nil, c.Value, stringLiteral, list)
	makeToken(nil, nil, "\"", stringLiteral, list)
	makeToken(nil, nil, "", newline, list)
}

func getTypeClassification(s string) int {
	if strings.HasPrefix(s, "*") {
		s = s[1:]
	}
	if reservedNames[s] {
		return keyword
	}
	return literal
}
