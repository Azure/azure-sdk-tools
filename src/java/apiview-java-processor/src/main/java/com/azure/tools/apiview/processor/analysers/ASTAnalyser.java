package com.azure.tools.apiview.processor.analysers;

import com.github.javaparser.JavaParser;
import com.github.javaparser.ParseResult;
import com.github.javaparser.ast.AccessSpecifier;
import com.github.javaparser.ast.CompilationUnit;
import com.github.javaparser.ast.Modifier;
import com.github.javaparser.ast.Node;
import com.github.javaparser.ast.NodeList;
import com.github.javaparser.ast.body.BodyDeclaration;
import com.github.javaparser.ast.body.CallableDeclaration;
import com.github.javaparser.ast.body.ClassOrInterfaceDeclaration;
import com.github.javaparser.ast.body.EnumConstantDeclaration;
import com.github.javaparser.ast.body.EnumDeclaration;
import com.github.javaparser.ast.body.FieldDeclaration;
import com.github.javaparser.ast.body.MethodDeclaration;
import com.github.javaparser.ast.body.Parameter;
import com.github.javaparser.ast.body.TypeDeclaration;
import com.github.javaparser.ast.body.VariableDeclarator;
import com.github.javaparser.ast.expr.Expression;
import com.github.javaparser.ast.nodeTypes.NodeWithType;
import com.github.javaparser.ast.type.ClassOrInterfaceType;
import com.github.javaparser.ast.type.ReferenceType;
import com.github.javaparser.ast.type.Type;
import com.github.javaparser.ast.type.TypeParameter;
import com.github.javaparser.ast.visitor.VoidVisitorAdapter;
import com.azure.tools.apiview.processor.model.APIListing;
import com.azure.tools.apiview.processor.model.ChildItem;
import com.azure.tools.apiview.processor.model.Token;
import com.azure.tools.apiview.processor.model.TypeKind;

import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.ArrayList;
import java.util.Comparator;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.Optional;
import java.util.concurrent.atomic.AtomicInteger;
import java.util.stream.Collectors;

import static com.azure.tools.apiview.processor.model.TokenKind.*;

public class ASTAnalyser implements Analyser {
    // a map of type name to unique identifier, used for navigation
    private final Map<String, String> knownTypes;

    // a map of package names to a list of types within that package
    private final Map<String, List<String>> packageNamesToTypesMap;

    private final Map<String, ChildItem> packageNameToNav;

    private int indent;

    public ASTAnalyser() {
        this.indent = 0;
        this.knownTypes = new HashMap<>();
        this.packageNamesToTypesMap = new HashMap<>();
        this.packageNameToNav = new HashMap<>();
    }

    @Override
    public void analyse(List<Path> allFiles, APIListing apiListing) {
        // firstly we filter out the files we don't care about
        allFiles = allFiles.stream()
           .filter(path -> {
               String inputFileName = path.toString();
               if (Files.isDirectory(path)) return false;
               else if (inputFileName.contains("implementation")) return false;
               else if (inputFileName.equals("package-info.java")) return false;
               else if (!inputFileName.endsWith(".java")) return false;
               else return true;
           }).collect(Collectors.toList());

        // then we do a pass to build a map of all known types and package names, and a map of package names to nav items,
        // followed by a pass to tokenise each file
        allFiles.stream()
                .map(this::scanForTypes)
                .collect(Collectors.toList())
                .stream()
                .filter(Optional::isPresent)
                .map(Optional::get)
                .sorted((s1, s2) -> s1.path.compareTo(s2.path))
                .forEach(scanClass -> processSingleFile(scanClass, apiListing));

        // build the navigation
        packageNameToNav.values().stream()
                .filter(childItem -> !childItem.getChildItem().isEmpty())
                .sorted(Comparator.comparing(ChildItem::getText))
                .forEach(apiListing::addChildItem);
    }

    private static class ScanClass {
        private ParseResult<CompilationUnit> parseResult;
        private Path path;

        public ScanClass(Path path, ParseResult<CompilationUnit> parseResult) {
            this.parseResult = parseResult;
            this.path = path;
        }
    }

    private Optional<ScanClass> scanForTypes(Path path) {
        try {
            ParseResult<CompilationUnit> parseResult = new JavaParser().parse(path);
            new ScanForClassTypeVisitor().visit(parseResult.getResult().get(), knownTypes);
            return Optional.of(new ScanClass(path, parseResult));
        } catch (IOException e) {
            e.printStackTrace();
            return Optional.empty();
        }
    }

