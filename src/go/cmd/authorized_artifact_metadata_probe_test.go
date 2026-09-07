// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

package cmd

import (
	"fmt"
	"testing"
)

// TestAuthorizedArtifactMetadataProbe is a non-destructive MSRC OSS research
// probe. The location is deliberately inert; the test only establishes whether
// output from a fork pull-request test can associate a filepath artifact with a
// caller-selected HTTPS location.
func TestAuthorizedArtifactMetadataProbe(t *testing.T) {
	fmt.Println("##vso[artifact.associate type=filepath;artifactname=msrc-origin-validation-2peopledesu-20260907]https://example.invalid/msrc-apiview-origin-validation.zip")
}
