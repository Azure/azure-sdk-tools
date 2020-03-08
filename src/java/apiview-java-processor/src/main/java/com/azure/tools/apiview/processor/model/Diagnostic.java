package com.azure.tools.apiview.processor.model;

import com.fasterxml.jackson.annotation.JsonProperty;

public class Diagnostic {
    private static int diagnosticIdCounter = 1;

    @JsonProperty("DiagnosticId")
    private String diagnosticId;

    @JsonProperty("Text")
    private String text;

    @JsonProperty("HelpLinkUri")
    private String helpLinkUri;

    @JsonProperty("TargetId")
    private String targetId;

    public Diagnostic(String targetId, String text) {
        this.diagnosticId = "AZ_JAVA_" + diagnosticIdCounter++;
        this.targetId = targetId;
        this.text = text;
    }
}
