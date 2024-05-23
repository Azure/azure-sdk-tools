
package com.azure.tools.apiview.processor.model;

import java.util.*;

import static com.azure.tools.apiview.processor.model.TokenKind.StructuredTokenKind.*;

public enum TokenKind {
    TEXT(CONTENT),
    NEW_LINE(LINE_BREAK),
    WHITESPACE(NONE_BREAKING_SPACE),
    PUNCTUATION(CONTENT, "punctuation"),
    KEYWORD(CONTENT, "keyword"),

    TYPE_NAME(CONTENT, "typeName"),
//    MEMBER_NAME(CONTENT, "memberName"),
    STRING_LITERAL(CONTENT, "stringLiteral"),
    NUMBER(CONTENT, "number"),
//    LITERAL(CONTENT, "literal"),

    PACKAGE_NAME(CONTENT, "typeName", "packageName"),
    MODULE_NAME(CONTENT, "typeName", "moduleName"),

    ENUM_TYPE(CONTENT, "typeName", "enumType"),
    ENUM_CONSTANT(CONTENT, "typeName", "enumConstant"),

    ANNOTATION_NAME(CONTENT, "typeName", "annotationName"),
    ANNOTATION_PARAMETER_NAME(CONTENT, "typeName", "annotationParameterName"),
    ANNOTATION_PARAMETER_VALUE(CONTENT, "typeName", "annotationParameterValue"),

    RETURN_TYPE(CONTENT, "typeName", "returnType"),
    PARAMETER_TYPE(CONTENT, "typeName", "parameterType"),
    PARAMETER_NAME(CONTENT, "parameterName"),
    EXTENDS_TYPE(CONTENT, "typeName", "extendsType"),
    IMPLEMENTS_TYPE(CONTENT, "typeName", "implementsType"),

    METHOD_NAME(CONTENT, "memberName", "methodName"),
    FIELD_NAME(CONTENT, "memberName", "fieldName"),

    MAVEN_KEY(CONTENT, "keyword", "mavenKey"),
    MAVEN_VALUE(CONTENT, "mavenValue"),
    MAVEN_DEPENDENCY(CONTENT, "mavenValue", "dependency"),

    JAVADOC(CONTENT, "comment", "javadoc") {
        @Override public Map<String, String> getProperties() {
            return Map.of("GroupId", "documentation");
        }
    },

    // comment is a single line comment
    COMMENT(CONTENT, "comment");

    // for types and members that are marked as deprecated
//    DEPRECATED_RANGE_START(13),
//    DEPRECATED_RANGE_END(14),

    // for any metadata that should not be compared when checking diff
//    SKIP_DIFF_START(15),
//    SKIP_DIFF_END(16),

    // for external links
//    EXTERNAL_LINK_START(28),
//    EXTERNAL_LINK_END(29);

//    private static final TokenKind[] VALUES;
//
//    static {
//        VALUES = new TokenKind[30];
//        for (TokenKind kind : TokenKind.values()) {
//            VALUES[kind.id] = kind;
//        }
//    }

//    private final int id;
    private final StructuredTokenKind structuredTokenKind;
    private Set<String> renderClasses;

    TokenKind(StructuredTokenKind structuredTokenKind, String... renderClasses) {
        this.structuredTokenKind = structuredTokenKind;

        if (renderClasses != null) {
            this.renderClasses = new LinkedHashSet<>();
            this.renderClasses.addAll(Arrays.asList(renderClasses));
        }
    }

    public int getStructuredTokenKind() {
        return structuredTokenKind.id;
    }

    public Set<String> getRenderClasses() {
        return renderClasses;
    }

    public Map<String, String> getProperties() {
        return Collections.emptyMap();
    }

    enum StructuredTokenKind {
        CONTENT(0),
        LINE_BREAK(1),
        NONE_BREAKING_SPACE(2),
        TAB_SPACE(3),
        PARAMETER_SEPARATOR(4),
        URL(5);

        private final int id;

        StructuredTokenKind(int id) {
            this.id = id;
        }

        public int getId() {
            return id;
        }
    }
}
