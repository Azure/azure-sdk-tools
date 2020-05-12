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
    COMMENT(10);

    private final int id;

    TokenKind(int id) {
        this.id = id;
    }

    @JsonValue
    public int getId() {
        return id;
    }
}
