package com.azure.tools.apiview.processor.diff.dto;

import com.azure.json.JsonWriter;
import java.io.IOException;
import java.util.Map;

/**
 * Represents a single API change between two revisions.
 * Minimal DTO with manual JSON serialization (azure-json) to avoid reflection needs on Java 8.
 */
public class ApiChangeDto {
    public String changeType; // e.g. AddedMethod, RemovedMethod, AddedClass, RemovedClass, ModifiedMethod, ModifiedField
    public String before;     // optional textual representation of prior symbol (null for additions)
    public String after;      // optional textual representation of new symbol (null for removals)
    public Meta meta = new Meta();
    public String impact;     // optional classification (e.g. Breaking, NonBreaking)
    public String confidence; // optional (e.g. High, Medium, Low)
    public String category;   // optional grouping (Parameters, ReturnType, Visibility, Deprecation, etc.)

    public static class Meta {
        public String symbolKind;   // Class, Method, Field
        public String fqn;          // Fully qualified class or member owner FQN
        public String methodName;   // For methods
        public String fieldName;    // For fields
        public String signature;    // Canonical signature for method/constructor
        public String visibility;   // public/protected
        public String returnType;   // For methods
        public String[] parameterTypes; // Erased param type list
        public String[] parameterNames; // Parameter names (old or new depending on context)
        public Boolean deprecated;      // Nullable
        public Boolean paramNameChange; // true if param names changed but types same
        public Map<String, String> extra; // Future extensibility
    }

    public void write(JsonWriter writer) throws IOException {
        writer.writeStartObject();
        writer.writeStringField("changeType", changeType);
        if (before != null) writer.writeStringField("before", before);
        if (after != null) writer.writeStringField("after", after);
        if (impact != null) writer.writeStringField("impact", impact);
        if (confidence != null) writer.writeStringField("confidence", confidence);
        if (category != null) writer.writeStringField("category", category);
        writeMeta(writer);
        writer.writeEndObject();
    }

    private void writeMeta(JsonWriter writer) throws IOException {
        writer.writeFieldName("meta");
        writer.writeStartObject();
        if (meta.symbolKind != null) writer.writeStringField("symbolKind", meta.symbolKind);
        if (meta.fqn != null) writer.writeStringField("fqn", meta.fqn);
        if (meta.methodName != null) writer.writeStringField("methodName", meta.methodName);
        if (meta.fieldName != null) writer.writeStringField("fieldName", meta.fieldName);
        if (meta.signature != null) writer.writeStringField("signature", meta.signature);
        if (meta.visibility != null) writer.writeStringField("visibility", meta.visibility);
        if (meta.returnType != null) writer.writeStringField("returnType", meta.returnType);
        if (meta.parameterTypes != null) {
            writer.writeFieldName("parameterTypes");
            writer.writeStartArray();
            for (String p : meta.parameterTypes) writer.writeString(p);
            writer.writeEndArray();
        }
        if (meta.parameterNames != null) {
            writer.writeFieldName("parameterNames");
            writer.writeStartArray();
            for (String p : meta.parameterNames) writer.writeString(p);
            writer.writeEndArray();
        }
        if (meta.deprecated != null) writer.writeBooleanField("deprecated", meta.deprecated);
        if (meta.paramNameChange != null) writer.writeBooleanField("paramNameChange", meta.paramNameChange);
        if (meta.extra != null && !meta.extra.isEmpty()) {
            writer.writeFieldName("extra");
            writer.writeStartObject();
            for (Map.Entry<String, String> e : meta.extra.entrySet()) {
                writer.writeStringField(e.getKey(), e.getValue());
            }
            writer.writeEndObject();
        }
        writer.writeEndObject();
    }
}
