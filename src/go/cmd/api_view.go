// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

package cmd

import (
	"encoding/json"
	"fmt"
	"io/ioutil"
	"sort"
	"strings"
)

type TokenType int

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

var (
	reservedNames = map[string]struct{}{
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
)

// CreateAPIView generates the output file that the API view tool uses.
func CreateAPIView(pkgDir, outputDir string) error {
	m, err := NewModule(pkgDir)
	if err != nil {
		panic(err)
	}
	tokenList := &[]Token{}
	nav := []Navigation{}
	packageNames := []string{}
	for name, p := range m.packages {
		if strings.Contains(p.relName, "internal") || p.c.isEmpty() {
			continue
		}
		packageNames = append(packageNames, name)
	}
	sort.Strings(packageNames)
	for _, name := range packageNames {
		p := m.packages[name]
		n := p.relName
		makeToken(nil, nil, "package", memberName, tokenList)
		makeToken(nil, nil, " ", whitespace, tokenList)
		makeToken(&n, &n, n, typeName, tokenList)
		makeToken(nil, nil, "", newline, tokenList)
		makeToken(nil, nil, "", newline, tokenList)
		// TODO: reordering these calls reorders APIView output and can omit content
		p.c.parseSimpleType(tokenList)
		p.c.parseConst(tokenList)
		p.c.parseInterface(tokenList)
		p.c.parseStruct(tokenList)
		p.c.parseFunc(tokenList)
		navItems := p.c.generateNavChildItems()
		nav = append(nav, Navigation{
			Text:         &n,
			NavigationId: &n,
			ChildItems:   navItems,
		})
	}
	review := PackageReview{
		Language:   "Go",
		Name:       m.Name,
		Tokens:     *tokenList,
		Navigation: nav,
	}
	if outputDir == "." {
		outputDir = ""
	} else if !strings.HasSuffix(outputDir, "/") {
		outputDir = fmt.Sprintf("%s/", outputDir)
	}
	filename := fmt.Sprintf("%s%s.json", outputDir, m.Name)
	file, _ := json.MarshalIndent(review, "", " ")
	err = ioutil.WriteFile(filename, file, 0644)
	if err != nil {
		return err
	}
	return nil
}