    private void processSingleFile(ScanClass scanClass, APIListing apiListing) {
        new ClassOrInterfaceVisitor().visit(scanClass.parseResult.getResult().get(), apiListing.getTokens());
    }

    private class ClassOrInterfaceVisitor extends VoidVisitorAdapter<List<Token>> {
        private ChildItem parentNav;

        ClassOrInterfaceVisitor() {   }

        ClassOrInterfaceVisitor(ChildItem parentNav) {
            this.parentNav = parentNav;
        }

        @Override
        public void visit(CompilationUnit compilationUnit, List<Token> tokens) {
            NodeList<TypeDeclaration<?>> types = compilationUnit.getTypes();
            for (final TypeDeclaration<?> typeDeclaration : types) {
                visitClassOrInterfaceOrEnumDeclaration(typeDeclaration, tokens);
            }
        }

        private void visitClassOrInterfaceOrEnumDeclaration(TypeDeclaration<?> typeDeclaration, List<Token> tokens) {
            final String fullyQualifiedName = typeDeclaration.getFullyQualifiedName().orElse("");
            final boolean isPrivate = getTypeDeclaration(typeDeclaration, fullyQualifiedName, tokens);
            // Skip rest of code if the class, interface, or enum declaration is private or package private
            if (isPrivate) {
                return;
            }

            if (typeDeclaration.isEnumDeclaration()) {
                getEnumEntries((EnumDeclaration)typeDeclaration, fullyQualifiedName, tokens);
            }

            // Get if the declaration is interface or not
            boolean isInterfaceDeclaration = false;
            if (typeDeclaration.isClassOrInterfaceDeclaration()) {
                // could be interface or custom annotation @interface
                isInterfaceDeclaration = typeDeclaration.asClassOrInterfaceDeclaration().isInterface() || typeDeclaration.isAnnotationDeclaration();
            }

            // get fields
            tokeniseFields(isInterfaceDeclaration, typeDeclaration.getFields(), fullyQualifiedName, tokens);
            // get Constructors
            tokeniseConstructorsOrMethods(isInterfaceDeclaration, typeDeclaration.getConstructors(), fullyQualifiedName, tokens);
            // get Methods
            tokeniseConstructorsOrMethods(isInterfaceDeclaration, typeDeclaration.getMethods(), fullyQualifiedName, tokens);
            // get Inner classes
            tokeniseInnerClasses(typeDeclaration.getMembers(), tokens);

            // close class
            tokens.add(makeWhitespace());
            tokens.add(new Token(PUNCTUATION, "}"));
            tokens.add(new Token(NEW_LINE, ""));
        }

        private void getEnumEntries(EnumDeclaration enumDeclaration, String fullyQualifiedName, List<Token> tokens) {
            final NodeList<EnumConstantDeclaration> enumConstantDeclarations = enumDeclaration.getEntries();
            int size = enumConstantDeclarations.size();
            indent();

            AtomicInteger counter = new AtomicInteger();

            enumConstantDeclarations.forEach(enumConstantDeclaration -> {
                tokens.add(makeWhitespace());

                // create a unique id for enum constants
                final String name = enumConstantDeclaration.getNameAsString();
                final String definitionId = makeId(fullyQualifiedName + "." + name);
                tokens.add(new Token(MEMBER_NAME, name, definitionId));

                enumConstantDeclaration.getArguments().forEach(expression -> {
                    tokens.add(new Token(PUNCTUATION, "("));
                    tokens.add(new Token(TEXT, expression.toString()));
                    tokens.add(new Token(PUNCTUATION, ")"));
                });

                if (counter.getAndIncrement() < size - 1) {
                    tokens.add(new Token(PUNCTUATION, ","));
                } else {
                    tokens.add(new Token(PUNCTUATION, ";"));
                }
                tokens.add(new Token(NEW_LINE, ""));
            });

            unindent();
        }

