package com.azure.tools.apiview.processor.model;

import com.fasterxml.jackson.annotation.JsonValue;

public enum TokenKind {
    TEXT(0),
    NEW_LINE(1),
    WHITESPACE(2),
    PUNCTUATION(3),
    KEYWORD(4),

    // use this if there are no visible tokens with ID on the line but you still want to be able to leave a comment for it
    LINE_ID_MARKER(5),

    TYPE_NAME(6),
    MEMBER_NAME(7),
    STRING_LITERAL(8),
    LITERAL(9),

    // comment is a single line comment
    COMMENT(10),

    // documentation is JavaDoc that can be hidden or shown in the UI with a checkbox
    DOCUMENTATION_RANGE_START(11),
    DOCUMENTATION_RANGE_END(12),

    // for types and members that are marked as deprecated
    DEPRECATED_RANGE_START(13),
    DEPRECATED_RANGE_END(14),

    // for any metadata that should not be compared when checking diff
    SKIP_DIFF_START(15),
    SKIP_DIFF_END(16),

    // for external links
    EXTERNAL_LINK_START(28),
    EXTERNAL_LINK_END(29);

    private final int id;

    TokenKind(int id) {
        this.id = id;
    }

    @JsonValue
    public int getId() {
        return id;
    }
}
