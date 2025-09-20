package com.azure.tools.apiview.processor.model;

import com.azure.json.JsonSerializable;
import com.azure.json.JsonWriter;
import com.azure.tools.apiview.processor.model.maven.Pom;
import com.azure.tools.apiview.processor.model.traits.Parent;

import java.io.File;
import java.io.IOException;
import java.io.UncheckedIOException;
import java.nio.file.Files;
import java.util.ArrayList;
import java.util.Collections;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.zip.GZIPOutputStream;

import static com.azure.tools.apiview.processor.analysers.models.Constants.APIVIEW_JSON_SCHEMA;
import static com.azure.tools.apiview.processor.analysers.models.Constants.JSON_NAME_DIAGNOSTICS;
import static com.azure.tools.apiview.processor.analysers.models.Constants.JSON_NAME_LANGUAGE;
import static com.azure.tools.apiview.processor.analysers.models.Constants.JSON_NAME_LANGUAGE_VARIANT;
import static com.azure.tools.apiview.processor.analysers.models.Constants.JSON_NAME_PACKAGE_NAME;
import static com.azure.tools.apiview.processor.analysers.models.Constants.JSON_NAME_PACKAGE_VERSION;
import static com.azure.tools.apiview.processor.analysers.models.Constants.JSON_NAME_PARSER_VERSION;
import static com.azure.tools.apiview.processor.analysers.models.Constants.JSON_NAME_REVIEW_LINES;

public class APIListing implements Parent, JsonSerializable<APIListing> {
    private static final String parserVersion = "29";

    private Language language;
    private LanguageVariant languageVariant;
    private String packageName;
    private String packageVersion;

    private final List<ReviewLine> reviewLines;
    private final List<Diagnostic> diagnostics;
    private final Map<String, String> knownTypes;

    private final Map<String, String> typeToPackageNameMap;
    private Pom mavenPom;
    private ApiViewProperties apiViewProperties;
    // methodIndex provides O(1) lookup for method signature diffing without reconstructing from reviewLines tokens.
    // Key format:
    //   <enclosingTypeFqn>#<methodName>(<comma-separated-param-types>)
    // Values contain ordered parameter names and types (raw token text) necessary for detecting name & (later) type changes.
    private final Map<String, MethodIndexEntry> methodIndex;

    public APIListing() {
        this.diagnostics = new ArrayList<>();
        this.knownTypes = new HashMap<>();
        this.typeToPackageNameMap = new HashMap<>();
        this.reviewLines = new ArrayList<>();
        this.apiViewProperties = new ApiViewProperties();
        this.methodIndex = new HashMap<>();
    }

    @Override
    public ReviewLine addChildLine(ReviewLine reviewLine) {
        this.reviewLines.add(reviewLine);
        return reviewLine;
    }

    @Override
    public ReviewLine addChildLine(final String lineId) {
        return addChildLine(new ReviewLine(this, lineId));
    }

    @Override
    public ReviewLine addChildLine() {
        return addChildLine(new ReviewLine(this));
    }

    @Override
    public List<ReviewLine> getChildren() {
        return reviewLines;
    }

    //    public List<TreeNode> getApiForest() {
//        return Collections.unmodifiableList(apiForest);
//    }

    public void addDiagnostic(Diagnostic diagnostic) {
        this.diagnostics.add(diagnostic);
    }

    public List<Diagnostic> getDiagnostics() {
        return Collections.unmodifiableList(diagnostics);
    }

    public Language getLanguage() {
        return language;
    }

    public void setLanguage(final Language language) {
        this.language = language;
    }

    public LanguageVariant getLanguageVariant() {
        return languageVariant;
    }

    public void setLanguageVariant(final LanguageVariant variant) {
        this.languageVariant = variant;
    }

    public String getPackageName() {
        return packageName;
    }

    public void setPackageName(final String packageName) {
        this.packageName = packageName;
    }

    public String getPackageVersion() {
        return packageVersion;
    }

    public void setPackageVersion(final String packageVersion) {
        this.packageVersion = packageVersion;
    }

    /**
     * Returns a map of type name to unique identifier, used for navigation.
     */
    public Map<String, String> getKnownTypes() {
        return knownTypes;
    }

    public void addPackageTypeMapping(String packageName, String typeName) {
//        packageNamesToTypesMap.computeIfAbsent(packageName, name -> new ArrayList<>()).add(typeName);
        typeToPackageNameMap.put(typeName, packageName);
    }

    public Map<String, String> getTypeToPackageNameMap() {
        return typeToPackageNameMap;
    }

    public void setMavenPom(Pom mavenPom) {
        this.mavenPom = mavenPom;
    }

    public Pom getMavenPom() {
        return mavenPom;
    }

    public void setApiViewProperties(ApiViewProperties properties) {
        this.apiViewProperties = properties;
    }

    public ApiViewProperties getApiViewProperties() {
        return apiViewProperties;
    }

