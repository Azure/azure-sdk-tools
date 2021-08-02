package com.azure.tools.apiview.processor.model;

import com.azure.tools.apiview.processor.analysers.JavaASTAnalyser;
import com.fasterxml.jackson.annotation.JsonIgnore;
import com.fasterxml.jackson.annotation.JsonProperty;

import java.util.HashMap;
import java.util.Map;
import java.util.Objects;
import java.util.Set;
import java.util.TreeSet;

public class ChildItem implements Comparable<ChildItem> {
    @JsonProperty("ChildItems")
    private Set<ChildItem> childItems;

    @JsonIgnore
    private Map<String, ChildItem> packageNameToChildMap;

    @JsonProperty("NavigationId")
    private String navigationId;

    @JsonProperty("Text")
    private String text;

    @JsonProperty("Tags")
    private Tags tags;

    public ChildItem(final String text, TypeKind typeKind) {
        this(null, text, typeKind);
    }

    public ChildItem(final String navigationId, final String text, TypeKind typeKind) {
        this.childItems = new TreeSet<>();
        this.navigationId = navigationId;
        this.tags = new Tags(typeKind);
        this.text = text;
        this.packageNameToChildMap = new HashMap<>();
    }

    public Set<ChildItem> getChildItem() {
        return childItems;
    }

    public void addChildItem(ChildItem childItem) {
        this.childItems.add(childItem);
    }

    public void addChildItem(String packageName, ChildItem childItem) {
        if (packageNameToChildMap.containsKey(packageName)) {
            ChildItem parent = packageNameToChildMap.get(packageName);
            parent.addChildItem(childItem);
        } else {
            ChildItem parent = new ChildItem(packageName, packageName, TypeKind.NAMESPACE);
            parent.addChildItem(childItem);
            packageNameToChildMap.put(packageName, parent);
            childItems.add(parent);
        }
    }

    public String getNavigationId() {
        return navigationId;
    }

    public void setNavigationId (String navigationId) {
        this.navigationId = navigationId;
    }

    public String getText() {
        return text;
    }

    public void setText(String text) {
        this.text = text;
    }

    public Tags getTags() {
        return tags;
    }

    public void setTags(Tags tags) {
        this.tags = tags;
    }

    @Override
    public int compareTo(ChildItem o) {
        // we special case the maven pom.xml file and the module-info file so it appears at the top
        if (JavaASTAnalyser.MAVEN_KEY.equals(text)) return -1;
        else if (JavaASTAnalyser.MAVEN_KEY.equals(o.text)) return 1;

        if (JavaASTAnalyser.MODULE_INFO_KEY.equals(text)) return -1;
        else if (JavaASTAnalyser.MODULE_INFO_KEY.equals(o.text)) return 1;

        return text.compareTo(o.text);
    }

    @Override
    public String toString() {
        return "ChildItem [childItems = "+childItems+", navigationId = "+navigationId+", text = "+text+", tags = "+tags+"]";
    }

    @Override
    public boolean equals(final Object o) {
        if (this == o) return true;
        if (o == null || getClass() != o.getClass()) return false;
        final ChildItem childItem = (ChildItem) o;
        return text.equals(childItem.text);
    }

    @Override
    public int hashCode() {
        return Objects.hash(text);
    }
}