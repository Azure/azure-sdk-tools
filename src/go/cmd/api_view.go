// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

package cmd

import (
	"encoding/json"
	"io/ioutil"
	"path/filepath"
	"sort"
	"strings"
)

// CreateAPIView generates the output file that the API view tool uses.
func CreateAPIView(pkgDir, outputDir string) error {
	review, err := createReview(pkgDir)
	if err != nil {
		panic(err)
	}
	filename := filepath.Join(outputDir, review.Name+".json")
	file, _ := json.MarshalIndent(review, "", " ")
	err = ioutil.WriteFile(filename, file, 0644)
	if err != nil {
		return err
	}
	return nil
}

func createReview(pkgDir string) (PackageReview, error) {
	m, err := NewModule(pkgDir)
	if err != nil {
		return PackageReview{}, err
	}
	tokenList := &[]Token{}
	nav := []Navigation{}
	diagnostics := []Diagnostic{}
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
		makeToken(nil, nil, "package", TokenTypeMemberName, tokenList)
		makeToken(nil, nil, " ", TokenTypeWhitespace, tokenList)
		makeToken(&n, nil, n, TokenTypeTypeName, tokenList)
		makeToken(nil, nil, "", TokenTypeNewline, tokenList)
		makeToken(nil, nil, "", TokenTypeNewline, tokenList)
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
			Tags: &map[string]string{
				"TypeKind": "namespace",
			},
		})
		diagnostics = append(diagnostics, p.diagnostics...)
	}
	return PackageReview{
		Diagnostics: diagnostics,
		Language:    "Go",
		Name:        m.Name,
		Navigation:  nav,
		Tokens:      *tokenList,
	}, nil
}