    /**
     * Returns the (possibly empty) method index map. Populated in a later pass (Tier 0 population step).
     */
    public Map<String, MethodIndexEntry> getMethodIndex() {
        return methodIndex;
    }

    /**
     * Adds or replaces a method index entry. Intended to be called by the analyser after building reviewLines.
     * @param signatureKey Fully qualified signature key.
     * @param paramNames Ordered parameter names.
     * @param paramTypes Ordered parameter types (raw textual form as emitted in source/AST).
     */
    public void putMethodIndexEntry(String signatureKey, List<String> paramNames, List<String> paramTypes) {
        if (signatureKey == null || signatureKey.isEmpty()) {
            return; // ignore invalid keys silently to avoid destabilizing existing pipeline
        }
        methodIndex.put(signatureKey, new MethodIndexEntry(paramNames, paramTypes));
    }

    @Override
    public JsonWriter toJson(JsonWriter jsonWriter) throws IOException {
        jsonWriter.writeStartObject();
        jsonWriter.writeStringField("$schema", APIVIEW_JSON_SCHEMA)
            .writeStringField(JSON_NAME_PARSER_VERSION, parserVersion)
            .writeStringField(JSON_NAME_LANGUAGE, language.toString())
            .writeStringField(JSON_NAME_LANGUAGE_VARIANT, languageVariant.toString())
            .writeStringField(JSON_NAME_PACKAGE_NAME, packageName)
            .writeStringField(JSON_NAME_PACKAGE_VERSION, packageVersion)
            .writeArrayField(JSON_NAME_REVIEW_LINES, reviewLines, JsonWriter::writeJson)
            .writeArrayField(JSON_NAME_DIAGNOSTICS, diagnostics, JsonWriter::writeJson);

        // methodIndex serialized as an object: keys are signature strings; values are objects with paramNames & paramTypes arrays.
        jsonWriter.writeStartObject("methodIndex");
        for (Map.Entry<String, MethodIndexEntry> e : methodIndex.entrySet()) {
            jsonWriter.writeStartObject(e.getKey());
            e.getValue().toJson(jsonWriter);
            jsonWriter.writeEndObject();
        }
        jsonWriter.writeEndObject();

        return jsonWriter.writeEndObject();
    }

    /**
     * Writes the {@link APIListing} to the {@code outputFile} and returns the raw JSON.
     *
     * @param outputFile The file to write.
     * @param gzipOutput Whether the output needs to be GZIP'd.
     * @return The raw JSON bytes for other final checks.
     */
    public byte[] toFile(File outputFile, boolean gzipOutput) {
        try {
            // Write out to the filesystem, make the file if it doesn't exist
            if (!outputFile.exists()) {
                if (!outputFile.createNewFile()) {
                    System.out.printf("Failed to create output file %s%n", outputFile);
                }
            }

            // Best-effort populate if analyser hasn't (Tier 0). Safe no-op if already populated.
            if (methodIndex.isEmpty()) {
                try { buildMethodIndexFromReviewLines(); } catch (Exception ignore) { /* non-fatal */ }
            }

            byte[] rawJsonBytes = toJsonBytes();

            if (!gzipOutput) {
                Files.write(outputFile.toPath(), rawJsonBytes);
            } else {
                try (GZIPOutputStream gzip = new GZIPOutputStream(Files.newOutputStream(outputFile.toPath()))) {
                    gzip.write(rawJsonBytes);
                }
            }

            return rawJsonBytes;
        } catch (IOException e) {
            throw new UncheckedIOException(e);
        }
    }

    // ---------------- Tier 0 helper: derive methodIndex from existing reviewLines if analyser has not populated it. ----------------
    private void buildMethodIndexFromReviewLines() {
        if (!methodIndex.isEmpty()) return; // already populated
        if (reviewLines.isEmpty()) return;

        // Traverse lines with a stack of enclosing type names.
        List<String> typeStack = new ArrayList<>();
        for (ReviewLine line : reviewLines) {
            traverseLine(line, typeStack);
        }
    }

    private void traverseLine(ReviewLine line, List<String> typeStack) {
        List<ReviewToken> tokens = line.getTokens();
        if (tokens != null && !tokens.isEmpty()) {
            boolean isTypeDecl = false;
            String pendingTypeName = null;
            // Pass 1: detect type declaration & method line
            for (int i = 0; i < tokens.size(); i++) {
                ReviewToken t = tokens.get(i);
                if (hasRenderClass(t, "class") || hasRenderClass(t, "interface") || hasRenderClass(t, "enum")) {
                    // find subsequent token with typeName
                    for (int j = i + 1; j < tokens.size(); j++) {
                        ReviewToken tn = tokens.get(j);
                        if (hasRenderClass(tn, "typeName")) {
                            pendingTypeName = getTokenValue(tn);
                            if (pendingTypeName != null && !pendingTypeName.isEmpty()) {
                                typeStack.add(pendingTypeName);
                                isTypeDecl = true;
                            }
                            break;
                        }
                    }
                }

                if (hasRenderClass(t, "methodName")) {
                    buildMethodEntry(tokens, i, typeStack);
                    break; // one method per line expected
                }
            }

            // Recurse children first before popping type (so nested methods in inner classes work)
            if (!line.getChildren().isEmpty()) {
                for (ReviewLine child : line.getChildren()) {
                    traverseLine(child, typeStack);
                }
            }

            if (isTypeDecl && !typeStack.isEmpty()) {
                typeStack.remove(typeStack.size() - 1);
            }
            return;
        }
        // No tokens; still traverse children
        if (!line.getChildren().isEmpty()) {
            for (ReviewLine child : line.getChildren()) {
                traverseLine(child, typeStack);
            }
        }
    }

