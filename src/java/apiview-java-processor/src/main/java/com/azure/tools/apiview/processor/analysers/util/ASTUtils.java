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

import java.util.List;
import java.util.Optional;
import java.util.regex.Pattern;
import java.util.stream.Stream;

public class ASTUtils {
    private static final Pattern MAKE_ID = Pattern.compile("\"| ");

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
     * Returns true if the type is public or protected, or it the type is an interface that is defined within another
     * public interface.
     */
    public static boolean isTypeAPublicAPI(TypeDeclaration type) {
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
        if (node instanceof TypeDeclaration<?>) {
            TypeDeclaration<?> type = (TypeDeclaration<?>) node;
            return type.getFullyQualifiedName().get();
        } else {
            return "";
        }
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
        List<Comment> orphanedComments = bodyDeclaration.getParentNode().get().getOrphanComments();

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
            .filter(c -> c.getRange().get().overlapsWith(expectedJavadocRangeOverlap))
            .filter(c -> c instanceof JavadocComment)
            .map(c -> (JavadocComment) c)
            .findFirst();
    }
}