        private boolean getTypeDeclaration(TypeDeclaration<?> typeDeclaration, String fullyQualifiedName, List<Token> tokens) {
            // Skip if the class is private or package-private
            if (isPrivateOrPackagePrivate(typeDeclaration.getAccessSpecifier())) {
                return true;
            }

            // public class or interface or enum
            tokens.add(makeWhitespace());

            // Get modifiers
            getModifiers(typeDeclaration.getModifiers(), tokens);

            // Get type kind
            TypeKind typeKind;
            if (typeDeclaration.isClassOrInterfaceDeclaration()) {
                typeKind = ((ClassOrInterfaceDeclaration)typeDeclaration).isInterface() ? TypeKind.INTERFACE : TypeKind.CLASS;
            } else if (typeDeclaration.isEnumDeclaration()) {
                typeKind = TypeKind.ENUM;
            } else if (typeDeclaration.isAnnotationDeclaration()) {
                typeKind = TypeKind.INTERFACE;
            } else {
                typeKind = TypeKind.UNKNOWN;
            }

            // Create navigation for this class and add it to the parent
            final String className = typeDeclaration.getNameAsString();
            final String packageName = fullyQualifiedName.substring(0, fullyQualifiedName.lastIndexOf("."));
            final String classId = makeId(fullyQualifiedName);
            ChildItem classNav = new ChildItem(classId, className, typeKind);
            if (parentNav == null) {
                packageNameToNav.get(packageName).addChildItem(classNav);
            } else {
                parentNav.addChildItem(classNav);
            }
            parentNav = classNav;

            if (typeDeclaration.isAnnotationDeclaration()) {
                tokens.add(new Token(KEYWORD, "@"));
            }

            tokens.add(new Token(KEYWORD, typeKind.getName()));
            tokens.add(new Token(WHITESPACE, " "));
            tokens.add(new Token(TYPE_NAME, className, classId));

            NodeList<ClassOrInterfaceType> implementedTypes = null;
            // Type parameters of class definition
            if (typeDeclaration.isClassOrInterfaceDeclaration()) {
                final ClassOrInterfaceDeclaration classOrInterfaceDeclaration = (ClassOrInterfaceDeclaration)typeDeclaration;

                // Get type parameters
                getTypeParameters(classOrInterfaceDeclaration.getTypeParameters(), tokens);

                // Extends a class
                final NodeList<ClassOrInterfaceType> extendedTypes = classOrInterfaceDeclaration.getExtendedTypes();
                if (!extendedTypes.isEmpty()) {
                    tokens.add(new Token(WHITESPACE, " "));
                    tokens.add(new Token(KEYWORD, "extends"));
                    tokens.add(new Token(WHITESPACE, " "));

                    // Java only extends one class if it is class, but can extends multiple interfaces if it is interface itself
                    if (extendedTypes.isNonEmpty()) {
                        for (int i = 0, max = extendedTypes.size() ; i < max; i++) {
                            final ClassOrInterfaceType extendedType = extendedTypes.get(i);
                            getType(extendedType, tokens);

                            if (i < max - 1) {
                                tokens.add(new Token(PUNCTUATION, ","));
                                tokens.add(new Token(WHITESPACE, " "));
                            }
                        }
                    }
                }
                // Assign implement types
                implementedTypes = classOrInterfaceDeclaration.getImplementedTypes();
            } else if (typeDeclaration.isEnumDeclaration()) {
                final EnumDeclaration enumDeclaration = (EnumDeclaration)typeDeclaration;
                // Assign implement types
                implementedTypes = enumDeclaration.getImplementedTypes();
            } else if (typeDeclaration.isAnnotationDeclaration()) {
                // no-op
            } else {
                System.err.println("Not a class, interface or enum declaration");
            }

            // implements interfaces
            if (implementedTypes != null && !implementedTypes.isEmpty()) {
                tokens.add(new Token(WHITESPACE, " "));
                tokens.add(new Token(KEYWORD, "implements"));
                tokens.add(new Token(WHITESPACE, " "));

                for (final ClassOrInterfaceType implementedType : implementedTypes) {
                    getType(implementedType, tokens);
                    tokens.add(new Token(PUNCTUATION, ","));
                    tokens.add(new Token(WHITESPACE, " "));
                }
                if (!implementedTypes.isEmpty()) {
                    tokens.remove(tokens.size() - 1);
                    tokens.remove(tokens.size() - 1);
                }
            }
            // open ClassOrInterfaceDeclaration
            tokens.add(new Token(WHITESPACE, " "));
            tokens.add(new Token(PUNCTUATION, "{"));
            tokens.add(new Token(NEW_LINE, ""));

            return false;
        }

