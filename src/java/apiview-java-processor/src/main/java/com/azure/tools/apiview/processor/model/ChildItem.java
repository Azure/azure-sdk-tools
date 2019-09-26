package com.azure.tools.apiview.processor.model;

import com.fasterxml.jackson.annotation.JsonProperty;

import java.util.ArrayList;
import java.util.List;

public class ChildItem {
    @JsonProperty("ChildItems")
    private List<ChildItem> childItems;

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
        this.childItems = new ArrayList<>();
        this.navigationId = navigationId;
        this.tags = new Tags(typeKind);
        this.text = text;
    }

    public List<ChildItem> getChildItem() {
        return childItems;
    }

    public void addChildItem(ChildItem childItem) {
        this.childItems.add(childItem);
    }

    public void setChildItems (List<ChildItem> childItems) {
        this.childItems = childItems;
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
    public String toString() {
        return "ChildItem [childItems = "+childItems+", navigationId = "+navigationId+", text = "+text+", tags = "+tags+"]";
    }
}