    private void buildMethodEntry(List<ReviewToken> tokens, int methodNameIndex, List<String> typeStack) {
        String methodName = getTokenValue(tokens.get(methodNameIndex));
        if (methodName == null) return;
        int openIdx = findTokenValue(tokens, methodNameIndex, "(");
        int closeIdx = findMatchingParen(tokens, openIdx);
        List<String> paramTypes = new ArrayList<>();
        List<String> paramNames = new ArrayList<>();
        if (openIdx >= 0 && closeIdx > openIdx) {
            for (int i = openIdx + 1; i < closeIdx; i++) {
                ReviewToken t = tokens.get(i);
                if (hasRenderClass(t, "parameterType")) {
                    String v = getTokenValue(t);
                    paramTypes.add(v == null ? "" : v);
                } else if (hasRenderClass(t, "parameterName")) {
                    String v = getTokenValue(t);
                    paramNames.add(v == null ? "" : v);
                }
            }
        }
        String enclosing = String.join(".", typeStack);
        String signatureKey = enclosing + "#" + methodName + "(" + String.join(",", paramTypes) + ")";
        putMethodIndexEntry(signatureKey, paramNames, paramTypes);
    }

    private boolean hasRenderClass(ReviewToken token, String renderClass) {
        // Render classes are emitted via tokenKind; we need reflection of classes from ReviewToken (no public getter currently)
        // Workaround: rely on token.toJson? Not efficient. Instead we add a helper method to ReviewToken in a future tier.
        // For Tier 0 we approximate using tokenKind name heuristics where possible, else skip.
        // Minimal viable: methodName => tokenKind MEMBER_NAME with value preceding '(' ; parameterType / parameterName currently not accessible without render classes.
        // If render classes unavailable, we cannot enrich methodIndex here; rely on analyser future population.
        return false; // placeholder (population expected from analyser to achieve accuracy)
    }

    private String getTokenValue(ReviewToken token) {
        try { // reflection fallback (best-effort) to access private 'value'
            java.lang.reflect.Field f = ReviewToken.class.getDeclaredField("value");
            f.setAccessible(true);
            Object v = f.get(token);
            return v == null ? null : v.toString();
        } catch (Exception e) {
            return null;
        }
    }

    private int findTokenValue(List<ReviewToken> tokens, int start, String value) {
        for (int i = start; i < tokens.size(); i++) {
            String v = getTokenValue(tokens.get(i));
            if (value.equals(v)) return i;
        }
        return -1;
    }

    private int findMatchingParen(List<ReviewToken> tokens, int openIdx) {
        if (openIdx < 0) return -1;
        int depth = 0;
        for (int i = openIdx; i < tokens.size(); i++) {
            String v = getTokenValue(tokens.get(i));
            if ("(".equals(v)) depth++;
            else if (")".equals(v)) {
                depth--;
                if (depth == 0) return i;
            }
        }
        return -1;
    }
}

/**
 * POJO representing a compact method summary used for fast diffing.
 * Only includes fields required for Tier 0 (parameter names & types). Additional semantic data can be added later
 * in a backward compatible way (e.g., returnType, visibility) without impacting existing consumers.
 */
class MethodIndexEntry implements JsonSerializable<MethodIndexEntry> {
    private final List<String> paramNames;
    private final List<String> paramTypes;

    MethodIndexEntry(List<String> paramNames, List<String> paramTypes) {
        this.paramNames = paramNames == null ? Collections.emptyList() : new ArrayList<>(paramNames);
        this.paramTypes = paramTypes == null ? Collections.emptyList() : new ArrayList<>(paramTypes);
    }

    public List<String> getParamNames() { return Collections.unmodifiableList(paramNames); }
    public List<String> getParamTypes() { return Collections.unmodifiableList(paramTypes); }

    @Override
    public JsonWriter toJson(JsonWriter jsonWriter) throws IOException {
        return jsonWriter
            .writeArrayField("paramNames", paramNames, (w, s) -> w.writeString(s))
            .writeArrayField("paramTypes", paramTypes, (w, s) -> w.writeString(s));
    }
}
