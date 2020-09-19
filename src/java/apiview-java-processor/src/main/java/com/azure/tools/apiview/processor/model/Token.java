package com.azure.tools.apiview.processor.model;

import com.fasterxml.jackson.annotation.JsonProperty;

public class Token {
    @JsonProperty("DefinitionId")
    private String definitionId;

    @JsonProperty("NavigateToId")
    private String navigateToId;

    @JsonProperty("Kind")
    private TokenKind kind;

    @JsonProperty("Value")
    private String value;

    public Token(final TokenKind kind) {
        this(kind, null);
    }

    public Token(final TokenKind kind, final String value) {
        this(kind, value, null);
    }

    public Token(final TokenKind kind, final String value, final String definitionId) {
        this.kind = kind;
        this.value = value;
        this.definitionId = definitionId;
    }

    public String getDefinitionId() {
        return definitionId;
    }

    public void setDefinitionId(String definitionId) {
        this.definitionId = definitionId;
    }

    public String getNavigateToId() {
        return navigateToId;
    }

    public void setNavigateToId(String navigateToId) {
        this.navigateToId = navigateToId;
    }

    public TokenKind getKind() {
        return kind;
    }

    public void setKind(TokenKind kind) {
        this.kind = kind;
    }

    public String getValue() {
        return value;
    }

    public void setValue(String Value) {
        this.value = Value;
    }

    @Override
    public String toString() {
        return "Token [definitionId = "+definitionId+", navigateToId = "+navigateToId+", kind = "+kind+", value = "+value+"]";
    }
}
