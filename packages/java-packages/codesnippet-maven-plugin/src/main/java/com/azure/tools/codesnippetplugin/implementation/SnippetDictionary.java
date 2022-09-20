// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

package com.azure.tools.codesnippetplugin.implementation;

import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.Map;

/**
 * Maintains the known codesnippets in a run.
 */
public final class SnippetDictionary {
    private final Map<String, List<String>> snippetDictionary = new HashMap<>();
    private final List<String> missingBeginTag = new ArrayList<>();

    public boolean isActive() {
        return !snippetDictionary.isEmpty();
    }

    /**
     * Gets all codesnippet aliases that are missing a corresponding end tag, aka all begin tags missing an end tag.
     *
     * @return All codesnippet aliases that are missing an end tag.
     */
    public List<String> getMissingEndTags() {
        return new ArrayList<>(snippetDictionary.keySet());
    }

    /**
     * Gets all codesnippet aliases that are missing a corresponding begin tag, aka all end tags missing a begin tag.
     *
     * @return All codesnippet aliases that are missing a begin tag.
     */
    public List<String> getMissingBeginTags() {
        return missingBeginTag;
    }

    public void beginSnippet(String key) {
        if (!this.snippetDictionary.containsKey((key))) {
            this.snippetDictionary.put(key, new ArrayList<>());
        }
    }

    public void processLine(String line) {
        for (Map.Entry<String, List<String>> entry : this.snippetDictionary.entrySet()) {
            entry.getValue().add(line);
        }
    }

    public List<String> finalizeSnippet(String key) {
        List<String> value = null;

        // Check the dictionary for containing the key.
        if (snippetDictionary.containsKey(key)) {
            // If it does contain the key return the codesnippet for the key.
            value = snippetDictionary.remove(key);
        } else {
            // Otherwise add the codesnippet to the list of keys that have missing begin tags.
            missingBeginTag.add(key);
        }

        // Return no matter what, if begin tags are missing an exception will be thrown later.
        return value;
    }
}
