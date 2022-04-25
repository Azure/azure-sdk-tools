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
		p.c.parseInterface(tokenList)
		p.c.parseStruct(tokenList)
		p.c.parseSimpleType(tokenList)
		p.c.parseVar(tokenList)
		p.c.parseConst(tokenList)
		p.c.parseFunc(tokenList)
		navItems := p.c.generateNavChildItems()
		nav = append(nav, Navigation{
			Text:         n,
			NavigationId: n,
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
