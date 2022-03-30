package com.azure.tools.apiview.processor.analysers.util;

import com.github.javaparser.Range;
import com.github.javaparser.ast.AccessSpecifier;
import com.github.javaparser.ast.CompilationUnit;
import com.github.javaparser.ast.ImportDeclaration;
import com.github.javaparser.ast.Node;
import com.github.javaparser.ast.PackageDeclaration;
import com.github.javaparser.ast.body.BodyDeclaration;
import com.github.javaparser.ast.body.CallableDeclaration;
import com.github.javaparser.ast.body.ClassOrInterfaceDeclaration;
import com.github.javaparser.ast.body.ConstructorDeclaration;
import com.github.javaparser.ast.body.FieldDeclaration;
import com.github.javaparser.ast.body.MethodDeclaration;
import com.github.javaparser.ast.body.TypeDeclaration;
import com.github.javaparser.ast.body.VariableDeclarator;
import com.github.javaparser.ast.comments.Comment;
import com.github.javaparser.ast.comments.JavadocComment;
import com.github.javaparser.ast.nodeTypes.NodeWithJavadoc;

import java.util.Collections;
import java.util.List;
import java.util.Optional;
import java.util.regex.Pattern;
import java.util.stream.Stream;

/**
 * Abstract syntax tree (AST) utility methods.
 */
public final class ASTUtils {
    private static final Pattern MAKE_ID = Pattern.compile("\"| ");

    /**
     * Attempts to get the package name for a compilation unit.
     *
     * @param cu The compilation unit.
     * @return An optional that may contain the package name for the compilation unit.
     */
    public static Optional<String> getPackageName(CompilationUnit cu) {
        return cu.getPackageDeclaration().map(PackageDeclaration::getNameAsString);
    }

    /**
     * Attempts to get the package name for a type declaration.
     * <p>
     * If the type declaration doesn't have a package name an empty string will be returned.
     *
     * @param typeDeclaration The type declaration.
     * @return The package name for the type declaration if it exists or an empty string if it doesn't.
     */
    public static String getPackageName(TypeDeclaration<?> typeDeclaration) {
        return typeDeclaration.getFullyQualifiedName()
            .map(str -> str.substring(0, str.lastIndexOf(".")))
            .orElse("");
    }

    /**
     * Gets the imports of a compilation unit.
     *
     * @param cu The compilation unit.
     * @return The imports of the compilation unit.
     */
    public static Stream<String> getImports(CompilationUnit cu) {
        return cu.getImports().stream().map(ImportDeclaration::getNameAsString);
    }

    /**
     * Attempts to get the primary type declaration name for the compilation unit.
     * <p>
     * The class name returned is based on the file name, using the expectation that the top-level type declaration
     * matches the file name.
     *
     * @param cu The compilation unit.
     * @return The primary type declaration name for the compilation unit.
     */
    public static Optional<String> getClassName(CompilationUnit cu) {
        return cu.getPrimaryTypeName();
    }

    /**
     * Gets all type declarations contained in the compilation unit.
     * <p>
     * Type declarations are classes, enums, and interfaces.
     *
     * @param cu The compilation unit.
     * @return All type declarations contained in the compilation unit.
     */
    public static Stream<TypeDeclaration<?>> getClasses(CompilationUnit cu) {
        return cu.getTypes().stream();
    }

    /**
     * Gets all public API constructors contained in a compilation unit.
     *
     * @param cu The compilation unit.
     * @return All public API constructors contained in the compilation unit.
     */
    public static Stream<ConstructorDeclaration> getPublicOrProtectedConstructors(CompilationUnit cu) {
        return getClasses(cu).flatMap(ASTUtils::getPublicOrProtectedConstructors);
    }

    /**
     * Gets all public API constructors contained in a type declaration.
     *
     * @param typeDeclaration The type declaration.
     * @return All public API constructors contained in the type declaration.
     */
    public static Stream<ConstructorDeclaration> getPublicOrProtectedConstructors(TypeDeclaration<?> typeDeclaration) {
        return typeDeclaration.getConstructors().stream()
            .filter(type -> isPublicOrProtected(type.getAccessSpecifier()));
    }

