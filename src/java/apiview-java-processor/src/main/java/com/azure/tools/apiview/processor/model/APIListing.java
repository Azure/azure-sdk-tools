package com.azure.tools.apiview.processor.model;

import com.fasterxml.jackson.annotation.JsonIgnore;
import com.fasterxml.jackson.annotation.JsonProperty;

import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.Map;

public class APIListing {
    @JsonProperty("Navigation")
    private List<ChildItem> childItems;

    @JsonProperty("Name")
    private String Name;

    @JsonProperty("Tokens")
    private List<Token> tokens;

    @JsonProperty("Diagnostics")
    private List<Diagnostic> diagnostics;

    @JsonIgnore
    private Map<String, String> knownTypes;

    // a map of package names to a list of types within that package
    @JsonIgnore
    private final Map<String, List<String>> packageNamesToTypesMap;

    @JsonIgnore
    private final Map<String, String> typeToPackageNameMap;

    public APIListing() {
        this.childItems = new ArrayList<>();
        this.diagnostics = new ArrayList<>();
        this.knownTypes = new HashMap<>();
        this.packageNamesToTypesMap = new HashMap<>();
        this.typeToPackageNameMap = new HashMap<>();
    }

    public List<ChildItem> getNavigation() {
        return childItems;
    }

    public void addChildItem(ChildItem childItem) {
        this.childItems.add(childItem);
    }

    public void addDiagnostic(Diagnostic diagnostic) {
        this.diagnostics.add(diagnostic);
    }

    public void setNavigation(List<ChildItem> childItems) {
        this.childItems = childItems;
    }

    public String getName() {
        return Name;
    }

    public void setName(String Name) {
        this.Name = Name;
    }

    public List<Token> getTokens() {
        return tokens;
    }

    public void setTokens(List<Token> tokens) {
        this.tokens = tokens;
    }

    @Override
    public String toString() {
        return "APIListing [childItems = "+childItems+", Name = "+Name+", Tokens = "+tokens+"]";
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
}