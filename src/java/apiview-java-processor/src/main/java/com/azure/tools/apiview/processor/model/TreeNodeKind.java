package com.azure.tools.apiview.processor.model;

public enum TreeNodeKind {
    ASSEMBLY("Assembly", null),     // i.e. a Jar File
    NAMESPACE("Namespace", null),   // i.e. a Java package
    CLASS("Type", "Class", "class"),
    INTERFACE("Type", "Interface", "interface"),
    ENUM("Type", "Enum", "enum"),
    ENUM_CONSTANT("Member", "EnumConstant"),

    // Note: This is for the definition of annotations (rather than the use of them)
    ANNOTATION("Type", "Annotation", "@annotation"),

    MODULE_INFO("Module-Info"),
    MODULE_REQUIRES("Module-Info", "Module-Requires"),
    MODULE_REQUIRES_TRANSITIVE("Module-Info", "Module-Requires-Transitive"),
    MODULE_EXPORTS("Module-Info", "Module-Exports"),
    MODULE_OPENS("Module-Info", "Module-Opens"),
    MODULE_USES("Module-Info", "Module-Uses"),
    MODULE_PROVIDES("Module-Info", "Module-Provides"),

    MAVEN("Maven"),
    FIELD("Member", "Field"),
    METHOD("Member", "Method"),
    UNKNOWN("unknown");

    private final String name;
    private final String subKind;
    private final String typeDeclarationString;

    TreeNodeKind(String name) {
        this(name, null);
    }

    TreeNodeKind(String name, String subKind) {
        this(name, subKind, null);
    }

    TreeNodeKind(String name, String subKind, String typeDeclarationString) {
        this.name = name;
        this.subKind = subKind;
        this.typeDeclarationString = typeDeclarationString == null ? "UNKNOWN" : typeDeclarationString;
    }

    public String getName() {
        return name;
    }

    public String getSubKind() {
        return subKind;
    }

    public String getIconName(APIListing apiListing) {
        String iconName = subKind != null && !subKind.isEmpty() ? subKind : name;
        return apiListing.getLanguage() + "-" + iconName.toLowerCase();
    }

    public String getTypeDeclarationString() {
        return typeDeclarationString;
    }
}