    /**
     * Gets all public API methods contained in a compilation unit.
     *
     * @param cu The compilation unit.
     * @return All public API methods contained in the compilation unit.
     */
    public static Stream<MethodDeclaration> getPublicOrProtectedMethods(CompilationUnit cu) {
        return getClasses(cu).flatMap(ASTUtils::getPublicOrProtectedMethods);
    }

    /**
     * Gets all public API methods contained in a type declaration.
     *
     * @param typeDeclaration The type declaration.
     * @return All public API methods contained in the type declaration.
     */
    public static Stream<MethodDeclaration> getPublicOrProtectedMethods(TypeDeclaration<?> typeDeclaration) {
        return typeDeclaration.getMethods().stream()
            .filter(type -> isPublicOrProtected(type.getAccessSpecifier()));
    }

    /**
     * Gets all public API fields contained in a compilation unit.
     *
     * @param cu The compilation unit.
     * @return All public API fields contained in the compilation unit.
     */
    public static Stream<FieldDeclaration> getPublicOrProtectedFields(CompilationUnit cu) {
        return getClasses(cu).flatMap(ASTUtils::getPublicOrProtectedFields);
    }

    /**
     * Gets all public API fields contained in a type declaration.
     *
     * @param typeDeclaration The type declaration.
     * @return All public API fields contained in the type declaration.
     */
    public static Stream<FieldDeclaration> getPublicOrProtectedFields(TypeDeclaration<?> typeDeclaration) {
        return typeDeclaration.getFields().stream()
            .filter(type -> isPublicOrProtected(type.getAccessSpecifier()));
    }

    /**
     * Determines whether the access specifier is public or protected.
     *
     * @param accessSpecifier The access specifier.
     * @return Whether the access specifier is public or protected.
     */
    public static boolean isPublicOrProtected(AccessSpecifier accessSpecifier) {
        return (accessSpecifier == AccessSpecifier.PUBLIC) || (accessSpecifier == AccessSpecifier.PROTECTED);
    }

    /**
     * Determines whether the access specifier is package-private or private.
     *
     * @param accessSpecifier The access specifier.
     * @return Whether the access specifier is package-private or private.
     */
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

    public static String makeId(CallableDeclaration<?> callableDeclaration) {
        return makeId(getNodeFullyQualifiedName(callableDeclaration.getParentNode()) + "." + callableDeclaration.getDeclarationAsString());
    }

    public static String makeId(FieldDeclaration fieldDeclaration) {
        return makeId(fieldDeclaration.getVariables().get(0));
    }

    public static String makeId(String fullPath) {
        return MAKE_ID.matcher(fullPath).replaceAll("-");
    }

    /**
     * Determines whether the type declaration is a public API (public or protected).
     * <p>
     * This handles inner classes and interfaces by also checking the surrounding type for being a public API as well.
     *
     * @param type The type declaration.
     * @return Whether the type declaration is a public API.
     */
    public static boolean isTypeAPublicAPI(TypeDeclaration<?> type) {
        Node parentNode = type.getParentNode().orElse(null);
        final boolean isTypePrivate = isPrivateOrPackagePrivate(type.getAccessSpecifier());

        // When we have no parent use the access modifier on the given type.
        if (parentNode == null) {
            return !isTypePrivate;
        }

        // otherwise there are more rules we want to consider...
        final boolean isInterfaceType = isInterfaceType(type);
//        final boolean isNestedType = type.isNestedType();

        if (parentNode instanceof ClassOrInterfaceDeclaration) {
            ClassOrInterfaceDeclaration parentClass = (ClassOrInterfaceDeclaration) parentNode;
            boolean isInPublicParent = isPublicOrProtected(parentClass.getAccessSpecifier());
            boolean isParentAnInterface = isInterfaceType(parentClass);

            /*
             * 1) If the parent type is a non-public non-interface type this type is not part of the public API.
             */
            if (!isInPublicParent && !isParentAnInterface) {
                return false;
            }

            /*
             * 2) If the parent type is a public interface then this type is part of the public API.
             */
            if (isInPublicParent && isParentAnInterface) {
                return true;
            }

            // 3) If the type is an non-public interface, but all parent types are either public or also interfaces,
            // then this type is public EXCEPT if the root type is a private interface.
            if (isTypePrivate && isInterfaceType) {
                // work way up parent chain, checking along the way
                while (parentClass != null) {
                    if (isInPublicParent && !isParentAnInterface) {
                        // we are looking at a public parent *class*. This means we have a private interface in a public
                        // class, so we know we have non-public API here
                        return false;
                    }

                    if (isParentAnInterface || isInPublicParent) {
                        parentNode = parentClass.getParentNode().orElse(null);
                        if (parentNode == null || parentNode instanceof CompilationUnit) {
                            // we have reached the top of the hierarchy, lets now determine if the previous type
                            // was public - if so we return true
                            return isInPublicParent;
                        }
                        parentClass = (ClassOrInterfaceDeclaration) parentNode;
                        isInPublicParent = isPublicOrProtected(parentClass.getAccessSpecifier());
                        isParentAnInterface = parentClass.isInterface();
                    } else {
                        return false;
                    }
                }
            }
        }

        // 3) otherwise, we return based on whether the type itself is public or not
        return !isTypePrivate;
    }

