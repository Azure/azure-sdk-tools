package com.azure.tools.apiview.processor.model;

import com.azure.json.JsonReader;
import com.azure.json.JsonSerializable;
import com.azure.json.JsonToken;
import com.azure.json.JsonWriter;
import com.azure.tools.apiview.processor.model.maven.Pom;

import java.io.IOException;
import java.util.ArrayList;
import java.util.Collections;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.Objects;

public class APIListing implements JsonSerializable<APIListing> {
    private List<ChildItem> navigation;
    private ChildItem rootNav;
    private String name;
    private String language;
    private LanguageVariant languageVariant;
    private String packageName;
    private String packageVersion;

    // This string is taken from here:
    // https://github.com/Azure/azure-sdk-tools/blob/main/src/dotnet/APIView/APIView/Languages/CodeFileBuilder.cs#L50
    private final String versionString = "21";
    private List<Token> tokens;
    private List<Diagnostic> diagnostics;
    private Map<String, String> knownTypes;

    // a map of package names to a list of types within that package
    private final Map<String, List<String>> packageNamesToTypesMap;
    private final Map<String, String> typeToPackageNameMap;
    private Pom mavenPom;
    private ApiViewProperties apiViewProperties;

    public APIListing() {
        this.diagnostics = new ArrayList<>();
        this.knownTypes = new HashMap<>();
        this.packageNamesToTypesMap = new HashMap<>();
        this.typeToPackageNameMap = new HashMap<>();
        this.navigation = new ArrayList<>();
        this.apiViewProperties = new ApiViewProperties();
    }

    public void setReviewName(final String name) {
        this.name = name;
        this.rootNav = new ChildItem(name, TypeKind.ASSEMBLY);
        this.navigation.add(rootNav);
    }

    public void addChildItem(ChildItem childItem) {
        this.rootNav.addChildItem(childItem);
    }

    public void addChildItem(String packageName, ChildItem childItem) {
        this.rootNav.addChildItem(packageName, childItem);
    }

    public void addDiagnostic(Diagnostic diagnostic) {
        this.diagnostics.add(diagnostic);
    }

    public List<Diagnostic> getDiagnostics() {
        return Collections.unmodifiableList(diagnostics);
    }

    public String getLanguage() {
        return language;
    }

    public void setLanguage(final String language) {
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

    public List<Token> getTokens() {
        return tokens;
    }

    public void setTokens(List<Token> tokens) {
        this.tokens = tokens;
    }

    @Override
    public String toString() {
        return "APIListing [rootNav = " + rootNav + ", Name = " + name + ", Tokens = " + tokens + "]";
    }

    /**
     * Returns a map of type name to unique identifier, used for navigation.
     */
    public Map<String, String> getKnownTypes() {
        return knownTypes;
    }

    public void addPackageTypeMapping(String packageName, String typeName) {
        packageNamesToTypesMap.computeIfAbsent(packageName, name -> new ArrayList<>()).add(typeName);
        typeToPackageNameMap.put(typeName, packageName);
    }

    public Map<String, List<String>> getPackageNamesToTypesMap() {
        return packageNamesToTypesMap;
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
            .writeArrayField("Navigation", navigation, JsonWriter::writeJson)
            .writeStringField("Name", name)
            .writeStringField("Language", language)
            .writeStringField("LanguageVariant", Objects.toString(languageVariant, null))
            .writeStringField("PackageName", packageName)
            .writeStringField("PackageVersion", packageVersion)
            .writeStringField("VersionString", versionString)
            .writeArrayField("Tokens", tokens, JsonWriter::writeJson)
            .writeArrayField("Diagnostics", diagnostics, JsonWriter::writeJson)
            .writeEndObject();
    }

    public static APIListing fromJson(JsonReader jsonReader) throws IOException {
        return jsonReader.readObject(reader -> {
            APIListing apiListing = new APIListing();

            while (reader.nextToken() != JsonToken.END_OBJECT) {
                String fieldName = reader.getFieldName();
                reader.nextToken();

                if ("Navigation".equals(fieldName)) {
                    apiListing.navigation = reader.readArray(ChildItem::fromJson);
                } else if ("Name".equals(fieldName)) {
                    apiListing.name = reader.getString();
                } else if ("Language".equals(fieldName)) {
                    apiListing.language = reader.getString();
                } else if ("LanguageVariant".equals(fieldName)) {
                    apiListing.languageVariant = LanguageVariant.fromString(reader.getString());
                } else if ("PackageName".equals(fieldName)) {
                    apiListing.packageName = reader.getString();
                } else if ("PackageVersion".equals(fieldName)) {
                    apiListing.packageVersion = reader.getString();
                } else if ("Tokens".equals(fieldName)) {
                    apiListing.tokens = reader.readArray(Token::fromJson);
                } else if ("Diagnostics".equals(fieldName)) {
                    apiListing.diagnostics = reader.readArray(Diagnostic::fromJson);
                } else {
                    reader.skipChildren();
                }
            }

            return apiListing;
        });
    }
}
