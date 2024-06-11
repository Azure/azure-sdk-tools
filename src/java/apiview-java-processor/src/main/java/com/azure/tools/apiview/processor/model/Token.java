package com.azure.tools.apiview.processor.model;

import com.azure.json.JsonSerializable;
import com.azure.json.JsonWriter;

import java.io.IOException;
import java.util.*;

import static com.azure.tools.apiview.processor.model.TokenKind.StructuredTokenKind;
import static com.azure.tools.apiview.processor.analysers.models.Constants.*;
import static com.azure.tools.apiview.processor.model.TokenKind.StructuredTokenKind.*;

public class Token implements JsonSerializable<Token> {

    // The token value which will be displayed.
    private final List<String> value;

    // which will be used to navigate and find token on page.
    private final String id;

    // Could be: LineBreak NoneBreakingSpace TabSpace ParameterSeparator Content
    // All tokens should be content except for spacing tokens.
    // ParameterSeparator should be used between method or function parameters. Spacing token dont need to have value.
    private final StructuredTokenKind structuredTokenKind;

    // for things like deprecated, hidden, etc
    private final Set<String> tags;

    // Capture any other interesting data here. e.g Use GroupId : documentation to group consecutive tokens.
    private final Map<String, String> properties;

    // Add css classes for how the tokens will be rendered. To avoid collision between languages use a language prefix
    // for you classes. e.g csKeyword , jsModule, pyModule
    private final Set<RenderClass> renderClasses;

    public Token(TokenKind tokenKind, String value) {
        this(tokenKind, value, null);
    }

    public Token(TokenKind tokenKind, String value, String id) {
        this.value = value == null ? new ArrayList<>() : new ArrayList<>(Collections.singletonList(value));
        this.id = id;
        this.tags = new HashSet<>();

        if (tokenKind == null) {
            throw new NullPointerException("tokenKind cannot be null");
        }

        this.structuredTokenKind = tokenKind.getStructuredTokenKind();
        this.renderClasses = new HashSet<>(tokenKind.getRenderClasses());
        this.properties = new HashMap<>(tokenKind.getProperties());
    }

    public StructuredTokenKind getKind() {
        return structuredTokenKind;
    }

    public Token addValue(String value) {
        this.value.add(value);
        return this;
    }

    public String getId() {
        return id;
    }

    public Token addRenderClass(RenderClass renderClass) {
        renderClasses.add(Objects.requireNonNull(renderClass));
        return this;
    }

    public Token addProperty(String key, String value) {
        properties.put(Objects.requireNonNull(key), Objects.requireNonNull(value));
        return this;
    }

    public Token addTag(String tag) {
        tags.add(Objects.requireNonNull(tag));
        return this;
    }

    @Override
    public JsonWriter toJson(JsonWriter jsonWriter) throws IOException {
        jsonWriter.writeStartObject()
            .writeStringField(JSON_NAME_ID, id)
            .writeIntField(JSON_NAME_KIND, structuredTokenKind.getId());

        // Small file size optimisation - don't write the value if the structured token is formatting token kind.
        // (The renderer knows to place a single space, newline, etc in its place).
        if (!EnumSet.of(LINE_BREAK, NONE_BREAKING_SPACE, TAB_SPACE, PARAMETER_SEPARATOR).contains(structuredTokenKind)) {
            if (value.size() == 1) {
                jsonWriter.writeStringField(JSON_NAME_VALUE, value.getFirst());
            } else {
                jsonWriter.writeArrayField(JSON_NAME_GROUP_VALUE, value, JsonWriter::writeString);
            }
        }

        if (properties != null && !properties.isEmpty()) {
            jsonWriter.writeMapField(JSON_NAME_PROPERTIES, properties, JsonWriter::writeString);
        }

        if (!tags.isEmpty()) {
            jsonWriter.writeArrayField(JSON_NAME_TAGS, tags, JsonWriter::writeString);
        }

        if (renderClasses != null && !renderClasses.isEmpty()) {
            jsonWriter.writeArrayField(JSON_NAME_RENDER_CLASSES, renderClasses, (jw, rc) -> jw.writeString(rc.getValue()));
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
