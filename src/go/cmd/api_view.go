// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

package cmd

import (
	"encoding/json"
	"fmt"
	"io/ioutil"
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
	// load the given Go package
	pkg, err := loadPackage(pkgDir)
	if err != nil {
		return err
	}
	tokenList := &[]Token{}
	c := newContent()
	c = inspectAST(pkg)
	// create the tokens for the Go package declaration
	makeToken(nil, nil, "package", memberName, tokenList)
	makeToken(nil, nil, " ", whitespace, tokenList)
	makeToken(&pkg.p.Name, nil, pkg.p.Name, typeName, tokenList)
	makeToken(nil, nil, "", newline, tokenList)
	makeToken(nil, nil, " ", whitespace, tokenList)
	makeToken(nil, nil, "", newline, tokenList)
	// work through consts, interfaces, structs and funcs sequentially adding tokens
	c.parseConst(tokenList)
	c.parseInterface(tokenList)
	c.parseStruct(tokenList)
	c.parseFunc(tokenList)
	// generate navigation items for each top level Go component
	navItems := c.generateNavChildItems()
	review := PackageReview{
		Name:   pkg.p.Name,
		Tokens: *tokenList,
		Navigation: []Navigation{
			{
				Text:       &pkg.p.Name,
				ChildItems: navItems,
			},
		},
	}
	if outputDir == "." {
		outputDir = ""
	} else if !strings.HasSuffix(outputDir, "/") {
		outputDir = fmt.Sprintf("%s/", outputDir)
	}
	filename := fmt.Sprintf("%s%s.json", outputDir, pkg.p.Name)
	file, _ := json.MarshalIndent(review, "", " ")
	err = ioutil.WriteFile(filename, file, 0644)
	if err != nil {
		return err
	}
	return nil
}