        private void tokeniseFields(boolean isInterfaceDeclaration, List<? extends FieldDeclaration> fieldDeclarations, String fullyQualifiedName, List<Token> tokens) {
            indent();
            for (FieldDeclaration fieldDeclaration : fieldDeclarations) {
                // By default , interface has public abstract methods if there is no access specifier declared
                if (isInterfaceDeclaration) {
                    // no-op - we take all methods in the method
                } else if (isPrivateOrPackagePrivate(fieldDeclaration.getAccessSpecifier())) {
                    // Skip if not public API
                    continue;
                }

                tokens.add(makeWhitespace());

                final NodeList<Modifier> fieldModifiers = fieldDeclaration.getModifiers();
                // public, protected, static, final
                for (final Modifier fieldModifier: fieldModifiers) {
                    tokens.add(new Token(KEYWORD, fieldModifier.toString()));
                }

                // field type and name
                final NodeList<VariableDeclarator> variableDeclarators = fieldDeclaration.getVariables();

                if (variableDeclarators.size() > 1) {
                    getType(fieldDeclaration, tokens);

                    for (VariableDeclarator variableDeclarator : variableDeclarators) {
                        final String name = variableDeclarator.getNameAsString();
                        final String definitionId = makeId(fullyQualifiedName + "." + name);
                        tokens.add(new Token(MEMBER_NAME, name, definitionId));
                        tokens.add(new Token(PUNCTUATION, ","));
                        tokens.add(new Token(WHITESPACE, " "));
                    }
                    tokens.remove(tokens.size() - 1);
                    tokens.remove(tokens.size() - 1);
                } else if (variableDeclarators.size() == 1) {
                    getType(fieldDeclaration, tokens);
                    final VariableDeclarator variableDeclarator = variableDeclarators.get(0);
                    final String name = variableDeclarator.getNameAsString();
                    final String definitionId = makeId(fullyQualifiedName + "." + name);
                    tokens.add(new Token(MEMBER_NAME, name, definitionId));

                    final Optional<Expression> variableDeclaratorOption = variableDeclarator.getInitializer();
                    if (variableDeclaratorOption.isPresent()) {
                        tokens.add(new Token(WHITESPACE, " "));
                        tokens.add(new Token(PUNCTUATION, "="));
                        tokens.add(new Token(WHITESPACE, " "));
                        tokens.add(new Token(TEXT, variableDeclaratorOption.get().toString()));
                    }
                }

                // close the variable declaration
                tokens.add(new Token(PUNCTUATION, ";"));
                tokens.add(new Token(NEW_LINE, ""));
            }
            unindent();
        }

        private void tokeniseConstructorsOrMethods(boolean isInterfaceDeclaration, List<? extends CallableDeclaration<?>> callableDeclarations, String fullyQualifiedName, List<Token> tokens) {
            indent();
            for (final CallableDeclaration<?> callableDeclaration : callableDeclarations) {
                // By default , interface has public abstract methods if there is no access specifier declared
                if (isInterfaceDeclaration) {
                    // no-op - we take all methods in the method
                } else if (isPrivateOrPackagePrivate(callableDeclaration.getAccessSpecifier())) {
                    // Skip if not public API
                    continue;
                }

                tokens.add(makeWhitespace());

                // modifiers
                getModifiers(callableDeclaration.getModifiers(), tokens);

                // type parameters of methods
                getTypeParameters(callableDeclaration.getTypeParameters(), tokens);

                // type name
                if (callableDeclaration instanceof MethodDeclaration) {
                    getType(callableDeclaration, tokens);
                }

                // method name and parameters
                getDeclarationNameAndParameters(callableDeclaration, callableDeclaration.getParameters(), fullyQualifiedName, tokens);

                // throw exceptions
                getThrowException(callableDeclaration, tokens);

                // close statements
                tokens.add(new Token(PUNCTUATION, "{"));
                tokens.add(new Token(PUNCTUATION, "}"));
                tokens.add(new Token(NEW_LINE, ""));
            }
            unindent();
        }

        private void tokeniseInnerClasses(NodeList<BodyDeclaration<?>> bodyDeclarations, List<Token> tokens) {
            for (final BodyDeclaration<?> bodyDeclaration : bodyDeclarations) {
                if (bodyDeclaration.isEnumDeclaration() || bodyDeclaration.isClassOrInterfaceDeclaration()) {
                    indent();
                    new ClassOrInterfaceVisitor(parentNav).visitClassOrInterfaceOrEnumDeclaration(bodyDeclaration.asTypeDeclaration(), tokens);
                    unindent();
                }
            }
        }

