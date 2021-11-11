// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

package com.azure.tools.codesnippetplugin.implementation;

import java.nio.file.Path;
import java.util.List;

/**
 * Represents a codesnippet.
 */
final class Codesnippet {
    private final String id;
    private final Path definitionLocation;
    private final List<String> content;

    /**
     * Creates a new codesnippet.
     *
     * @param id ID of the codesnippet.
     * @param definitionLocation Where the codesnippet is defined.
     * @param content Content of the codesnippet.
     */
    Codesnippet(String id, Path definitionLocation, List<String> content) {
        this.id = id;
        this.definitionLocation = definitionLocation;
        this.content = content;
    }

    /**
     * Gets the ID of the codesnippet.
     *
     * @return The ID of the codesnippet.
     */
    String getId() {
        return id;
    }

    /**
     * Gets the location where the codesnippet is defined.
     *
     * @return The location where the codesnippet is defined.
     */
    Path getDefinitionLocation() {
        return definitionLocation;
    }

    /**
     * Gets the contents of the codesnippet.
     *
     * @return Contents of the codesnippet.
     */
    List<String> getContent() {
        return content;
    }
}
