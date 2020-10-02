package com.azure.tools.apiview.processor.analysers.util;

import com.github.javaparser.ast.AccessSpecifier;
import com.github.javaparser.ast.CompilationUnit;
import com.github.javaparser.ast.ImportDeclaration;
import com.github.javaparser.ast.Node;
import com.github.javaparser.ast.PackageDeclaration;
import com.github.javaparser.ast.body.CallableDeclaration;
import com.github.javaparser.ast.body.ClassOrInterfaceDeclaration;
import com.github.javaparser.ast.body.ConstructorDeclaration;
import com.github.javaparser.ast.body.FieldDeclaration;
import com.github.javaparser.ast.body.MethodDeclaration;
import com.github.javaparser.ast.body.TypeDeclaration;
import com.github.javaparser.ast.body.VariableDeclarator;

import java.util.Optional;
import java.util.stream.Stream;

public class ASTUtils {

    public static Optional<String> getPackageName(CompilationUnit cu) {
        return cu.getPackageDeclaration().map(PackageDeclaration::getNameAsString);
    }

    public static String getPackageName(TypeDeclaration<?> typeDeclaration) {
        return typeDeclaration.getFullyQualifiedName()
                       .map(str -> str.substring(0, str.lastIndexOf(".")))
                       .orElse("");
    }

    public static Stream<String> getImports(CompilationUnit cu) {
        return cu.getImports().stream().map(ImportDeclaration::getNameAsString);
    }

    public static Optional<String> getClassName(CompilationUnit cu) {
        return cu.getPrimaryTypeName();
    }

    public static Stream<TypeDeclaration<?>> getClasses(CompilationUnit cu) {
        return cu.getTypes().stream();
    }

    public static Stream<ConstructorDeclaration> getPublicOrProtectedConstructors(CompilationUnit cu) {
        return cu.getTypes().stream().flatMap(ASTUtils::getPublicOrProtectedConstructors);
    }

    public static Stream<ConstructorDeclaration> getPublicOrProtectedConstructors(TypeDeclaration<?> typeDeclaration) {
        return typeDeclaration.getConstructors().stream()
                       .filter(type -> isPublicOrProtected(type.getAccessSpecifier()));
    }

    public static Stream<MethodDeclaration> getPublicOrProtectedMethods(CompilationUnit cu) {
        return cu.getTypes().stream().flatMap(ASTUtils::getPublicOrProtectedMethods);
    }

    public static Stream<MethodDeclaration> getPublicOrProtectedMethods(TypeDeclaration<?> typeDeclaration) {
        return typeDeclaration.getMethods().stream()
                       .filter(type -> isPublicOrProtected(type.getAccessSpecifier()));
    }

    public static Stream<FieldDeclaration> getPublicOrProtectedFields(CompilationUnit cu) {
        return cu.getTypes().stream().flatMap(ASTUtils::getPublicOrProtectedFields);
    }

    public static Stream<FieldDeclaration> getPublicOrProtectedFields(TypeDeclaration<?> typeDeclaration) {
        return typeDeclaration.getFields().stream()
                       .filter(type -> isPublicOrProtected(type.getAccessSpecifier()));
    }

    public static boolean isPublicOrProtected(AccessSpecifier accessSpecifier) {
        return (accessSpecifier == AccessSpecifier.PUBLIC) || (accessSpecifier == AccessSpecifier.PROTECTED);
    }

    public static boolean isPrivateOrPackagePrivate(AccessSpecifier accessSpecifier) {
        return (accessSpecifier == AccessSpecifier.PRIVATE) || (accessSpecifier == AccessSpecifier.PACKAGE_PRIVATE);
    }

    public static String makeId(CompilationUnit cu) {
        return makeId(cu.getPrimaryType().get());
    }

    public static String makeId(TypeDeclaration<?> typeDeclaration) {
        return makeId(typeDeclaration.getFullyQualifiedName().get());
    }

    public static String makeId(VariableDeclarator variableDeclarator) {
        return makeId(getNodeFullyQualifiedName(variableDeclarator.getParentNode()) + "." + variableDeclarator.getNameAsString());
    }

    public static String makeId(CallableDeclaration callableDeclaration) {
        return makeId(getNodeFullyQualifiedName(callableDeclaration.getParentNode()) + "." + callableDeclaration.getDeclarationAsString());
    }

    public static String makeId(FieldDeclaration fieldDeclaration) {
        return makeId(fieldDeclaration.getVariables().get(0));
    }

    public static String makeId(String fullPath) {
        return fullPath.replaceAll("\"| ", "-");
    }

    /**
     * Returns true if the type is public or protected, or it the type is an interface that is defined within another
     * public interface.
     */
    public static boolean isTypeAPublicAPI(TypeDeclaration type) {
        final Node parentNode = type.getParentNode().orElse(null);
        final boolean isTypePrivate = isPrivateOrPackagePrivate(type.getAccessSpecifier());

        // if we have no parent, we just inspect the access modifier on the given type and return appropriately from that.
        if (parentNode == null) {
            return !isTypePrivate;
        }

        // otherwise there are more rules we want to consider...
//        final boolean isInterfaceType = isInterfaceType(type);
//        final boolean isNestedType = type.isNestedType();

        if (parentNode instanceof ClassOrInterfaceDeclaration) {
            final ClassOrInterfaceDeclaration parentClass = (ClassOrInterfaceDeclaration)parentNode;
            final boolean isInPublicParent = isPublicOrProtected(parentClass.getAccessSpecifier());
            final boolean isParentAnInterface = isInterfaceType(parentClass);

            // conditions to consider:
            // 1) if the parent type is private, then it doesn't matter - we are always going to be private
            if (!isInPublicParent) {
                return false;
            }

            // 2) If the type is non-public, but the parent type is an interface, then it is still public
            if (isTypePrivate && isParentAnInterface && isInPublicParent) {
                return true;
            }
        }

        // 3) otherwise, we return based on whether the type itself is public or not
        return !isTypePrivate;
    }

    /**
     * Returns true if the type is a public interface.
     */
    public static boolean isInterfaceType(TypeDeclaration type) {
        if (type.isClassOrInterfaceDeclaration()) {
            return type.asClassOrInterfaceDeclaration().isInterface();
        }
        return false;
    }

    private static String getNodeFullyQualifiedName(Optional<Node> nodeOptional) {
        if (!nodeOptional.isPresent()) {
            return "";
        }

        Node node = nodeOptional.get();
        if (node instanceof ClassOrInterfaceDeclaration) {
            ClassOrInterfaceDeclaration type = (ClassOrInterfaceDeclaration)node;
            return type.getFullyQualifiedName().get();
        } else {
            return "";
        }
    }
}
