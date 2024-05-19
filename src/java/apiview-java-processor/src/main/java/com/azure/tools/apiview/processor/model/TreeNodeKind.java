package com.azure.tools.apiview.processor.model;

public enum TreeNodeKind {
    ASSEMBLY("Assembly"),     // i.e. a Jar File
    NAMESPACE("Namespace"),   // i.e. a Java package
    CLASS("Type", "Class") {
        @Override
        public String getTypeDeclarationString() {
            return "class";
        }
    },
    INTERFACE("Type", "Interface") {
        @Override
        public String getTypeDeclarationString() {
            return "interface";
        }
    },
    ENUM("Type", "Enum") {
        @Override
        public String getTypeDeclarationString() {
            return "enum";
        }
    },
    ENUM_CONSTANT("Member", "EnumConstant"),

    // Note: This is for the definition of annotations (rather than the use of them)
    ANNOTATION("Type", "Annotation") {
        @Override
        public String getTypeDeclarationString() {
            return "@annotation";
        }
    },

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


    TreeNodeKind(String name) {
        this(name, null);
    }

    TreeNodeKind(String name, String subKind) {
        this.name = name;
        this.subKind = subKind;
    }

    public String getName() {
        return name;
    }

    public String getSubKind() {
        return subKind;
    }

    public String getTypeDeclarationString() {
        return "UNKNOWN";
    }

    public static TreeNodeKind fromName(String name) {
        for (TreeNodeKind typeKind : TreeNodeKind.values()) {
            if (typeKind.getName().equals(name)) {
                return typeKind;
            }
        }
        return null;
    }
}
