// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

package com.azure.tools.codesnippetplugin;

import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.Map;

class SnippetDictionary {
    private final Map<String, List<String>> snippetDictionary = new HashMap<>();

    public boolean isActive() {
        return !snippetDictionary.isEmpty();
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
        List<String> value = this.snippetDictionary.get(key);
        this.snippetDictionary.remove(key);

        return value;
    }
}
