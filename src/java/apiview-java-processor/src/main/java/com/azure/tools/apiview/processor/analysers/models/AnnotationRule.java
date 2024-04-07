package com.azure.tools.apiview.processor.analysers.models;

import java.util.Set;

public class AnnotationRule {

    private boolean hideAnnotation = false;

    private boolean hideAttributes = false;

    private boolean showOnNewline = false;

    public AnnotationRule setHidden(boolean hidden) {
        this.hideAnnotation = hidden;
        return this;
    }

    public boolean isHidden() {
        return hideAnnotation;
    }

    public AnnotationRule setHideAttributes(boolean hidden) {
        this.hideAttributes = hidden;
        return this;
    }

    public boolean isHideAttributes() {
        return hideAttributes;
    }
}
