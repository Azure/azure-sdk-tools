package com.azure.tools.apiview.processor.diff.model;

import java.util.HashSet;
import java.util.Set;

/**
 * Represents a field in a public/protected class.
 */
public final class FieldSymbol {
    private String name;
    private String type;            // erased or simple representation
    private String typeFull;        // full generic form (if needed)
    private boolean deprecated;
    private final Set<String> modifiers = new HashSet<>();
    private String visibility;      // public|protected|private|default

    public String getName() { return name; }
    public FieldSymbol setName(String name) { this.name = name; return this; }
    public String getType() { return type; }
    public FieldSymbol setType(String type) { this.type = type; return this; }
    public String getTypeFull() { return typeFull; }
    public FieldSymbol setTypeFull(String typeFull) { this.typeFull = typeFull; return this; }
    public boolean isDeprecated() { return deprecated; }
    public FieldSymbol setDeprecated(boolean deprecated) { this.deprecated = deprecated; return this; }
    public Set<String> getModifiers() { return modifiers; }
    public String getVisibility() { return visibility; }
    public FieldSymbol setVisibility(String visibility) { this.visibility = visibility; return this; }
}
