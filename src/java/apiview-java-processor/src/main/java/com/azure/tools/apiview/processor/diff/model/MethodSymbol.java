package com.azure.tools.apiview.processor.diff.model;

import java.util.ArrayList;
import java.util.HashSet;
import java.util.List;
import java.util.Set;

/**
 * Represents a method or constructor included in the public surface.
 */
public final class MethodSymbol {
    private String name;                // simple method name (or constructor name)
    private String fqn;                 // containing class FQN
    private String returnType;          // erased/simple return type ("void" for constructors)
    private String returnTypeFull;      // full generic return type
    private final List<Param> params = new ArrayList<>();
    private boolean deprecated;
    private final Set<String> modifiers = new HashSet<>();
    private String visibility;          // public|protected|private|default
    private int typeParamCount;
    private String fullSignature;       // <FQN>#name(type1,type2,...)
    private String signatureWithReturn; // <FQN>#name(type1,type2,...):ReturnType
    private String arityKey;            // name|paramCount

    public String getName() { return name; }
    public MethodSymbol setName(String name) { this.name = name; return this; }
    public String getFqn() { return fqn; }
    public MethodSymbol setFqn(String fqn) { this.fqn = fqn; return this; }
    public String getReturnType() { return returnType; }
    public MethodSymbol setReturnType(String returnType) { this.returnType = returnType; return this; }
    public String getReturnTypeFull() { return returnTypeFull; }
    public MethodSymbol setReturnTypeFull(String returnTypeFull) { this.returnTypeFull = returnTypeFull; return this; }
    public List<Param> getParams() { return params; }
    public boolean isDeprecated() { return deprecated; }
    public MethodSymbol setDeprecated(boolean deprecated) { this.deprecated = deprecated; return this; }
    public Set<String> getModifiers() { return modifiers; }
    public String getVisibility() { return visibility; }
    public MethodSymbol setVisibility(String visibility) { this.visibility = visibility; return this; }
    public int getTypeParamCount() { return typeParamCount; }
    public MethodSymbol setTypeParamCount(int typeParamCount) { this.typeParamCount = typeParamCount; return this; }
    public String getFullSignature() { return fullSignature; }
    public MethodSymbol setFullSignature(String fullSignature) { this.fullSignature = fullSignature; return this; }
    public String getSignatureWithReturn() { return signatureWithReturn; }
    public MethodSymbol setSignatureWithReturn(String signatureWithReturn) { this.signatureWithReturn = signatureWithReturn; return this; }
    public String getArityKey() { return arityKey; }
    public MethodSymbol setArityKey(String arityKey) { this.arityKey = arityKey; return this; }

    public static final class Param {
        private String name;
        private String type;        // erased/simple
        private String typeFull;    // full generic form

        public String getName() { return name; }
        public Param setName(String name) { this.name = name; return this; }
        public String getType() { return type; }
        public Param setType(String type) { this.type = type; return this; }
        public String getTypeFull() { return typeFull; }
        public Param setTypeFull(String typeFull) { this.typeFull = typeFull; return this; }
    }
}
