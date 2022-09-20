// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

package com.azure.tools.codesnippetplugin.implementation;

import java.nio.file.Path;

/**
 * Represents a codesnippet length error during updating or validation.
 */
final class CodesnippetLengthError extends CodesnippetError {
    private final int snippetLength;

    /**
     * Creates a codesnippet mismatch error.
     *
     * @param snippetId The ID of the codesnippet.
     * @param snippetLocation Where the codesnippet was being referenced.
     * @param snippetLength The length of the codesnippet before applying replacements.
     */
    CodesnippetLengthError(String snippetId, Path snippetLocation, int snippetLength) {
        super(snippetId, snippetLocation);
        this.snippetLength = snippetLength;
    }

    @Override
    String getErrorMessage() {
        return "SnippetId: " + snippetId + ", Location: " + snippetLocation + ", Length: " + snippetLength;
    }

    /**
     * Gets the length of the codesnippet that had an error.
     *
     * @return The length of the codesnippet that had an error.
     */
    int getSnippetLength() {
        return snippetLength;
    }
}