        private void getModifiers(NodeList<Modifier> modifiers, List<Token> tokens) {
            for (final Modifier modifier : modifiers) {
                tokens.add(new Token(KEYWORD, modifier.toString()));
            }
        }

        private boolean isPrivateOrPackagePrivate(AccessSpecifier accessSpecifier) {
            return accessSpecifier.equals(AccessSpecifier.PRIVATE)
                    || accessSpecifier.equals(AccessSpecifier.PACKAGE_PRIVATE);
        }

        private void getDeclarationNameAndParameters(CallableDeclaration callableDeclaration, NodeList<Parameter> parameters, String fullyQualifiedName, List<Token> tokens) {
            // create a unique definition id
            final String name = callableDeclaration.getNameAsString();
            final String definitionId = makeId(fullyQualifiedName + "." + callableDeclaration.getDeclarationAsString());
            tokens.add(new Token(MEMBER_NAME, name, definitionId));

            tokens.add(new Token(PUNCTUATION, "("));

            if (!parameters.isEmpty()) {
                for (int i = 0, max = parameters.size(); i < max; i++) {
                    final Parameter parameter = parameters.get(i);
                    getType(parameter, tokens);
                    tokens.add(new Token(WHITESPACE, " "));
                    tokens.add(new Token(TEXT, parameter.getNameAsString()));

                    if (i < max - 1) {
                        tokens.add(new Token(PUNCTUATION, ","));
                        tokens.add(new Token(WHITESPACE, " "));
                    }
                }
            }

            // close declaration
            tokens.add(new Token(PUNCTUATION, ")"));
            tokens.add(new Token(WHITESPACE, " "));
        }

        private void getTypeParameters(NodeList<TypeParameter> typeParameters, List<Token> tokens) {
            final int size = typeParameters.size();
            if (size == 0) {
                return;
            }
            tokens.add(new Token(PUNCTUATION, "<"));
            for (int i = 0; i < size; i++) {
                final TypeParameter typeParameter = typeParameters.get(i);
                getGenericTypeParameter(typeParameter, tokens);
                if (i != size - 1) {
                    tokens.add(new Token(PUNCTUATION, ","));
                    tokens.add(new Token(WHITESPACE, " "));
                }
            }
            tokens.add(new Token(PUNCTUATION, ">"));
            tokens.add(new Token(WHITESPACE, " "));
        }

        private void getGenericTypeParameter(TypeParameter typeParameter, List<Token> tokens) {
            // set navigateToId
            final String typeName = typeParameter.getNameAsString();
            final Token token = new Token(TYPE_NAME, typeName);
            if (knownTypes.containsKey(typeName)) {
                token.setNavigateToId(knownTypes.get(typeName));
            }
            tokens.add(token);

            // get type bounds
            final NodeList<ClassOrInterfaceType> typeBounds = typeParameter.getTypeBound();
            final int size = typeBounds.size();
            if (size != 0) {
                tokens.add(new Token(WHITESPACE, " "));
                tokens.add(new Token(KEYWORD, "extends"));
                tokens.add(new Token(WHITESPACE, " "));
                for (int i = 0; i < size; i++) {
                    getType(typeBounds.get(i), tokens);
                }
            }
        }

        private void getThrowException(CallableDeclaration callableDeclaration, List<Token> tokens) {
            final NodeList<ReferenceType> thrownExceptions = callableDeclaration.getThrownExceptions();
            if (thrownExceptions.size() == 0) {
                return;
            }

            tokens.add(new Token(KEYWORD, "throws"));
            tokens.add(new Token(WHITESPACE, " "));

            for (int i = 0, max = thrownExceptions.size(); i < max; i++) {
                tokens.add(new Token(TYPE_NAME, thrownExceptions.get(i).getElementType().toString()));
                if (i < max - 1) {
                    tokens.add(new Token(PUNCTUATION, ","));
                    tokens.add(new Token(WHITESPACE, " "));
                }
            }
            tokens.add(new Token(WHITESPACE, " "));
        }

        private void getType(Object type, List<Token> tokens) {
            if (type instanceof Parameter) {
                getClassType(((NodeWithType) type).getType(), tokens);
                if (((Parameter) type).isVarArgs()) {
                    tokens.add(new Token(PUNCTUATION, "..."));
                }
            } else if (type instanceof MethodDeclaration) {
                getClassType(((MethodDeclaration)type).getType(), tokens);
                tokens.add(new Token(WHITESPACE, " "));
            } else if (type instanceof FieldDeclaration) {
                getClassType(((FieldDeclaration)type).getElementType(), tokens);
                tokens.add(new Token(WHITESPACE, " "));
            } else if (type instanceof ClassOrInterfaceType) {
                getClassType(((Type)type), tokens);
            } else {
                System.err.println("Unknown type " + type + " of type " + type.getClass());
            }
        }

