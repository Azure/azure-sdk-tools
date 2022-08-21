package com.azure.tools.apiview.processor.model;

import com.fasterxml.jackson.annotation.JsonValue;

public enum TypeKind {
    ASSEMBLY("assembly"),     // i.e. a Jar File
    NAMESPACE("namespace"),   // i.e. a Java package
    CLASS("class"),
    INTERFACE("interface"),
    ENUM("enum"),
    ANNOTATION("annotation"),
    MODULE("module"),
    MAVEN("maven"),
    GRADLE("gradle"),
    UNKNOWN("unknown");

    private final String name;

    TypeKind(String name) {
        this.name = name;
    }

    @JsonValue
    public String getName() {
        return name;
    }

    public static TypeKind fromClass(Class<?> cls) {
        if (cls.isEnum()) {
            return ENUM;
        } else if (cls.isInterface()) {
            return INTERFACE;
        } else {
            return CLASS;
        }
    }
}
