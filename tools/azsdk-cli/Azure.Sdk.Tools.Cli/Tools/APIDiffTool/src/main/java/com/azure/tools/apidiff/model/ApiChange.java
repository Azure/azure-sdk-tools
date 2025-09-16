package com.azure.tools.apidiff.model;

import com.fasterxml.jackson.annotation.JsonInclude;

@JsonInclude(JsonInclude.Include.NON_NULL)
public class ApiChange {
    private String kind;
    private String symbol;
    private String detail;
    private Meta metadata;

    public ApiChange() {}
    public ApiChange(String kind, String symbol, String detail, Meta metadata){
        this.kind = kind; this.symbol = symbol; this.detail = detail; this.metadata = metadata;
    }

    public String getKind() { return kind; }
    public void setKind(String kind) { this.kind = kind; }

    public String getSymbol() { return symbol; }
    public void setSymbol(String symbol) { this.symbol = symbol; }

    public String getDetail() { return detail; }
    public void setDetail(String detail) { this.detail = detail; }

    public Meta getMetadata() { return metadata; }
    public void setMetadata(Meta metadata) { this.metadata = metadata; }

    public static class Meta {
        private String oldParamNames;
        private String newParamNames;
        private String paramNameChange;
        public Meta() {}
        public Meta(String oldParamNames, String newParamNames, boolean changed){
            this.oldParamNames = oldParamNames;
            this.newParamNames = newParamNames;
            this.paramNameChange = Boolean.toString(changed);
        }
        public String getOldParamNames() { return oldParamNames; }
        public void setOldParamNames(String v){ this.oldParamNames = v; }
        public String getNewParamNames() { return newParamNames; }
        public void setNewParamNames(String v){ this.newParamNames = v; }
        public String getParamNameChange() { return paramNameChange; }
        public void setParamNameChange(String v){ this.paramNameChange = v; }
    }
}
