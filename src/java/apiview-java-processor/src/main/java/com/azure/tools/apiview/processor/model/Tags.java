package com.azure.tools.apiview.processor.model;

import com.azure.json.JsonReader;
import com.azure.json.JsonSerializable;
import com.azure.json.JsonToken;
import com.azure.json.JsonWriter;

import java.io.IOException;

public class Tags implements JsonSerializable<Tags> {
    private TypeKind typeKind;

    public Tags(TypeKind typeKind) {
        this.typeKind = typeKind;
    }

    public TypeKind getTypeKind() {
        return typeKind;
    }

    public void setTypeKind(TypeKind TypeKind) {
        this.typeKind = TypeKind;
    }

    @Override
    public String toString() {
        return "Tags [typeKind = "+ typeKind +"]";
    }

    @Override
    public JsonWriter toJson(JsonWriter jsonWriter) throws IOException {
        jsonWriter.writeStartObject();

        if (typeKind != null) {
            jsonWriter.writeStringField("TypeKind", typeKind.getName());
        }

        return jsonWriter.writeEndObject();
    }

    public static Tags fromJson(JsonReader jsonReader) throws IOException {
        return jsonReader.readObject(reader -> {
            TypeKind typeKind = null;

            while (reader.nextToken() != JsonToken.END_OBJECT) {
                String fieldName = reader.getFieldName();
                reader.nextToken();

                if (fieldName.equals("TypeKind")) {
                    typeKind = TypeKind.fromName(reader.getString());
                } else {
                    reader.skipChildren();
                }
            }
            return new Tags(typeKind);
        });
    }
}
