// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

package com.azure.tools.codesnippetplugin;

import java.nio.file.Path;

/**
 * Verification result for a codesnippet.
 */
class VerifyResult {
    public String snippetWithIssues;
    public Path readmeLocation;

    public VerifyResult(Path readmeLocation, String snippetWithIssues) {
        this.readmeLocation = readmeLocation;
        this.snippetWithIssues = snippetWithIssues;
    }
}