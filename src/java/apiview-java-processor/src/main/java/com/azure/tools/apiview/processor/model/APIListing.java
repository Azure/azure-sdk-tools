package com.azure.tools.apiview.processor.model;

import com.azure.json.JsonProviders;
import com.azure.json.JsonSerializable;
import com.azure.json.JsonWriter;
import com.azure.tools.apiview.processor.analysers.models.Constants;
import com.azure.tools.apiview.processor.model.maven.Pom;

import java.io.*;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.util.ArrayList;
import java.util.Collections;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.zip.GZIPOutputStream;

import static com.azure.tools.apiview.processor.analysers.models.Constants.*;

public class APIListing implements JsonSerializable<APIListing> {
    private static final String versionString = "26";

    private String name;
    private Language language;
    private LanguageVariant languageVariant;
    private String packageName;
    private String packageVersion;

    private final List<TreeNode> apiForest;
    private final List<Diagnostic> diagnostics;
    private final Map<String, String> knownTypes;

    private final Map<String, String> typeToPackageNameMap;
    private Pom mavenPom;
    private ApiViewProperties apiViewProperties;

    public APIListing() {
        this.diagnostics = new ArrayList<>();
        this.knownTypes = new HashMap<>();
        this.typeToPackageNameMap = new HashMap<>();
        this.apiForest = new ArrayList<>();
        this.apiViewProperties = new ApiViewProperties();
    }

    public void setReviewName(final String name) {
        this.name = name;
    }

    public void addTreeNode(TreeNode node) {
        if (node != null) {
            node.setApiListing(this);
        }
        this.apiForest.add(node);
    }

    public List<TreeNode> getApiForest() {
        return Collections.unmodifiableList(apiForest);
    }

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

    @Override
    public JsonWriter toJson(JsonWriter jsonWriter) throws IOException {
        return jsonWriter.writeStartObject()
            // Version?
            .writeStringField(JSON_NAME_VERSION_STRING, versionString)
            .writeStringField(JSON_NAME_NAME, name)
            .writeStringField(JSON_NAME_LANGUAGE, language.toString())
            .writeStringField(JSON_NAME_LANGUAGE_VARIANT, languageVariant.toString())
            .writeStringField(JSON_NAME_PACKAGE_NAME, packageName)
            .writeStringField(JSON_NAME_PACKAGE_VERSION, packageVersion)
            // ServiceName?
            // PackageDisplayName?
            .writeArrayField(JSON_NAME_API_FOREST, apiForest, JsonWriter::writeJson)
            .writeArrayField(JSON_NAME_DIAGNOSTICS, diagnostics, JsonWriter::writeJson)
            .writeEndObject();
    }

    public void toFile(File outputFile, boolean gzipOutput) {
        try {
            // Write out to the filesystem, make the file if it doesn't exist
            if (!outputFile.exists()) {
                if (!outputFile.createNewFile()) {
                    System.out.printf("Failed to create output file %s%n", outputFile);
                }
            }

            OutputStream fileStream = Files.newOutputStream(outputFile.toPath());
            OutputStream outputStream = gzipOutput ? new GZIPOutputStream(fileStream) : fileStream;
            BufferedWriter writer = new BufferedWriter(new OutputStreamWriter(outputStream, StandardCharsets.UTF_8));
            try (JsonWriter jsonWriter = JsonProviders.createWriter(writer)) {
                toJson(jsonWriter);
            }
            System.out.println("Output written to file: " + outputFile);
        } catch (IOException e) {
            e.printStackTrace();
        }
    }
}
