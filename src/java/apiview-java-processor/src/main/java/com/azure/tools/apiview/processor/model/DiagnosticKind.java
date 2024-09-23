
package com.azure.tools.apiview.processor.model;

public enum DiagnosticKind {
    ERROR(3),       // red
    WARNING(2),     // yellow
    INFO(1);        // green

    private final int level;

    DiagnosticKind(int level) {
        this.level = level;
    }

    public static DiagnosticKind fromInt(int level) {
        switch (level) {
            case 3:
                return ERROR;
            case 2:
                return WARNING;
            case 1:
                return INFO;
            default:
                return null;
        }
    }

    public int getLevel() {
        return level;
    }
}
