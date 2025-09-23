package com.azure.tools.apiview.processor.diff;

import com.azure.tools.apiview.processor.diff.model.*;
import com.github.javaparser.ast.CompilationUnit;
import com.github.javaparser.ast.NodeList;
import com.github.javaparser.ast.body.*;
import com.github.javaparser.ast.type.Type;
import com.github.javaparser.ast.nodeTypes.NodeWithModifiers;

import java.util.stream.Collectors;

/**
 * Builds an {@link ApiSymbolTable} from parsed JavaParser {@link CompilationUnit}s capturing
 * the public/protected surface (classes, fields, methods & constructors).
 */
public final class SymbolExtractor {

    private SymbolExtractor() {}

    public static void extract(CompilationUnit cu, ApiSymbolTable table) {
        if (cu == null) return;
        cu.getTypes().forEach(td -> visitType(td, cu, table, null));
    }

    private static void visitType(TypeDeclaration<?> td, CompilationUnit cu, ApiSymbolTable table, String enclosingFqn) {
        if (td.isAnnotationDeclaration()) return; // skip annotations for now
        if (isHidden(td)) return; // only public/protected

        String pkg = cu.getPackageDeclaration().map(p -> p.getNameAsString()).orElse("");
        String fqn = pkg.isEmpty() ? td.getNameAsString() : pkg + "." + td.getNameAsString();
        if (enclosingFqn != null) {
            fqn = enclosingFqn + "." + td.getNameAsString();
        }

        ClassSymbol cls = table.classes.computeIfAbsent(fqn, k -> new ClassSymbol());
        cls.fqn = fqn;
        cls.enclosingFqn = enclosingFqn;
        cls.deprecated = td.isAnnotationPresent("Deprecated");
        td.getModifiers().forEach(m -> cls.modifiers.add(m.getKeyword().asString()));

        // Fields
        td.getFields().forEach(fd -> {
            if (isHidden(fd)) return;
            fd.getVariables().forEach(v -> {
                FieldSymbol fs = new FieldSymbol();
                fs.name = v.getNameAsString();
                fs.typeFull = fd.getElementType().toString();
                fs.type = eraseType(fd.getElementType());
                fs.deprecated = fd.isAnnotationPresent("Deprecated");
                fd.getModifiers().forEach(m -> fs.modifiers.add(m.getKeyword().asString()));
                fs.visibility = visibility(fs.modifiers);
                cls.fields.put(fs.name, fs);
            });
        });

        // Methods & constructors
        for (BodyDeclaration<?> member : td.getMembers()) {
            if (member instanceof MethodDeclaration) {
                MethodDeclaration md = (MethodDeclaration) member;
                if (isHidden(md)) continue;
                addMethod(cls, md, fqn);
            } else if (member instanceof ConstructorDeclaration) {
                ConstructorDeclaration cd = (ConstructorDeclaration) member;
                if (isHidden(cd)) continue;
                addConstructor(cls, cd, fqn);
            } else if (member instanceof TypeDeclaration<?>) {
                TypeDeclaration<?> nested = (TypeDeclaration<?>) member;
                visitType(nested, cu, table, fqn);
                cls.nestedTypeFqns.add(fqn + "." + nested.getNameAsString());
            }
        }
    }

    private static void addConstructor(ClassSymbol cls, ConstructorDeclaration cd, String fqn) {
        MethodSymbol ms = baseMethodSymbol(cls, cd.getNameAsString(), fqn, "void", "void", cd.getTypeParameters().size(), cd);
        fillParams(ms, cd.getParameters());
        finalizeMethod(cls, ms);
    }

    private static void addMethod(ClassSymbol cls, MethodDeclaration md, String fqn) {
        String retFull = md.getType().toString();
        String retErased = eraseType(md.getType());
        MethodSymbol ms = baseMethodSymbol(cls, md.getNameAsString(), fqn, retErased, retFull, md.getTypeParameters().size(), md);
        fillParams(ms, md.getParameters());
        finalizeMethod(cls, ms);
    }

    private static MethodSymbol baseMethodSymbol(ClassSymbol cls, String name, String fqn,
                                                 String returnType, String returnTypeFull,
                                                 int typeParams, CallableDeclaration<?> callable) {
        MethodSymbol ms = new MethodSymbol();
        ms.name = name;
        ms.fqn = fqn;
        ms.returnType = returnType;
        ms.returnTypeFull = returnTypeFull;
        ms.typeParamCount = typeParams;
        callable.getModifiers().forEach(m -> ms.modifiers.add(m.getKeyword().asString()));
        ms.deprecated = callable.isAnnotationPresent("Deprecated");
        ms.visibility = visibility(ms.modifiers);
        return ms;
    }

    private static void fillParams(MethodSymbol ms, NodeList<Parameter> params) {
        for (Parameter p : params) {
            MethodSymbol.Param pp = new MethodSymbol.Param();
            pp.name = p.getNameAsString();
            pp.typeFull = p.getType().toString();
            pp.type = eraseType(p.getType());
            ms.params.add(pp);
        }
        ms.arityKey = ms.name + "|" + ms.params.size();
        ms.fullSignature = ms.fqn + "#" + ms.name + "(" +
                ms.params.stream().map(pr -> pr.type).collect(Collectors.joining(",")) + ")";
        ms.signatureWithReturn = ms.fullSignature + ":" + ms.returnType;
    }

    private static void finalizeMethod(ClassSymbol cls, MethodSymbol ms) {
        cls.methodsBySignature.put(ms.fullSignature, ms);
        cls.methodsByName.computeIfAbsent(ms.name, k -> new java.util.ArrayList<>()).add(ms);
    }

    private static boolean isHidden(BodyDeclaration<?> bd) {
        if (!(bd instanceof NodeWithModifiers)) return true;
        NodeWithModifiers<?> nwm = (NodeWithModifiers<?>) bd;
        boolean isPublic = nwm.getModifiers().stream().anyMatch(m -> m.getKeyword().asString().equals("public"));
        boolean isProtected = nwm.getModifiers().stream().anyMatch(m -> m.getKeyword().asString().equals("protected"));
        return !(isPublic || isProtected);
    }

    private static String eraseType(Type t) {
        String txt = t.toString();
        int lt = txt.indexOf('<');
        if (lt >= 0) txt = txt.substring(0, lt);
        return txt.replace("...", "[]"); // normalize varargs
    }

    private static String visibility(java.util.Set<String> mods) {
        if (mods.contains("public")) return "public";
        if (mods.contains("protected")) return "protected";
        if (mods.contains("private")) return "private";
        return "default";
    }
}
