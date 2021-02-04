// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

package cmd

import (
	"encoding/json"
	"io/ioutil"
	"testing"
)

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
