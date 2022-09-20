// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

package cmd

// This file contains models comprising an APIView document

type Diagnostic struct {
	DiagnosticID string          `json:"DiagnosticId,omitempty"`
	HelpLinkURI  string          `json:"HelpLinkUri,omitempty"`
	Level        DiagnosticLevel `json:"Level,omitempty"`
	// TargetID is the DefinitionID of the Token to which this diagnostic applies
	TargetID string `json:"TargetId,omitempty"`
	Text     string `json:"Text,omitempty"`
}

type DiagnosticLevel int

const (
	DiagnosticLevelInfo    DiagnosticLevel = 1
	DiagnosticLevelWarning DiagnosticLevel = 2
	DiagnosticLevelError   DiagnosticLevel = 3
)

type Navigation struct {
	Text         string             `json:"Text"`
	NavigationId string             `json:"NavigationId"`
	ChildItems   []Navigation       `json:"ChildItems"`
	Tags         *map[string]string `json:"Tags"`
}

// PackageReview ...
type PackageReview struct {
	Diagnostics []Diagnostic `json:"Diagnostics,omitempty"`
	Language    string       `json:"Language,omitempty"`
	Name        string       `json:"Name,omitempty"`
	Tokens      []Token      `json:"Tokens,omitempty"`
	Navigation  []Navigation `json:"Navigation,omitempty"`
	PackageName string       `json:"PackageName,omitempty"`
}

// Token ...
type Token struct {
	DefinitionID *string   `json:"DefinitionId"`
	NavigateToID *string   `json:"NavigateToId"`
	Value        string    `json:"Value"`
	Kind         TokenType `json:"Kind"`
}

type TokenType int

const (
	TokenTypeText          TokenType = 0
	TokenTypeNewline       TokenType = 1
	TokenTypeWhitespace    TokenType = 2
	TokenTypePunctuation   TokenType = 3
	TokenTypeKeyword       TokenType = 6
	TokenTypeLineIDMarker  TokenType = 5
	TokenTypeTypeName      TokenType = 4
	TokenTypeMemberName    TokenType = 7
	TokenTypeStringLiteral TokenType = 8
	TokenTypeLiteral       TokenType = 9
	TokenTypeComment       TokenType = 10
)