    /**
     * Returns true if the type is a public interface.
     */
    public static boolean isInterfaceType(TypeDeclaration<?> type) {
        if (type.isClassOrInterfaceDeclaration()) {
            return type.asClassOrInterfaceDeclaration().isInterface();
        }
        return false;
    }

    private static String getNodeFullyQualifiedName(Optional<Node> nodeOptional) {
        return nodeOptional.filter(node -> node instanceof TypeDeclaration<?>)
            .map(node -> ((TypeDeclaration<?>) node).getFullyQualifiedName())
            .map(Optional::get)
            .orElse("");
    }

    /**
     * Attempts to retrieve the {@link JavadocComment} for a given {@link BodyDeclaration}.
     * <p>
     * If the {@link BodyDeclaration} is an instance of {@link NodeWithJavadoc}, it will be checked for having an
     * existing {@link NodeWithJavadoc#getJavadocComment()}.
     * <p>
     * If the {@link BodyDeclaration} isn't an instance of {@link NodeWithJavadoc} or doesn't have an existing {@link
     * JavadocComment} the {@link BodyDeclaration} will be inspected for having a "detached Javadoc". Detached Javadoc
     * comments can occur when there is comment lines between the Javadoc and the body. This is resolved by walking up
     * the comments (reading earlier line numbers) until the previous line is no longer a comment line. Then the
     * orphaned Javadoc comments for the file are checked for containing the current line number, if so that Javadoc
     * comment is used as the Javadoc comment for the body declaration.
     *
     * @param bodyDeclaration The {@link BodyDeclaration} whose Javadoc comment is being found.
     * @return An {@link Optional} either containing the Javadoc comment for the body declaration or {@link
     * Optional#empty()}.
     */
    public static Optional<JavadocComment> attemptToFindJavadocComment(BodyDeclaration<?> bodyDeclaration) {
        if (!(bodyDeclaration instanceof NodeWithJavadoc<?>)) {
            return Optional.empty();
        }

        NodeWithJavadoc<?> nodeWithJavadoc = (NodeWithJavadoc<?>) bodyDeclaration;

        // BodyDeclaration has a Javadoc.
        if (nodeWithJavadoc.getJavadocComment().isPresent()) {
            return nodeWithJavadoc.getJavadocComment();
        }

        // Get the orphaned comments.
        List<Comment> orphanedComments = bodyDeclaration.getParentNode()
            .map(Node::getOrphanComments)
            .orElse(Collections.emptyList());

        // Traverse up the comments between the Javadoc and the body declaration.
        Optional<Comment> commentTraversalNode = bodyDeclaration.getComment();
        Comment comment = null;
        while (commentTraversalNode.isPresent()) {
            comment = commentTraversalNode.get();
            commentTraversalNode = comment.getComment();
        }

        if (comment == null) {
            return Optional.empty();
        }

        // Check if there are any orphaned Javadoc comments before the additional code comments.
        Range expectedJavadocRangeOverlap = comment.getRange().get()
            .withBeginLine(comment.getRange().get().begin.line - 1);

        return orphanedComments.stream()
            .filter(c -> c instanceof JavadocComment)
            .map(c -> (JavadocComment) c)
            .filter(c -> c.getRange().map(range -> range.overlapsWith(expectedJavadocRangeOverlap)).orElse(false))
            .findFirst();
    }

    private ASTUtils() {
    }
}
