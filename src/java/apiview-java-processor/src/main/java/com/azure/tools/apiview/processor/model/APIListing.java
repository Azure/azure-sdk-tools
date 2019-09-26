package com.azure.tools.apiview.processor.model;

import com.fasterxml.jackson.annotation.JsonProperty;

import java.util.ArrayList;
import java.util.List;

public class APIListing {
    @JsonProperty("Navigation")
    private List<ChildItem> childItems;

    @JsonProperty("Name")
    private String Name;

    @JsonProperty("Tokens")
    private List<Token> tokens;

    public APIListing() {
        this.childItems = new ArrayList<>();
    }

    public List<ChildItem> getNavigation() {
        return childItems;
    }

    public void addChildItem(ChildItem childItem) {
        this.childItems.add(childItem);
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
}