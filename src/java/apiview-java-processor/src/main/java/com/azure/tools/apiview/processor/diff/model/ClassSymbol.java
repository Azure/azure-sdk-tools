package com.azure.tools.apiview.processor.diff.model;

import java.util.ArrayList;
import java.util.HashMap;
import java.util.HashSet;
import java.util.List;
import java.util.Map;
import java.util.Set;

/**
 * Represents a top-level or nested class/interface/enum that is part of the public surface.
 */
public final class ClassSymbol {
    private String fqn;                       // fully-qualified name
    private boolean deprecated;
    private final Set<String> modifiers = new HashSet<>();

    // Fields keyed by simple name.
    private final Map<String, FieldSymbol> fields = new HashMap<>();

    // Methods keyed by canonical full signature (<FQN>#name(paramType,...)).
    private final Map<String, MethodSymbol> methodsBySignature = new HashMap<>();

    // Methods grouped by simple name to help overload / param rename detection.
    private final Map<String, List<MethodSymbol>> methodsByName = new HashMap<>();

    // Optional hierarchy / nesting tracking (future use)
    private String enclosingFqn; // null if top-level
    private final List<String> nestedTypeFqns = new ArrayList<>();

    public String getFqn() { return fqn; }
    public void setFqn(String fqn) { this.fqn = fqn; }
    public boolean isDeprecated() { return deprecated; }
    public void setDeprecated(boolean deprecated) { this.deprecated = deprecated; }
    public Set<String> getModifiers() { return modifiers; }
    public Map<String, FieldSymbol> getFields() { return fields; }
    public Map<String, MethodSymbol> getMethodsBySignature() { return methodsBySignature; }
    public Map<String, List<MethodSymbol>> getMethodsByName() { return methodsByName; }
    public String getEnclosingFqn() { return enclosingFqn; }
    public void setEnclosingFqn(String enclosingFqn) { this.enclosingFqn = enclosingFqn; }
    public List<String> getNestedTypeFqns() { return nestedTypeFqns; }
}
