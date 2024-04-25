
package com.azure.tools.apiview.processor.model;

import com.azure.json.JsonReader;
import com.azure.json.JsonSerializable;
import com.azure.json.JsonWriter;

import java.io.IOException;

public class Token implements JsonSerializable<Token> {
    private String definitionId;
    private String navigateToId;
    private TokenKind kind;
    private String value;
    private String crossLanguageDefinitionId;

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

    public String getCrossLanguageDefinitionId() {
        return crossLanguageDefinitionId;
    }

    /**
     * This is used to link tokens back to TypeSpec definitions, and therefore, to other languages that have been
     * generated from the same TypeSpec.
     */
    public void setCrossLanguageDefinitionId(String crossLanguageDefinitionId) {
        this.crossLanguageDefinitionId = crossLanguageDefinitionId;
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

    @Override
    public JsonWriter toJson(JsonWriter jsonWriter) throws IOException {
        jsonWriter.writeStartObject()
            .writeStringField("DefinitionId", definitionId)
            .writeStringField("NavigateToId", navigateToId);

        if (kind != null) {
            jsonWriter.writeIntField("Kind", kind.getId());
        }

        return jsonWriter.writeStringField("Value", value)
            .writeStringField("CrossLanguageDefinitionId", crossLanguageDefinitionId)
            .writeEndObject();
    }

    public static Token fromJson(JsonReader jsonReader) throws IOException {
        return jsonReader.readObject(reader -> {
            String definitionId = null;
            String navigateToId = null;
            TokenKind kind = null;
            String value = null;
            String crossLanguageDefinitionId = null;

            while (reader.nextToken() != null) {
                String fieldName = reader.getFieldName();
                reader.nextToken();

                if (fieldName.equals("DefinitionId")) {
                    definitionId = reader.getString();
                } else if (fieldName.equals("NavigateToId")) {
                    navigateToId = reader.getString();
                } else if (fieldName.equals("Kind")) {
                    kind = TokenKind.fromId(reader.getInt());
                } else if (fieldName.equals("Value")) {
                    value = reader.getString();
                } else if (fieldName.equals("CrossLanguageDefinitionId")) {
                    crossLanguageDefinitionId = reader.getString();
                } else {
                    reader.skipChildren();
                }
            }

            Token token = new Token(kind, value, definitionId);
            token.setNavigateToId(navigateToId);
            token.setCrossLanguageDefinitionId(crossLanguageDefinitionId);

            return token;
        });
    }
}
