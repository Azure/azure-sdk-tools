
package com.azure.tools.apiview.processor.model;

import java.util.*;

import static com.azure.tools.apiview.processor.model.TokenKind.StructuredTokenKind.*;

public enum TokenKind {
    TEXT(CONTENT),
    NEW_LINE(LINE_BREAK),
    WHITESPACE(NONE_BREAKING_SPACE),
    PARAMETER_SEPARATOR(StructuredTokenKind.PARAMETER_SEPARATOR),
    PUNCTUATION(CONTENT, RenderClass.PUNCTUATION),
    KEYWORD(CONTENT, RenderClass.KEYWORD),

    TYPE_NAME(CONTENT, RenderClass.TYPE_NAME),
    STRING_LITERAL(CONTENT, RenderClass.STRING_LITERAL),
    NUMBER(CONTENT, RenderClass.NUMBER),

    PACKAGE_NAME(CONTENT, RenderClass.TYPE_NAME, RenderClass.PACKAGE_NAME),
    MODULE_NAME(CONTENT, RenderClass.TYPE_NAME, RenderClass.MODULE_NAME),

    ENUM_TYPE(CONTENT, RenderClass.TYPE_NAME, RenderClass.ENUM_TYPE),
    ENUM_CONSTANT(CONTENT, RenderClass.TYPE_NAME, RenderClass.ENUM_CONSTANT),

    ANNOTATION_NAME(CONTENT, RenderClass.TYPE_NAME, RenderClass.ANNOTATION_NAME),
    ANNOTATION_PARAMETER_NAME(CONTENT, RenderClass.TYPE_NAME, RenderClass.ANNOTATION_PARAMETER_NAME),
    ANNOTATION_PARAMETER_VALUE(CONTENT, RenderClass.TYPE_NAME, RenderClass.ANNOTATION_PARAMETER_VALUE),

    RETURN_TYPE(CONTENT, RenderClass.TYPE_NAME, RenderClass.RETURN_TYPE),
    PARAMETER_TYPE(CONTENT, RenderClass.TYPE_NAME, RenderClass.PARAMETER_TYPE),
    PARAMETER_NAME(CONTENT, RenderClass.PARAMETER_NAME),
    EXTENDS_TYPE(CONTENT, RenderClass.TYPE_NAME, RenderClass.EXTENDS_TYPE),
    IMPLEMENTS_TYPE(CONTENT, RenderClass.TYPE_NAME, RenderClass.IMPLEMENTS_TYPE),

    METHOD_NAME(CONTENT, RenderClass.MEMBER_NAME, RenderClass.METHOD_NAME),
    FIELD_NAME(CONTENT, RenderClass.MEMBER_NAME, RenderClass.FIELD_NAME),

    MAVEN_KEY(CONTENT, RenderClass.KEYWORD, RenderClass.MAVEN_KEY),
    MAVEN_VALUE(CONTENT, RenderClass.MAVEN_VALUE),
    MAVEN_DEPENDENCY(CONTENT, RenderClass.MAVEN_VALUE, RenderClass.MAVEN_DEPENDENCY),

    JAVADOC(CONTENT, RenderClass.COMMENT, RenderClass.JAVADOC) {
        @Override public Map<String, String> getProperties() {
            return Map.of("GroupId", "doc");
        }
    },

    // comment is a single line comment
    COMMENT(CONTENT, RenderClass.COMMENT),

    URL(StructuredTokenKind.URL);

    private final StructuredTokenKind structuredTokenKind;
    private Set<RenderClass> renderClasses;

    TokenKind(StructuredTokenKind structuredTokenKind, RenderClass... renderClasses) {
        this.structuredTokenKind = structuredTokenKind;

        if (renderClasses != null) {
            this.renderClasses = new LinkedHashSet<>();
            this.renderClasses.addAll(Arrays.asList(renderClasses));
        }
    }

    public StructuredTokenKind getStructuredTokenKind() {
        return structuredTokenKind;
    }

    public Set<RenderClass> getRenderClasses() {
        return renderClasses;
    }

    public Map<String, String> getProperties() {
        return Collections.emptyMap();
    }

    public enum StructuredTokenKind {
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
