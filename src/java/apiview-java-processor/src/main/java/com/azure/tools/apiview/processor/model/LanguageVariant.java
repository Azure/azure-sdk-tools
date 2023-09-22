package com.azure.tools.apiview.processor.model;

import com.fasterxml.jackson.annotation.JsonValue;

public enum LanguageVariant {
    DEFAULT("Default"),
    SPRING("Spring"),
    ANDROID("Android");

    private final String variantName;

    LanguageVariant(String name) {
        this.variantName = name;
    }

    @Override
    @JsonValue
    public String toString() {
        return this.variantName;
    }
}
