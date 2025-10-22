package com.azure.tools.apiview.processor.diff.collector;

import com.azure.tools.apiview.processor.diff.model.ClassSymbol;
import com.azure.tools.apiview.processor.diff.model.FieldSymbol;
import com.azure.tools.apiview.processor.diff.model.MethodSymbol;
import com.github.javaparser.ast.NodeList;
import com.github.javaparser.ast.body.*;
import com.github.javaparser.ast.type.Type;

import java.util.Locale;
import java.util.Map;

/**
 * Collects symbols into a class map from analyser callbacks.
 */
public class DiffSymbolCollector implements SymbolCollector {
    private final Map<String, ClassSymbol> classes;

    public DiffSymbolCollector(Map<String, ClassSymbol> classes) { this.classes = classes; }

    @Override
    public void onType(TypeDeclaration<?> td) {
        if (td == null) {
            return;
        }
        String fqn = td.getFullyQualifiedName().orElse(null);
        if (fqn == null) {
            return;
        }
        ClassSymbol cls = classes.computeIfAbsent(fqn, k -> new ClassSymbol());
        cls.setFqn(fqn);
        cls.setDeprecated(td.isAnnotationPresent("Deprecated"));
        td.getModifiers().forEach(m -> cls.getModifiers().add(m.getKeyword().asString()));
    }

    @Override
    public void onField(TypeDeclaration<?> parentType, FieldDeclaration fd) {
        if (parentType == null || fd == null) {
            return;
        }
        String parentFqn = parentType.getFullyQualifiedName().orElse(null);
        if (parentFqn == null) {
            return;
        }
        ClassSymbol cls = classes.computeIfAbsent(parentFqn, k -> new ClassSymbol());
        cls.setFqn(parentFqn);
                for (VariableDeclarator vd : fd.getVariables()) {
                        FieldSymbol fs = new FieldSymbol();
                        Type t = vd.getType();
                        fs.setName(vd.getNameAsString())
                            .setTypeFull(t.toString())
                            .setType(eraseType(t.toString()))
                            .setDeprecated(fd.isAnnotationPresent("Deprecated"))
                            .setVisibility(fd.getAccessSpecifier().name().toLowerCase(Locale.ROOT));
                        fd.getModifiers().forEach(m -> fs.getModifiers().add(m.getKeyword().asString()));
                        cls.getFields().putIfAbsent(fs.getName(), fs);
                }
    }

    @Override
    public void onMethod(TypeDeclaration<?> parentType, CallableDeclaration<?> cd, boolean isConstructor) {
        if (parentType == null || cd == null) {
            return;
        }
        String parentFqn = parentType.getFullyQualifiedName().orElse(null);
        if (parentFqn == null) {
            return;
        }
        ClassSymbol cls = classes.computeIfAbsent(parentFqn, k -> new ClassSymbol());
        cls.setFqn(parentFqn);

                MethodSymbol ms = new MethodSymbol();
                ms.setName(cd.getNameAsString())
                    .setFqn(parentFqn);
                String ret = isConstructor ? "void" : (cd instanceof MethodDeclaration ? ((MethodDeclaration) cd).getType().toString() : "void");
                ms.setReturnTypeFull(ret)
                    .setReturnType(eraseType(ret))
                    .setDeprecated(cd.isAnnotationPresent("Deprecated"))
                    .setVisibility(cd.getAccessSpecifier().name().toLowerCase(Locale.ROOT))
                    .setTypeParamCount(cd.getTypeParameters().size())
                    .setArityKey(ms.getName() + "|" + cd.getParameters().size());
                NodeList<Parameter> params = cd.getParameters();
                for (Parameter p : params) {
                        MethodSymbol.Param mp = new MethodSymbol.Param();
                        mp.setName(p.getNameAsString())
                            .setTypeFull(p.getType().toString())
                            .setType(eraseType(p.getType().toString()));
                        ms.getParams().add(mp);
                }
                cd.getModifiers().forEach(m -> ms.getModifiers().add(m.getKeyword().asString()));
                StringBuilder sig = new StringBuilder(parentFqn).append('#').append(ms.getName()).append('(');
                for (int i = 0; i < ms.getParams().size(); i++) { if (i > 0) sig.append(','); sig.append(ms.getParams().get(i).getType()); }
        sig.append(')');
                ms.setFullSignature(sig.toString());
                ms.setSignatureWithReturn(ms.getFullSignature() + ':' + ms.getReturnType());
                cls.getMethodsBySignature().putIfAbsent(ms.getFullSignature(), ms);
                cls.getMethodsByName().computeIfAbsent(ms.getName(), k -> new java.util.ArrayList<MethodSymbol>()).add(ms);
    }

    private static String eraseType(String type) {
        int idx = type.indexOf('<');
        return idx > 0 ? type.substring(0, idx) : type;
    }
}
