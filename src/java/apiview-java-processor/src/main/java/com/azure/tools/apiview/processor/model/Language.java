package com.azure.tools.apiview.processor.model;

public enum Language {
    JAVA("java"), KOTLIN("kotlin");

    private final String language;

    Language(String language) {
        this.language = language;
    }

    @Override public String toString() {
        return language;
    }
}