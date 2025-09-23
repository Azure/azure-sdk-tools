package com.azure.tools.apiview.processor.diff.model;

import java.util.HashSet;
import java.util.Set;

/**
 * Represents a field in a public/protected class.
 */
public final class FieldSymbol {
    public String name;
    public String type;            // erased or simple representation
    public String typeFull;        // full generic form (if needed)
    public boolean deprecated;
    public final Set<String> modifiers = new HashSet<>();
    public String visibility;      // public|protected|private|default
}
