package com.azure.tools.apiview.processor.model;

import com.fasterxml.jackson.annotation.JsonProperty;

public class Tags {
    @JsonProperty("TypeKind")
    private TypeKind typeKind;

    public Tags(TypeKind typeKind) {
        this.typeKind = typeKind;
    }

    public TypeKind getTypeKind() {
        return typeKind;
    }

    public void setTypeKind(TypeKind TypeKind) {
        this.typeKind = TypeKind;
    }

    @Override
    public String toString() {
        return "Tags [typeKind = "+ typeKind +"]";
    }
}