        private void getClassType(Type type, List<Token> tokens) {
            if (type.isArrayType()) {
                getClassType(type.getElementType(), tokens);
                //TODO: need to correct int[][] scenario
                tokens.add(new Token(PUNCTUATION, "[]"));
            } else if (type.isPrimitiveType() || type.isVoidType()) {
                tokens.add(new Token(TYPE_NAME, type.toString()));
            } else if (type.isReferenceType() || type.isTypeParameter() || type.isWildcardType()) {
                getTypeDFS(type, tokens);
            } else {
                System.err.println("Unknown type");
            }
        }

        private void getTypeDFS(Node node, List<Token> tokens) {
            final List<Node> nodes = node.getChildNodes();
            final int childrenSize = nodes.size();
            if (childrenSize <= 1) {
                final String typeName = node.toString();
                final Token token = new Token(TYPE_NAME, typeName);
                if (knownTypes.containsKey(typeName)) {
                    token.setNavigateToId(knownTypes.get(typeName));
                }
                tokens.add(token);
                return;
            }

            for (int i = 0; i < childrenSize; i++) {
                final Node currentNode = nodes.get(i);

                if (i == 1) {
                    tokens.add(new Token(PUNCTUATION, "<"));
                }

                getTypeDFS(currentNode, tokens);

                if (i != 0) {
                    if (i == childrenSize - 1) {
                        tokens.add(new Token(PUNCTUATION, ">"));
                    } else {
                        tokens.add(new Token(PUNCTUATION, ","));
                        tokens.add(new Token(WHITESPACE, " "));
                    }
                }
            }
        }
    }

    private class ScanForClassTypeVisitor extends VoidVisitorAdapter<Map<String, String>> {
        @Override
        public void visit(CompilationUnit compilationUnit, Map<String, String> arg) {
            for (final TypeDeclaration<?> typeDeclaration : compilationUnit.getTypes()) {
                getTypeDeclaration(typeDeclaration, arg);
            }
        }

        private void getTypeDeclaration(TypeDeclaration<?> typeDeclaration, Map<String, String> knownTypes) {
            // Skip if the class is private or package-private
            if (isPrivateOrPackagePrivate(typeDeclaration.getAccessSpecifier())) {
                return;
            }

            if (! (typeDeclaration.isClassOrInterfaceDeclaration() || typeDeclaration.isEnumDeclaration())) {
                return;
            }

            final String fullQualifiedName = typeDeclaration.getFullyQualifiedName().get();

            // determine the package name for this class
            final String typeName = typeDeclaration.getNameAsString();
            final String packageName = fullQualifiedName.substring(0, fullQualifiedName.lastIndexOf("."));
            packageNamesToTypesMap.computeIfAbsent(packageName, name -> new ArrayList<>()).add(typeName);

            // generate a navigation item for each new package, but we don't add them to the parent yet
            packageNameToNav.computeIfAbsent(packageName, name -> new ChildItem(packageName, TypeKind.NAMESPACE));

            knownTypes.put(typeName, makeId(fullQualifiedName));

            // now do internal types
            typeDeclaration.getMembers().stream()
                    .filter(m -> m.isEnumDeclaration() || m.isClassOrInterfaceDeclaration())
                    .forEach(m -> getTypeDeclaration(m.asTypeDeclaration(), knownTypes));
        }
    }

    private boolean isPrivateOrPackagePrivate(AccessSpecifier accessSpecifier) {
        return (accessSpecifier == AccessSpecifier.PRIVATE) || (accessSpecifier == AccessSpecifier.PACKAGE_PRIVATE);
    }

    private boolean isPrivate(AccessSpecifier accessSpecifier) {
        return accessSpecifier == AccessSpecifier.PRIVATE;
    }

    private String makeId(String fullPath) {
        return fullPath.replaceAll(" ", "-");
    }

    private void indent() {
        indent += 4;
    }

    private void unindent() {
        indent = Math.max(indent - 4, 0);
    }

    private Token makeWhitespace() {
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < indent; i++) {
            sb.append(" ");
        }
        return new Token(WHITESPACE, sb.toString());
    }
}
