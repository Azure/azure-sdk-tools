// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

package cmd

import "encoding/json"

// This file contains models comprising an APIView document

type CodeDiagnostic struct {
	DiagnosticID string `json:"DiagnosticId,omitempty"`
	HelpLinkURI  string `json:"HelpLinkUri,omitempty"`
	// Level is required
	Level CodeDiagnosticLevel
	// TargetID is the LineID of the ReviewLine on which to display this diagnostic. Required.
	TargetID string `json:"TargetId"`
	// Text is a comment to display under the line identified by TargetID. Required.
	Text string `json:"Text"`
}

type CodeDiagnosticLevel int

const (
	CodeDiagnosticLevelInfo    CodeDiagnosticLevel = 1
	CodeDiagnosticLevelWarning CodeDiagnosticLevel = 2
	CodeDiagnosticLevelError   CodeDiagnosticLevel = 3
	// CodeDiagnosticLevelFatal blocks API review approval and displays an error message
	CodeDiagnosticLevelFatal CodeDiagnosticLevel = 4
)

type CodeFile struct {
	CrossLanguagePackageID string           `json:"CrossLanguagePackageId,omitempty"`
	Diagnostics            []CodeDiagnostic `json:"Diagnostics,omitempty"`
	Language               string           `json:"Language,omitempty"`
	// LanguageVariant should be "None" if set
	LanguageVariant string           `json:"LanguageVariant,omitempty"`
	Name            string           `json:"Name,omitempty"`
	Navigation      []NavigationItem `json:"Navigation,omitempty"`
	PackageName     string           `json:"PackageName"`
	PackageVersion  string           `json:"PackageVersion"`
	ParserVersion   string           `json:"ParserVersion"`
	ReviewLines     []ReviewLine     `json:"ReviewLines,omitempty"`
}

type NavigationItem struct {
	ChildItems   []NavigationItem   `json:"ChildItems"`
	NavigationID string             `json:"NavigationId"`
	Tags         *map[string]string `json:"Tags"`
	Text         string             `json:"Text"`
}

type ReviewLine struct {
	// Children of the ReviewLine such as types in a namespace or methods on a type
	Children        []ReviewLine `json:"Children,omitempty"`
	CrossLanguageID string       `json:"CrossLanguageId,omitempty"`
	// IsContextEndLine sets this line as the end of a context e.g. a line with token "}" or an empty line after a type definition
	IsContextEndLine bool `json:"IsContextEndLine,omitempty"`
	// IsHidden hides lines that shouldn't be visible by default
	IsHidden bool `json:"IsHidden,omitempty"`
	// LineID is for lines that should allow comments. Its value must be unique within the CodeFile.
	LineID string `json:"LineId,omitempty"`
	// RelatedToLine is the ID of a line that when hidden, also hides this line
	RelatedToLine string `json:"RelatedToLine,omitempty"`
	// Tokens that construct a line in API review. This is the only required field.
	Tokens []ReviewToken `json:"Tokens"`
}

// MarshalJSON ensures the Tokens field isn't marshalled as null, as required
// by the schema
func (r ReviewLine) MarshalJSON() ([]byte, error) {
	type Alias ReviewLine
	if r.Tokens == nil {
		r.Tokens = []ReviewToken{}
	}
	return json.Marshal((Alias)(r))
}

// ReviewToken corresponds to an individual token within a ReviewLine
type ReviewToken struct {
	HasPrefixSpace  bool `json:"HasPrefixSpace,omitempty"`
	HasSuffixSpace  bool `json:"HasSuffixSpace"`
	IsDeprecated    bool `json:"IsDeprecated,omitempty"`
	IsDocumentation bool `json:"IsDocumentation,omitempty"`
	// Kind of token. Required.
	Kind TokenKind
	// NavigateToID is the LineID of another line this token should navigate to when clicked
	NavigateToID string `json:"NavigateToId,omitempty"`
	// NavigationDisplayName creates a tree node in the navigation panel. Navigation nodes will be created only if token contains navigation display name.
	NavigationDisplayName string `json:"NavigationDisplayName,omitempty"`
	// RenderClasses is language specific CSS class names for the token
	RenderClasses []string `json:"RenderClasses,omitempty"`
	// SkipDiff declares whether the token should be ignored in diff calculation
	SkipDiff bool `json:"SkipDiff,omitempty"`
	// Value of the token. Required.
	Value string
}

// MarshalJSON omits the HasSuffixSpace field when it's true because API View defaults that value to true
func (r ReviewToken) MarshalJSON() ([]byte, error) {
	type Alias ReviewToken
	aux := struct {
		Alias
		HasSuffixSpace *bool `json:"HasSuffixSpace,omitempty"`
	}{
		Alias: (Alias)(r),
	}
	if !r.HasSuffixSpace {
		aux.HasSuffixSpace = &r.HasSuffixSpace
	}
	return json.Marshal(aux)
}

type TokenKind int

const (
	TokenKindText        TokenKind = 0
	TokenKindPunctuation TokenKind = 1
	TokenKindKeyword     TokenKind = 2
	// TokenKindTypeName is for type definitions, parameter types, etc.
	TokenKindTypeName TokenKind = 3
	// TokenKindMemberName is for method and field names
	TokenKindMemberName TokenKind = 4
	// TokenKindStringLiteral is for any metadata or string literals to show in a review
	TokenKindStringLiteral TokenKind = 5
	// TokenKindLiteral is for any literals, e.g. enum value or numerical constant literal or default value
	TokenKindLiteral TokenKind = 6
	TokenKindComment TokenKind = 7
)
