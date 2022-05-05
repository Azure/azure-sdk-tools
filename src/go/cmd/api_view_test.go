// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

package cmd

import (
	"encoding/json"
	"io/ioutil"
	"os"
	"testing"

	"github.com/stretchr/testify/require"
)

func TestMain(m *testing.M) {
	indexTestdata = true
	os.Exit(m.Run())
}

func TestFuncDecl(t *testing.T) {
	err := CreateAPIView("./testdata/test_funcDecl/", "./output/")
	if err != nil {
		t.Fatal(err)
	}
	file, err := ioutil.ReadFile("./output/testfuncdecl.json")
	if err != nil {
		t.Fatal(err)
	}
	p := PackageReview{}
	err = json.Unmarshal(file, &p)
	if err != nil {
		t.Fatal(err)
	}
	if len(p.Tokens) != 41 {
		t.Fatal("unexpected token length, signals a change in the output")
	}
	if p.Name != "testfuncdecl" {
		t.Fatal("unexpected package name")
	}
	if len(p.Navigation) != 1 {
		t.Fatal("nagivation slice length should only be one for one package")
	}
	if len(p.Navigation[0].ChildItems) != 1 {
		t.Fatal("unexpected number of child items")
	}
}

func TestInterface(t *testing.T) {
	err := CreateAPIView("./testdata/test_interface/", "./output/")
	if err != nil {
		t.Fatal(err)
	}
	file, err := ioutil.ReadFile("./output/testinterface.json")
	if err != nil {
		t.Fatal(err)
	}
	p := PackageReview{}
	err = json.Unmarshal(file, &p)
	if err != nil {
		t.Fatal(err)
	}
	if len(p.Tokens) != 55 {
		t.Fatal("unexpected token length, signals a change in the output")
	}
	if p.Name != "testinterface" {
		t.Fatal("unexpected package name")
	}
	if len(p.Navigation) != 1 {
		t.Fatal("nagivation slice length should only be one for one package")
	}
	if len(p.Navigation[0].ChildItems) != 2 {
		t.Fatal("unexpected number of child items")
	}
}

func TestStruct(t *testing.T) {
	err := CreateAPIView("./testdata/test_struct/", "./output/")
	if err != nil {
		t.Fatal(err)
	}
	file, err := ioutil.ReadFile("./output/teststruct.json")
	if err != nil {
		t.Fatal(err)
	}
	p := PackageReview{}
	err = json.Unmarshal(file, &p)
	if err != nil {
		t.Fatal(err)
	}
	if len(p.Tokens) != 69 {
		t.Fatal("unexpected token length, signals a change in the output")
	}
	if p.Name != "teststruct" {
		t.Fatal("unexpected package name")
	}
	if len(p.Navigation) != 1 {
		t.Fatal("nagivation slice length should only be one for one package")
	}
	if len(p.Navigation[0].ChildItems) != 2 {
		t.Fatal("nagivation slice length should include link for ctor and struct")
	}
}

func TestConst(t *testing.T) {
	err := CreateAPIView("./testdata/test_const/", "./output/")
	if err != nil {
		t.Fatal(err)
	}
	file, err := ioutil.ReadFile("./output/testconst.json")
	if err != nil {
		t.Fatal(err)
	}
	p := PackageReview{}
	err = json.Unmarshal(file, &p)
	if err != nil {
		t.Fatal(err)
	}
	if len(p.Tokens) != 76 {
		t.Fatal("unexpected token length, signals a change in the output")
	}
	if p.Name != "testconst" {
		t.Fatal("unexpected package name")
	}
	if len(p.Navigation) != 1 {
		t.Fatal("nagivation slice length should only be one for one package")
	}
	if len(p.Navigation[0].ChildItems) != 3 {
		t.Fatal("unexpected child navigation items length")
	}
}

func TestSubpackage(t *testing.T) {
	review, err := createReview("./testdata/test_subpackage")
	require.NoError(t, err)
	require.Equal(t, "Go", review.Language)
	require.Equal(t, "test_subpackage", review.Name)
	seen := map[string]bool{}
	for _, token := range review.Tokens {
		if token.DefinitionID != nil {
			if seen[*token.DefinitionID] {
				t.Fatal("duplicate DefinitionID: " + *token.DefinitionID)
			}
			seen[*token.DefinitionID] = true
		}
	}
	// 2 packages * 10 exports each = 22 unique definition IDs expected
	require.Equal(t, 22, len(seen))
	// 10 exports - 4 methods = 6 nav links expected
	require.Equal(t, 2, len(review.Navigation))
	for _, nav := range review.Navigation {
		require.Equal(t, 6, len(nav.ChildItems))
		for _, item := range nav.ChildItems {
			require.Contains(t, seen, item.NavigationId)
		}
	}
}

func TestDiagnostics(t *testing.T) {
	review, err := createReview("./testdata/test_diagnostics")
	require.NoError(t, err)
	require.Equal(t, "Go", review.Language)
	require.Equal(t, "test_diagnostics", review.Name)
	require.Equal(t, 4, len(review.Diagnostics))
	for _, diagnostic := range review.Diagnostics {
		switch target := diagnostic.TargetID; target {
		case "test_diagnostics.Alias":
			require.Equal(t, DiagnosticLevelInfo, diagnostic.Level)
			require.Equal(t, aliasFor+"test_diagnostics/internal.InternalStruct", diagnostic.Text)
		case "test_diagnostics.ExportedStruct":
			require.Equal(t, DiagnosticLevelError, diagnostic.Level)
			require.Equal(t, diagnostic.Text, embedsUnexportedStruct+"unexportedStruct")
		case "test_diagnostics.ExternalAlias":
			require.Equal(t, DiagnosticLevelWarning, diagnostic.Level)
			require.Equal(t, aliasFor+"net/http.Client", diagnostic.Text)
		case "test_diagnostics.Sealed":
			require.Equal(t, DiagnosticLevelInfo, diagnostic.Level)
			require.Equal(t, sealedInterface, diagnostic.Text)
		default:
			t.Fatal("unexpected target " + target)
		}
	}
	require.Equal(t, embedsUnexportedStruct+"unexportedStruct", review.Diagnostics[0].Text)
	require.Equal(t, DiagnosticLevelError, review.Diagnostics[0].Level)
}
