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

    @JsonProperty("Level")
    private DiagnosticKind level;

    public Diagnostic(DiagnosticKind level, String targetId, String text) {
        this(level, targetId, text, null);
    }

    public Diagnostic(DiagnosticKind level, String targetId, String text, String helpLinkUri) {
        this.diagnosticId = "AZ_JAVA_" + diagnosticIdCounter++;
        this.targetId = targetId;
        this.text = text;
        this.level = level;
        this.helpLinkUri = helpLinkUri;
    }

    public String getText() {
        return text;
    }

    public String getHelpLinkUri() {
        return helpLinkUri;
    }
}
