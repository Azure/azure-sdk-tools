// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

package cmd

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

type SimpleType struct {
	Name           *string `json:"name,omitempty"`
	UnderlyingType *string `json:"underlyingType,omitempty"`
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
