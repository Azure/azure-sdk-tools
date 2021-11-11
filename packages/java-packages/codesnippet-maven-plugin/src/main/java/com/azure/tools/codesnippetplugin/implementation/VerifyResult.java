// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

package com.azure.tools.codesnippetplugin.implementation;

import java.nio.file.Path;

/**
 * Verification result for a codesnippet.
 */
public final class VerifyResult {
    private final Path location;
    private final String snippetId;

    public VerifyResult(Path location, String snippetId) {
        this.location = location;
        this.snippetId = snippetId;
    }

    /**
     * Gets the location where the codesnippet verification took place.
     *
     * @return The location where the codesnippet verification took place.
     */
    public Path getLocation() {
        return location;
    }

    /**
     * Gets the identifier of the codesnippet that was verified.
     *
     * @return The identifier of the codesnippet that was verified.
     */
    public String getSnippetId() {
        return snippetId;
    }
}
