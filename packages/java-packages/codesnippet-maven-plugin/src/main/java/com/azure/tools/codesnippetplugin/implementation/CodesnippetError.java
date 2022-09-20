// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

package com.azure.tools.codesnippetplugin.implementation;

import java.nio.file.Path;

/**
 * Represents an error that occurred during snippet updating or verification.
 */
abstract class CodesnippetError {
    final String snippetId;
    final Path snippetLocation;

    /**
     * Creates an error that occurred during snippet updating or verification.
     *
     * @param snippetId The ID of the codesnippet.
     * @param snippetLocation Where the codesnippet was being referenced.
     */
    CodesnippetError(String snippetId, Path snippetLocation) {
        this.snippetId = snippetId;
        this.snippetLocation = snippetLocation;
    }

    /**
     * Gets the identifier of the codesnippet that had an error.
     *
     * @return The identifier of the codesnippet that had an error.
     */
    final String getSnippetId() {
        return snippetId;
    }

    /**
     * Gets the location where the codesnippet was referenced.
     *
     * @return The location where the codesnippet was referenced.
     */
    final Path getLocation() {
        return snippetLocation;
    }

    /**
     * Gets the message for this error.
     *
     * @return The message for this error.
     */
    String getErrorMessage() {
        return "SnippetId: " + snippetId + ", Location: " + snippetLocation;
    }
}
