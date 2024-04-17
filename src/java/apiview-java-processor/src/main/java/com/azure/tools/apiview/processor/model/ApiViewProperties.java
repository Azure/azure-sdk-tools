package com.azure.tools.apiview.processor.model;

import com.azure.json.JsonReader;
import com.azure.json.JsonSerializable;
import com.azure.json.JsonToken;
import com.azure.json.JsonWriter;

import java.io.IOException;
import java.util.Collections;
import java.util.HashMap;
import java.util.Map;
import java.util.Optional;

/**
 * Sometimes libraries carry additional metadata with them that can make the output from APIView more useful. This
 * class is used to store that metadata, as it is deserialized from the /META-INF/apiview_properties.json file.
 */
public class ApiViewProperties implements JsonSerializable<ApiViewProperties> {
    private Flavor flavor;

    // This is a map of model names and methods to their TypeSpec definition IDs.
    private Map<String, String> crossLanguageDefinitionIds = new HashMap<>();

    /**
     * Cross Languages Definition ID is used to map from a model name or a method name to a TypeSpec definition ID. This
     * is used to enable cross-language linking, to make review time quicker as reviewers can jump between languages to
     * see how the API is implemented in each language.
     */
    public Optional<String> getCrossLanguageDefinitionId(String fullyQualifiedName) {
        return Optional.ofNullable(crossLanguageDefinitionIds.get(fullyQualifiedName));
    }

    /**
     * Returns an unmodifiable map of all the cross-language definition IDs.
     */
    public Map<String, String> getCrossLanguageDefinitionIds() {
        return Collections.unmodifiableMap(crossLanguageDefinitionIds);
    }

    public Flavor getFlavor() {
        return flavor;
    }

    @Override
    public JsonWriter toJson(JsonWriter jsonWriter) throws IOException {
        jsonWriter.writeStartObject()
            .writeMapField("CrossLanguageDefinitionId", crossLanguageDefinitionIds, JsonWriter::writeString);

        if (flavor != null) {
            jsonWriter.writeStringField("flavor", flavor.getSerializationValue());
        }

        return jsonWriter.writeEndObject();
    }

    public static ApiViewProperties fromJson(JsonReader jsonReader) throws IOException {
        return jsonReader.readObject(reader -> {
            ApiViewProperties properties = new ApiViewProperties();

            while (reader.nextToken() != JsonToken.END_OBJECT) {
                String fieldName = reader.getFieldName();
                reader.nextToken();

                if ("CrossLanguageDefinitionId".equals(fieldName)) {
                    properties.crossLanguageDefinitionIds = reader.readMap(JsonReader::getString);
                } else if ("flavor".equals(fieldName)) {
                    properties.flavor = Flavor.fromSerializationValue(reader.getString());
                } else {
                    reader.skipChildren();
                }
            }

            return properties;
        });
    }
}
