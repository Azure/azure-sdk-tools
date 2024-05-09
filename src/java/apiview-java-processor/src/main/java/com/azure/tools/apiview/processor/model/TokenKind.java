
package com.azure.tools.apiview.processor.model;

import java.util.*;

import static com.azure.tools.apiview.processor.model.TokenKind.StructuredTokenKind.*;

public enum TokenKind {
    TEXT(CONTENT),
    NEW_LINE(LINE_BREAK),
    WHITESPACE(NONE_BREAKING_SPACE),
    PUNCTUATION(CONTENT, "punctuation"),
    KEYWORD(CONTENT, "keyword"),

    // use this if there are no visible tokens with ID on the line but you still want to be able to leave a comment for it
//    LINE_ID_MARKER(5),

    TYPE_NAME(CONTENT, "typeName"),
    MEMBER_NAME(CONTENT, "memberName"),
    STRING_LITERAL(CONTENT, "stringLiteral"),
//    LITERAL(CONTENT, "literal"),

    JAVADOC(CONTENT, "comment", "javadoc") {
        final Map<String, String> propertiesMap = new HashMap<>();
        {
            propertiesMap.put("GroupId", "documentation");
        }

        @Override
        public Map<String, String> getProperties() {
            return propertiesMap;
        }
    },

    // comment is a single line comment
    COMMENT(CONTENT, "comment");

    // documentation is JavaDoc that can be hidden or shown in the UI with a checkbox
//    DOCUMENTATION_RANGE_START(CONTENT),
//    DOCUMENTATION_RANGE_END(CONTENT);

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
            for (String renderClass : renderClasses) {
                this.renderClasses.add(renderClass);
            }
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

//    public int getId() {
//        return id;
//    }
//
//    public static TokenKind fromId(int id) {
//        for (TokenKind kind : TokenKind.values()) {
//            if (kind.id == id) {
//                return kind;
//            }
//        }
//        return null;
//    }

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
