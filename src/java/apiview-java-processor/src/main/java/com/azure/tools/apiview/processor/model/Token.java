package com.azure.tools.apiview.processor.model;

import com.azure.json.JsonSerializable;
import com.azure.json.JsonWriter;

import java.io.IOException;
import java.util.HashSet;
import java.util.Map;
import java.util.Set;

public class Token implements JsonSerializable<Token> {
    // The token value which will be displayed.
    private String value;

    // which will be used to navigate and find token on page.
    private String id;

    // Could be: LineBreak NoneBreakingSpace TabSpace ParameterSeparator Content
    // All tokens should be content except for spacing tokens.
    // ParameterSeparator should be used between method or function parameters. Spacing token dont need to have value.
    private final int structuredTokenKind;

    // Capture any other interesting data here. e.g Use GroupId : documentation to group consecutive tokens.
    private final Map<String, String> properties;

    // Add css classes for how the tokens will be rendered. To avoid collision between languages use a language prefix
    // for you classes. e.g csKeyword , jsModule, pyModule
    private final Set<String> renderClasses;

    public Token(TokenKind tokenKind) {
        this(tokenKind, null, null);
    }

    public Token(TokenKind tokenKind, String value) {
        this(tokenKind, value, null);
    }

    public Token(TokenKind tokenKind, String value, String id) {
        this.value = value;
        this.id = id;

        if (tokenKind == null) {
            throw new NullPointerException("tokenKind cannot be null");
        }

        this.structuredTokenKind = tokenKind.getStructuredTokenKind();
        this.renderClasses = new HashSet<>(tokenKind.getRenderClasses());
        this.properties = tokenKind.getProperties();
    }

    public String getValue() {
        return value;
    }

    public String getId() {
        return id;
    }

    public void addRenderClass(String renderClass) {
        renderClasses.add(renderClass);
    }

    @Override
    public JsonWriter toJson(JsonWriter jsonWriter) throws IOException {
        jsonWriter.writeStartObject()
            .writeStringField("Value", value)
            .writeStringField("Id", id)
            .writeIntField("Kind", structuredTokenKind);

        if (properties != null && !properties.isEmpty()) {
            jsonWriter.writeMapField("Properties", properties, JsonWriter::writeString);
        }

        if (renderClasses != null && !renderClasses.isEmpty()) {
            jsonWriter.writeArrayField("RenderClasses", renderClasses, JsonWriter::writeString);
        }

        return jsonWriter.writeEndObject();
    }

    @Override
    public String toString() {
        return "Token{" +
                "value='" + value + '\'' +
                ", id='" + id + '\'' +
                ", structuredTokenKind=" + structuredTokenKind +
                ", properties=" + properties +
                ", renderClasses=" + renderClasses +
                '}';
    }
}
