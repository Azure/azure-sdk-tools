// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

package com.azure.tools.codesnippetplugin.implementation;

import java.nio.file.Path;

/**
 * Represents a missing codesnippet error during updating or validation.
 */
final class CodesnippetMissingError extends CodesnippetError {
    /**
     * Creates a missing codesnippet error.
     *
     * @param snippetId The ID of the codesnippet.
     * @param snippetLocation Where the codesnippet was being referenced.
     */
    CodesnippetMissingError(String snippetId, Path snippetLocation) {
        super(snippetId, snippetLocation);
    }
}
