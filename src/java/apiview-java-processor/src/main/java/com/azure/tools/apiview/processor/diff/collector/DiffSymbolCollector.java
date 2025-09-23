package com.azure.tools.apiview.processor.diff.collector;

import com.azure.tools.apiview.processor.diff.model.ApiSymbolTable;
import com.azure.tools.apiview.processor.diff.model.ClassSymbol;
import com.azure.tools.apiview.processor.diff.model.FieldSymbol;
import com.azure.tools.apiview.processor.diff.model.MethodSymbol;
import com.github.javaparser.ast.NodeList;
import com.github.javaparser.ast.body.*;
import com.github.javaparser.ast.type.Type;

import java.util.Locale;

/**
 * Builds an ApiSymbolTable from analyser callbacks.
 */
public class DiffSymbolCollector implements SymbolCollector {
    private final ApiSymbolTable table;

    public DiffSymbolCollector(ApiSymbolTable table) { this.table = table; }

    @Override
    public void onType(TypeDeclaration<?> td) {
        if (td == null) return;
        String fqn = td.getFullyQualifiedName().orElse(null);
        if (fqn == null) return;
        ClassSymbol cls = table.classes.computeIfAbsent(fqn, k -> new ClassSymbol());
        cls.fqn = fqn;
        cls.deprecated = td.isAnnotationPresent("Deprecated");
        td.getModifiers().forEach(m -> cls.modifiers.add(m.getKeyword().asString()));
    }

    @Override
    public void onField(TypeDeclaration<?> parentType, FieldDeclaration fd) {
        if (parentType == null || fd == null) return;
        String parentFqn = parentType.getFullyQualifiedName().orElse(null);
        if (parentFqn == null) return;
        ClassSymbol cls = table.classes.computeIfAbsent(parentFqn, k -> new ClassSymbol());
        cls.fqn = parentFqn;
        for (VariableDeclarator vd : fd.getVariables()) {
            FieldSymbol fs = new FieldSymbol();
            Type t = vd.getType();
            fs.name = vd.getNameAsString();
            fs.typeFull = t.toString();
            fs.type = eraseType(fs.typeFull);
            fs.deprecated = fd.isAnnotationPresent("Deprecated");
            fd.getModifiers().forEach(m -> fs.modifiers.add(m.getKeyword().asString()));
            fs.visibility = fd.getAccessSpecifier().name().toLowerCase(Locale.ROOT);
            cls.fields.putIfAbsent(fs.name, fs);
        }
    }

    @Override
    public void onMethod(TypeDeclaration<?> parentType, CallableDeclaration<?> cd, boolean isConstructor) {
        if (parentType == null || cd == null) return;
        String parentFqn = parentType.getFullyQualifiedName().orElse(null);
        if (parentFqn == null) return;
        ClassSymbol cls = table.classes.computeIfAbsent(parentFqn, k -> new ClassSymbol());
        cls.fqn = parentFqn;

        MethodSymbol ms = new MethodSymbol();
        ms.name = cd.getNameAsString();
        ms.fqn = parentFqn;
        String ret = isConstructor ? "void" : (cd instanceof MethodDeclaration ? ((MethodDeclaration) cd).getType().toString() : "void");
        ms.returnTypeFull = ret;
        ms.returnType = eraseType(ret);
        ms.deprecated = cd.isAnnotationPresent("Deprecated");
        cd.getModifiers().forEach(m -> ms.modifiers.add(m.getKeyword().asString()));
        ms.visibility = cd.getAccessSpecifier().name().toLowerCase(Locale.ROOT);
        ms.typeParamCount = cd.getTypeParameters().size();
        ms.arityKey = ms.name + "|" + cd.getParameters().size();
        NodeList<Parameter> params = cd.getParameters();
        for (Parameter p : params) {
            MethodSymbol.Param mp = new MethodSymbol.Param();
            mp.name = p.getNameAsString();
            mp.typeFull = p.getType().toString();
            mp.type = eraseType(mp.typeFull);
            ms.params.add(mp);
        }
        StringBuilder sig = new StringBuilder(parentFqn).append('#').append(ms.name).append('(');
        for (int i = 0; i < ms.params.size(); i++) { if (i > 0) sig.append(','); sig.append(ms.params.get(i).type); }
        sig.append(')');
        ms.fullSignature = sig.toString();
        ms.signatureWithReturn = ms.fullSignature + ':' + ms.returnType;
        cls.methodsBySignature.putIfAbsent(ms.fullSignature, ms);
        cls.methodsByName.computeIfAbsent(ms.name, k -> new java.util.ArrayList<>()).add(ms);
    }

    private static String eraseType(String type) {
        int idx = type.indexOf('<');
        return idx > 0 ? type.substring(0, idx) : type;
    }
}
