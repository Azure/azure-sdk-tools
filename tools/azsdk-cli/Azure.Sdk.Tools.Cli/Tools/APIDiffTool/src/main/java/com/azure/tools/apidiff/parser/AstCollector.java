package com.azure.tools.apidiff.parser;

import com.github.javaparser.StaticJavaParser;
import com.github.javaparser.ast.CompilationUnit;
import com.github.javaparser.ast.body.*;

import java.io.IOException;
import java.nio.file.*;
import java.util.*;

public class AstCollector {
    public static class MethodSig {
        public final String fqn; // e.g. com.example.Client#method(String,int)
        public final List<String> paramNames;
        public MethodSig(String fqn, List<String> paramNames){ this.fqn = fqn; this.paramNames = paramNames; }
    }

    public List<MethodSig> collectMethods(Path root) throws IOException {
        if (!Files.exists(root)) return Collections.emptyList();
        List<MethodSig> methods = new ArrayList<>();
        try(var stream = Files.walk(root)){
            stream.filter(p -> p.toString().endsWith(".java")).forEach(p -> {
                try {
                    CompilationUnit cu = StaticJavaParser.parse(p);
                    String pkg = cu.getPackageDeclaration().map(pd -> pd.getName().toString()).orElse("");
                    cu.findAll(ClassOrInterfaceDeclaration.class).forEach(clazz -> {
                        String className = clazz.getNameAsString();
                        clazz.getMethods().forEach(m -> {
                            String paramsTypes = String.join(",", m.getParameters().stream().map(param -> param.getType().asString()).toList());
                            String fqn = (pkg.isEmpty()?"":pkg + ".") + className + "#" + m.getNameAsString() + "(" + paramsTypes + ")";
                            List<String> paramNames = m.getParameters().stream().map(p2 -> p2.getNameAsString()).toList();
                            methods.add(new MethodSig(fqn, paramNames));
                        });
                    });
                } catch (Exception ignored) { }
            });
        }
        return methods;
    }
}
