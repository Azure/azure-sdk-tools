package com.azure.tools.apiview.processor.diff.model;

import java.util.ArrayList;
import java.util.HashSet;
import java.util.List;
import java.util.Set;

/**
 * Represents a method or constructor included in the public surface.
 */
public final class MethodSymbol {
    public String name;                // simple method name (or constructor name)
    public String fqn;                 // containing class FQN
    public String returnType;          // erased/simple return type ("void" for constructors)
    public String returnTypeFull;      // full generic return type
    public final List<Param> params = new ArrayList<>();
    public boolean deprecated;
    public final Set<String> modifiers = new HashSet<>();
    public String visibility;          // public|protected|private|default
    public int typeParamCount;
    public String fullSignature;       // <FQN>#name(type1,type2,...)
    public String signatureWithReturn; // <FQN>#name(type1,type2,...):ReturnType
    public String arityKey;            // name|paramCount

    public static final class Param {
        public String name;
        public String type;        // erased/simple
        public String typeFull;    // full generic form
    }
}
