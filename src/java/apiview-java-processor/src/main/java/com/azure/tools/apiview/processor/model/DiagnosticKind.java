package com.azure.tools.apiview.processor.model;

import com.fasterxml.jackson.annotation.JsonValue;

public enum DiagnosticKind {
    ERROR(3),       // red
    WARNING(2),     // yellow
    INFO(1);        // green

    private final int level;

    DiagnosticKind(int level) {
        this.level = level;
    }

    @JsonValue
    public int getLevel() {
        return level;
    }
}
