package com.azure.tools.apiview.processor.diff.dto;

import com.azure.json.JsonWriter;
import java.io.IOException;
import java.util.List;
import java.util.ArrayList;

/**
 * Container for a list of ApiChangeDto plus schema metadata.
 */
public class ApiDiffResult {
    private final List<ApiChangeDto> changes = new ArrayList<ApiChangeDto>();

    public void write(JsonWriter writer) throws IOException {
        writer.writeStartObject();
        writer.writeFieldName("changes");
        writer.writeStartArray();
        for (ApiChangeDto change : changes) {
            change.write(writer);
        }
        writer.writeEndArray();
        writer.writeEndObject();
    }

    public List<ApiChangeDto> getChanges() {
        return changes;
    }

    public ApiDiffResult addChange(ApiChangeDto change) {
        if (change != null) {
            changes.add(change);
        }
        return this;
    }
}
