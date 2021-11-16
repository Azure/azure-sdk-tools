package com.azure.tools.apiview.processor.analysers.util;

import com.github.javaparser.StaticJavaParser;
import com.github.javaparser.ast.CompilationUnit;
import com.github.javaparser.ast.body.ClassOrInterfaceDeclaration;
import com.github.javaparser.ast.body.TypeDeclaration;
import com.github.javaparser.resolution.UnsolvedSymbolException;
import com.github.javaparser.resolution.declarations.ResolvedReferenceTypeDeclaration;
import com.github.javaparser.symbolsolver.javaparsermodel.declarations.JavaParserClassDeclaration;
import com.github.javaparser.symbolsolver.javaparsermodel.declarations.JavaParserEnumDeclaration;
import com.github.javaparser.symbolsolver.javaparsermodel.declarations.JavaParserInterfaceDeclaration;
import com.github.javaparser.symbolsolver.model.resolution.SymbolReference;
import com.github.javaparser.symbolsolver.model.resolution.TypeSolver;
import javassist.ClassPool;
import javassist.NotFoundException;

import java.io.*;
import java.nio.file.Path;
import java.util.Enumeration;
import java.util.HashMap;
import java.util.Map;
import java.util.jar.JarEntry;
import java.util.jar.JarFile;

/**
 * Will let the symbol solver look inside a jar file while solving types.
 */
public class SourceJarTypeSolver implements TypeSolver {

    private static SourceJarTypeSolver instance;

    private TypeSolver parent;
    private Map<String, ClasspathElement> classpathElements = new HashMap<>();
    private ClassPool classPool = new ClassPool(false);

    public SourceJarTypeSolver(Path pathToJar) throws IOException {
        this(pathToJar.toFile());
    }

    public SourceJarTypeSolver(File pathToJar) throws IOException {
        this(pathToJar.getCanonicalPath());
    }

    public SourceJarTypeSolver(String pathToJar) throws IOException {
        addPathToJar(pathToJar);
    }

    public SourceJarTypeSolver(InputStream jarInputStream) throws IOException {
        addPathToJar(jarInputStream);
    }

    public static SourceJarTypeSolver getJarTypeSolver(String pathToJar) throws IOException {
        if (instance == null) {
            instance = new SourceJarTypeSolver(pathToJar);
        } else {
            instance.addPathToJar(pathToJar);
        }
        return instance;
    }

    private File dumpToTempFile(InputStream inputStream) throws IOException {
        File tempFile = File.createTempFile("jar_file_from_input_stream", ".jar");
        tempFile.deleteOnExit();

        byte[] buffer = new byte[8 * 1024];

        try (OutputStream output = new FileOutputStream(tempFile)) {
            int bytesRead;
            while ((bytesRead = inputStream.read(buffer)) != -1) {
                output.write(buffer, 0, bytesRead);
            }
        } finally {
            inputStream.close();
        }
        return tempFile;
    }

    private void addPathToJar(InputStream jarInputStream) throws IOException {
        addPathToJar(dumpToTempFile(jarInputStream).getAbsolutePath());
    }

    private void addPathToJar(String pathToJar) throws IOException {
        try {
            classPool.appendClassPath(pathToJar);
            classPool.appendSystemPath();
        } catch (NotFoundException e) {
            throw new RuntimeException(e);
        }
        JarFile jarFile = new JarFile(pathToJar);
        JarEntry entry;
        Enumeration<JarEntry> e = jarFile.entries();
        while (e.hasMoreElements()) {
            entry = e.nextElement();
            if (entry != null && !entry.isDirectory() && entry.getName().endsWith(".java")) {
                String name = entryPathToClassName(entry.getName());
                classpathElements.put(name, new ClasspathElement(jarFile, entry));
            }
        }
    }

    @Override
    public TypeSolver getParent() {
        return parent;
    }

    @Override
    public void setParent(TypeSolver parent) {
        this.parent = parent;
    }

    private String entryPathToClassName(String entryPath) {
        if (!entryPath.endsWith(".java")) {
            throw new IllegalStateException();
        }
        String className = entryPath.substring(0, entryPath.length() - ".java".length());
        className = className.replace('/', '.');
        className = className.replace('$', '.');
        return className;
    }

    @Override
    public SymbolReference<ResolvedReferenceTypeDeclaration> tryToSolveType(String name) {
        if (classpathElements.containsKey(name)) {
            CompilationUnit cu = classpathElements.get(name).parseJava();

            for (TypeDeclaration<?> type : cu.getTypes()) {
                if (type.isClassOrInterfaceDeclaration()) {
                    ClassOrInterfaceDeclaration classType = (ClassOrInterfaceDeclaration) type;
                    return SymbolReference.solved(type.asClassOrInterfaceDeclaration().isInterface() ?
                                                          new JavaParserInterfaceDeclaration(classType, this) :
                                                          new JavaParserClassDeclaration(classType, this));
                } else if (type.isEnumDeclaration()) {
                    return SymbolReference.solved(new JavaParserEnumDeclaration(type.asEnumDeclaration(), this));
                } else {
                    System.err.println("Can't resolve " + type);
                }
            }
            return SymbolReference.unsolved(ResolvedReferenceTypeDeclaration.class);
        } else {
            return SymbolReference.unsolved(ResolvedReferenceTypeDeclaration.class);
        }
    }

    @Override
    public ResolvedReferenceTypeDeclaration solveType(String name) throws UnsolvedSymbolException {
        SymbolReference<ResolvedReferenceTypeDeclaration> ref = tryToSolveType(name);
        if (ref.isSolved()) {
            return ref.getCorrespondingDeclaration();
        } else {
            throw new UnsolvedSymbolException(name);
        }
    }

    private class ClasspathElement {
        private JarFile jarFile;
        private JarEntry entry;

        ClasspathElement(JarFile jarFile, JarEntry entry) {
            this.jarFile = jarFile;
            this.entry = entry;
        }

        public CompilationUnit parseJava() {
            try {
                return StaticJavaParser.parse(jarFile.getInputStream(entry));
            } catch (IOException e) {
                e.printStackTrace();
            }
            return null;
        }
    }
}