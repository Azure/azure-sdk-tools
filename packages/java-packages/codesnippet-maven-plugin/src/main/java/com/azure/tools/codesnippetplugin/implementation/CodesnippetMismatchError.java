// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

package com.azure.tools.codesnippetplugin.implementation;

import java.nio.file.Path;

/**
 * Represents a codesnippet mismatch error during validation.
 */
final class CodesnippetMismatchError extends CodesnippetError {
    /**
     * Creates a codesnippet mismatch error.
     *
     * @param snippetId The ID of the codesnippet.
     * @param snippetLocation Where the codesnippet was being referenced.
     */
    CodesnippetMismatchError(String snippetId, Path snippetLocation) {
        super(snippetId, snippetLocation);
    }
}
