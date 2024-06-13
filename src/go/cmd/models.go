// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

package cmd

import (
	"encoding/json"
	"fmt"
	"slices"
	"strings"
)

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

func (n Navigation) MarshalJSON() ([]byte, error) {
	sb := strings.Builder{}
	sb.WriteRune('{')
	sb.WriteString(fmt.Sprintf("\"Text\":\"%s\",", n.Text))
	sb.WriteString(fmt.Sprintf("\"NavigationId\":\"%s\",", n.NavigationId))
	ci, err := json.Marshal(n.ChildItems)
	if err != nil {
		return nil, err
	}
	sb.WriteString(fmt.Sprintf("\"ChildItems\":%s,", string(ci)))

	if n.Tags != nil {
		sb.WriteString("\"Tags\":")
		sb.WriteRune('{')
		// write tags in stable sort order.
		// default marshaler is non-deterministic
		tags := []string{}
		for key := range *n.Tags {
			tags = append(tags, fmt.Sprintf("\"%s\":\"%s\"", key, (*n.Tags)[key]))
		}
		slices.Sort(tags)
		sb.WriteString(strings.Join(tags, ","))
		sb.WriteRune('}')
	}

	sb.WriteRune('}')
	return []byte(sb.String()), nil
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
