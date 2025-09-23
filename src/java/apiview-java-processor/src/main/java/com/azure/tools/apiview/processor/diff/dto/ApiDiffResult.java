package com.azure.tools.apiview.processor.diff.dto;

import com.azure.json.JsonWriter;
import java.io.IOException;
import java.util.List;

/**
 * Container for a list of ApiChangeDto plus schema metadata.
 */
public class ApiDiffResult {
    public List<ApiChangeDto> changes;

    public void write(JsonWriter writer) throws IOException {
        writer.writeStartObject();
        writer.writeFieldName("changes");
        writer.writeStartArray();
        if (changes != null) {
            for (ApiChangeDto change : changes) {
                change.write(writer);
            }
        }
        writer.writeEndArray();
        writer.writeEndObject();
    }
}
