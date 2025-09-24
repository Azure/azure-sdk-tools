package com.azure.tools.apiview.processor.diff.dto;

import com.azure.json.JsonWriter;

import java.io.IOException;
import java.util.Map;

/**
 * Represents a single API change between two revisions.
 */
public class ApiChangeDto {
    private String changeType; // e.g. AddedMethod, RemovedMethod, AddedClass, RemovedClass, ModifiedMethod, ModifiedField
    private String before;     // optional textual representation of prior symbol (null for additions)
    private String after;      // optional textual representation of new symbol (null for removals)
    private Meta meta = new Meta();
    private String category;   // optional grouping (Parameters, ReturnType, Visibility, Deprecation, etc.)

    public static class Meta {
        private String symbolKind;   // Class, Method, Field
        private String fqn;          // Fully qualified class or member owner FQN
        private String methodName;   // For methods
        private String fieldName;    // For fields
        private String signature;    // Canonical signature for method/constructor
        private String visibility;   // public/protected
        private String returnType;   // For methods
        private String[] parameterTypes; // Erased param type list
        private String[] parameterNames; // Parameter names (old or new depending on context)
        private Boolean deprecated;      // Nullable
        private Boolean paramNameChange; // true if param names changed but types same
        private Map<String, String> extra; // Future extensibility

        // Getters / setters
        public String getSymbolKind() { return symbolKind; }
        public Meta setSymbolKind(String v) { this.symbolKind = v; return this; }
        public String getFqn() { return fqn; }
        public Meta setFqn(String v) { this.fqn = v; return this; }
        public String getMethodName() { return methodName; }
        public Meta setMethodName(String v) { this.methodName = v; return this; }
        public String getFieldName() { return fieldName; }
        public Meta setFieldName(String v) { this.fieldName = v; return this; }
        public String getSignature() { return signature; }
        public Meta setSignature(String v) { this.signature = v; return this; }
        public String getVisibility() { return visibility; }
        public Meta setVisibility(String v) { this.visibility = v; return this; }
        public String getReturnType() { return returnType; }
        public Meta setReturnType(String v) { this.returnType = v; return this; }
        public String[] getParameterTypes() { return parameterTypes; }
        public Meta setParameterTypes(String[] v) { this.parameterTypes = v; return this; }
        public String[] getParameterNames() { return parameterNames; }
        public Meta setParameterNames(String[] v) { this.parameterNames = v; return this; }
        public Boolean getDeprecated() { return deprecated; }
        public Meta setDeprecated(Boolean v) { this.deprecated = v; return this; }
        public Boolean getParamNameChange() { return paramNameChange; }
        public Meta setParamNameChange(Boolean v) { this.paramNameChange = v; return this; }
        public Map<String, String> getExtra() { return extra; }
        public Meta setExtra(Map<String, String> v) { this.extra = v; return this; }
    }

    public void write(JsonWriter writer) throws IOException {
        writer.writeStartObject();
        writer.writeStringField("changeType", changeType);
        if (before != null) writer.writeStringField("before", before);
        if (after != null) writer.writeStringField("after", after);
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

    // Getters / setters for top-level fields
    public String getChangeType() { return changeType; }
    public ApiChangeDto setChangeType(String v) { this.changeType = v; return this; }
    public String getBefore() { return before; }
    public ApiChangeDto setBefore(String v) { this.before = v; return this; }
    public String getAfter() { return after; }
    public ApiChangeDto setAfter(String v) { this.after = v; return this; }
    public Meta getMeta() { return meta; }
    public ApiChangeDto setMeta(Meta m) { this.meta = m; return this; }
    public String getCategory() { return category; }
    public ApiChangeDto setCategory(String v) { this.category = v; return this; }
}
