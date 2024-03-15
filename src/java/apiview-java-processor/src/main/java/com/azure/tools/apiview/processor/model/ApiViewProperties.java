package com.azure.tools.apiview.processor.model;

import com.fasterxml.jackson.annotation.JsonProperty;

import java.util.Collections;
import java.util.HashMap;
import java.util.Map;
import java.util.Optional;

/**
 * Sometimes libraries carry additional metadata with them that can make the output from APIView more useful. This
 * class is used to store that metadata, as it is deserialized from the /META-INF/apiview_properties.json file.
 */
public class ApiViewProperties {

    // This is a map of model names and methods to their TypeSpec definition IDs.
    @JsonProperty("CrossLanguageDefinitionId")
    private final Map<String, String> crossLanguageDefinitionIds = new HashMap<>();

    /**
     * Cross Languages Definition ID is used to map from a model name or a method name to a TypeSpec definition ID. This
     * is used to enable cross-language linking, to make review time quicker as reviewers can jump between languages to
     * see how the API is implemented in each language.
     */
    public Optional<String> getCrossLanguageDefinitionId(String fullyQualifiedName) {
        return Optional.ofNullable(crossLanguageDefinitionIds.get(fullyQualifiedName));
    }

    /**
     * Returns an unmodifiable map of all the cross-language definition IDs.
     */
    public Map<String, String> getCrossLanguageDefinitionIds() {
        return Collections.unmodifiableMap(crossLanguageDefinitionIds);
    }
}
