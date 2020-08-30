package com.azure.tools.apiview.processor.model;

import com.azure.tools.apiview.processor.analysers.ASTAnalyser;
import com.fasterxml.jackson.annotation.JsonProperty;

import java.util.List;
import java.util.Set;
import java.util.TreeSet;

public class ChildItem implements Comparable<ChildItem> {
    @JsonProperty("ChildItems")
    private Set<ChildItem> childItems;

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
    }

    public Set<ChildItem> getChildItem() {
        return childItems;
    }

    public void addChildItem(ChildItem childItem) {
        this.childItems.add(childItem);
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
        // we special case the module-info file so it appears at the top
        if (ASTAnalyser.MODULE_INFO_KEY.equals(text)) return -1;
        else if (ASTAnalyser.MODULE_INFO_KEY.equals(o.text)) return 1;
        else return text.compareTo(o.text);
    }

    @Override
    public String toString() {
        return "ChildItem [childItems = "+childItems+", navigationId = "+navigationId+", text = "+text+", tags = "+tags+"]";
    }
}