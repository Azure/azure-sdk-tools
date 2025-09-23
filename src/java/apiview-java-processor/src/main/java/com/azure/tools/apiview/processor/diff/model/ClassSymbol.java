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
    public String fqn;                       // fully-qualified name
    public boolean deprecated;
    public final Set<String> modifiers = new HashSet<>();

    // Fields keyed by simple name.
    public final Map<String, FieldSymbol> fields = new HashMap<>();

    // Methods keyed by canonical full signature (<FQN>#name(paramType,...)).
    public final Map<String, MethodSymbol> methodsBySignature = new HashMap<>();

    // Methods grouped by simple name to help overload / param rename detection.
    public final Map<String, List<MethodSymbol>> methodsByName = new HashMap<>();

    // Optional hierarchy / nesting tracking (future use)
    public String enclosingFqn; // null if top-level
    public final List<String> nestedTypeFqns = new ArrayList<>();
}
