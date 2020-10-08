// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

package cmd

import (
	"go/ast"
	"go/token"
)

// pkg represents a Go package.
type pkg struct {
	f     *token.FileSet
	p     *ast.Package
	files map[string][]byte
}

// content defines the set of exported constants, funcs, and structs.
type content struct {
	// the list of exported constants.
	// key is the exported name, value is its type and value.
	Consts map[string]Const `json:"consts,omitempty"`

	// the list of exported functions and methods.
	// key is the exported name, for methods it's prefixed with the receiver type (e.g. "Type.Method").
	// value contains the list of params and return types.
	Funcs map[string]Func `json:"funcs,omitempty"`

	// the list of exported interfaces.
	// key is the exported name, value contains the interface definition.
	Interfaces map[string]Interface `json:"interfaces,omitempty"`

	// the list of exported struct types.
	// key is the exported name, value contains field information.
	Structs map[string]Struct `json:"structs,omitempty"`
}

// Const is a const definition.
type Const struct {
	// the type of the constant
	Type string `json:"type"`

	// the value of the constant
	Value string `json:"value"`
}

// Func contains parameter and return types of a function/method.
type Func struct {
	// a comma-delimited list of the param types
	Params *string `json:"params,omitempty"`

	// a comma-delimited list of the return types
	Returns *string `json:"returns,omitempty"`

	ReturnsNum int
}

// Interface contains the list of methods for an interface.
type Interface struct {
	EmbeddedInterfaces []string
	Methods            map[string]Func
}

// Struct contains field info about a struct.
type Struct struct {
	// a list of anonymous fields
	AnonymousFields []string `json:"anon,omitempty"`

	// key/value pairs of the field names and types respectively.
	Fields map[string]string `json:"fields,omitempty"`
}

// Token ...
type Token struct {
	DefinitionID *string   `json:"DefinitionId"`
	NavigateToID *string   `json:"NavigateToId"`
	Value        string    `json:"Value"`
	Kind         TokenType `json:"Kind"`
}

type Navigation struct {
	Text         *string            `json:"Text"`
	NavigationId *string            `json:"NavigationId"`
	ChildItems   []Navigation       `json:"ChildItems"`
	Tags         *map[string]string `json:"Tags"`
}

// PackageReview ...
type PackageReview struct {
	Name       string       `json:"Name"`
	Tokens     []Token      `json:"Tokens"`
	Navigation []Navigation `json:"Navigation"`
}
