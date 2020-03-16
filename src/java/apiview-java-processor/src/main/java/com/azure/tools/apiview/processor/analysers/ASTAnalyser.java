package com.azure.tools.apiview.processor.analysers;

import com.azure.tools.apiview.processor.diagnostics.Diagnostics;
import com.azure.tools.apiview.processor.model.APIListing;
import com.azure.tools.apiview.processor.model.ChildItem;
import com.azure.tools.apiview.processor.model.Token;
import com.azure.tools.apiview.processor.model.TypeKind;
import com.github.javaparser.ParserConfiguration;
import com.github.javaparser.StaticJavaParser;
import com.github.javaparser.TokenRange;
import com.github.javaparser.ast.CompilationUnit;
import com.github.javaparser.ast.ImportDeclaration;
import com.github.javaparser.ast.Modifier;
import com.github.javaparser.ast.Node;
import com.github.javaparser.ast.NodeList;
import com.github.javaparser.ast.body.AnnotationDeclaration;
import com.github.javaparser.ast.body.AnnotationMemberDeclaration;
import com.github.javaparser.ast.body.BodyDeclaration;
import com.github.javaparser.ast.body.CallableDeclaration;
import com.github.javaparser.ast.body.ClassOrInterfaceDeclaration;
import com.github.javaparser.ast.body.ConstructorDeclaration;
import com.github.javaparser.ast.body.EnumConstantDeclaration;
import com.github.javaparser.ast.body.EnumDeclaration;
import com.github.javaparser.ast.body.FieldDeclaration;
import com.github.javaparser.ast.body.MethodDeclaration;
import com.github.javaparser.ast.body.Parameter;
import com.github.javaparser.ast.body.TypeDeclaration;
import com.github.javaparser.ast.body.VariableDeclarator;
import com.github.javaparser.ast.expr.AnnotationExpr;
import com.github.javaparser.ast.expr.Expression;
import com.github.javaparser.ast.nodeTypes.NodeWithType;
import com.github.javaparser.ast.type.ClassOrInterfaceType;
import com.github.javaparser.ast.type.ReferenceType;
import com.github.javaparser.ast.type.Type;
import com.github.javaparser.ast.type.TypeParameter;
import com.github.javaparser.ast.visitor.VoidVisitorAdapter;
import com.github.javaparser.symbolsolver.JavaSymbolSolver;
import com.github.javaparser.symbolsolver.resolution.typesolvers.CombinedTypeSolver;
import com.github.javaparser.symbolsolver.resolution.typesolvers.ReflectionTypeSolver;

import java.io.File;
import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.Comparator;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.Optional;
import java.util.concurrent.atomic.AtomicInteger;
import java.util.stream.Collectors;

import static com.azure.tools.apiview.processor.analysers.util.ASTUtils.getPackageName;
import static com.azure.tools.apiview.processor.analysers.util.ASTUtils.isInterfaceType;
import static com.azure.tools.apiview.processor.analysers.util.ASTUtils.isPrivateOrPackagePrivate;
import static com.azure.tools.apiview.processor.analysers.util.ASTUtils.isTypeAPublicAPI;
import static com.azure.tools.apiview.processor.analysers.util.ASTUtils.makeId;
import static com.azure.tools.apiview.processor.model.TokenKind.KEYWORD;
import static com.azure.tools.apiview.processor.model.TokenKind.MEMBER_NAME;
import static com.azure.tools.apiview.processor.model.TokenKind.NEW_LINE;
import static com.azure.tools.apiview.processor.model.TokenKind.PUNCTUATION;
import static com.azure.tools.apiview.processor.model.TokenKind.TEXT;
import static com.azure.tools.apiview.processor.model.TokenKind.TYPE_NAME;
import static com.azure.tools.apiview.processor.model.TokenKind.WHITESPACE;

public class ASTAnalyser implements Analyser {
    private final APIListing apiListing;

    private final Map<String, ChildItem> packageNameToNav;

    private int indent;

    public ASTAnalyser(File inputFile, APIListing apiListing) {
        this.apiListing = apiListing;
        this.indent = 0;
        this.packageNameToNav = new HashMap<>();
    }

    @Override
    public void analyse(List<Path> allFiles) {
        // firstly we filter out the files we don't care about
        allFiles = allFiles.stream()
           .filter(path -> {
               String inputFileName = path.toString();
               if (Files.isDirectory(path)) return false;
               else if (inputFileName.contains("implementation")) return false;
               else if (inputFileName.contains("package-info.java")) return false;
               else if (inputFileName.contains("module-info.java")) return false;
               else return inputFileName.endsWith(".java");
           }).collect(Collectors.toList());

        // then we do a pass to build a map of all known types and package names, and a map of package names to nav items,
        // followed by a pass to tokenise each file
        allFiles.stream()
                .map(this::scanForTypes)
                .filter(Optional::isPresent)
                .map(Optional::get)
                .sorted(Comparator.comparing(s -> s.path))
                .forEach(this::processSingleFile);

        // build the navigation
        packageNameToNav.values().stream()
                .filter(childItem -> !childItem.getChildItem().isEmpty())
                .sorted(Comparator.comparing(ChildItem::getText))
                .forEach(apiListing::addChildItem);
    }

    private static class ScanClass {
        private CompilationUnit compilationUnit;
        private Path path;

        public ScanClass(Path path, CompilationUnit compilationUnit) {
            this.compilationUnit = compilationUnit;
            this.path = path;
        }
    }

    private Optional<ScanClass> scanForTypes(Path path) {
        try {
            // Set up a minimal type solver that only looks at the classes used to run this sample.
            CombinedTypeSolver combinedTypeSolver = new CombinedTypeSolver();
            combinedTypeSolver.add(new ReflectionTypeSolver(false));
//            combinedTypeSolver.add(new SourceJarTypeSolver(inputFile));
          
            ParserConfiguration parserConfiguration = new ParserConfiguration()
                  .setStoreTokens(true)
                  .setSymbolResolver(new JavaSymbolSolver(combinedTypeSolver));

            // Configure JavaParser to use type resolution
            StaticJavaParser.setConfiguration(parserConfiguration);

            CompilationUnit compilationUnit = StaticJavaParser.parse(path);
            new ScanForClassTypeVisitor().visit(compilationUnit, null);
            return Optional.of(new ScanClass(path, compilationUnit));
        } catch (IOException e) {
            e.printStackTrace();
            return Optional.empty();
        }
    }

    private void processSingleFile(ScanClass scanClass) {
        new ClassOrInterfaceVisitor().visit(scanClass.compilationUnit, null);
    }

    private class ClassOrInterfaceVisitor extends VoidVisitorAdapter<Void> {
        private ChildItem parentNav;

        ClassOrInterfaceVisitor() {   }

        ClassOrInterfaceVisitor(ChildItem parentNav) {
            this.parentNav = parentNav;
        }

        @Override
        public void visit(CompilationUnit compilationUnit, Void args) {
            NodeList<TypeDeclaration<?>> types = compilationUnit.getTypes();
            for (final TypeDeclaration<?> typeDeclaration : types) {
                visitClassOrInterfaceOrEnumDeclaration(typeDeclaration);
            }

            Diagnostics.scan(compilationUnit, apiListing);
        }

        private void visitClassOrInterfaceOrEnumDeclaration(TypeDeclaration<?> typeDeclaration) {
            // public custom annotation @interface's annotations
            if (typeDeclaration.isAnnotationDeclaration() && !isPrivateOrPackagePrivate(typeDeclaration.getAccessSpecifier())) {
                final AnnotationDeclaration annotationDeclaration = (AnnotationDeclaration) typeDeclaration;

                // Annotations on top of AnnotationDeclaration class, for example
                // @Retention(RUNTIME)
                // @Target(PARAMETER)
                // public @interface BodyParam {}
                final NodeList<AnnotationExpr> annotations = annotationDeclaration.getAnnotations();
                for (AnnotationExpr annotation : annotations) {
                    final Optional<TokenRange> tokenRange = annotation.getTokenRange();
                    if (!tokenRange.isPresent()) {
                        continue;
                    }
                    final TokenRange annotationTokenRange = tokenRange.get();
                    // TODO: could be more specified instead of string
                    final String name = annotationTokenRange.toString();
                    addToken(new Token(KEYWORD, name));
                    addToken(new Token(NEW_LINE, ""));
                }
            }

            // Skip if the class is private or package-private
            final boolean isPrivate = getTypeDeclaration(typeDeclaration);
            // Skip rest of code if the class, interface, or enum declaration is private or package private
            if (isPrivate) {
                return;
            }

            if (typeDeclaration.isEnumDeclaration()) {
                getEnumEntries((EnumDeclaration)typeDeclaration);
            }

            // Get if the declaration is interface or not
            boolean isInterfaceDeclaration = isInterfaceType(typeDeclaration);

            // public custom annotation @interface's members
            if (typeDeclaration.isAnnotationDeclaration() && !isPrivateOrPackagePrivate(typeDeclaration.getAccessSpecifier())) {
                final AnnotationDeclaration annotationDeclaration = (AnnotationDeclaration) typeDeclaration;
                tokeniseAnnotationMember(annotationDeclaration);
            }

            // get fields
            tokeniseFields(isInterfaceDeclaration, typeDeclaration);

            // get Constructors
            final List<ConstructorDeclaration> constructors = typeDeclaration.getConstructors();
            if (constructors.isEmpty()) {
                // add default constructor if there is no constructor at all
                if (!isInterfaceDeclaration) {
                    addDefaultConstructor(typeDeclaration);
                } else {
                    // skip and do nothing if there is no constructor in the interface.
                }
            } else {
                tokeniseConstructorsOrMethods(isInterfaceDeclaration, constructors);
            }

            // get Methods
            tokeniseConstructorsOrMethods(isInterfaceDeclaration, typeDeclaration.getMethods());

            // get Inner classes
            tokeniseInnerClasses(typeDeclaration.getMembers());

            // close class
            addToken(makeWhitespace());
            addToken(new Token(PUNCTUATION, "}"));
            addToken(new Token(NEW_LINE, ""));
        }

        private void getEnumEntries(EnumDeclaration enumDeclaration) {
            final NodeList<EnumConstantDeclaration> enumConstantDeclarations = enumDeclaration.getEntries();
            int size = enumConstantDeclarations.size();
            indent();

            AtomicInteger counter = new AtomicInteger();

            enumConstantDeclarations.forEach(enumConstantDeclaration -> {
                addToken(makeWhitespace());

                // create a unique id for enum constants
                final String name = enumConstantDeclaration.getNameAsString();
                final String definitionId = makeId(enumDeclaration.getFullyQualifiedName().get() + "." + counter);
                addToken(new Token(MEMBER_NAME, name, definitionId));

                enumConstantDeclaration.getArguments().forEach(expression -> {
                    addToken(new Token(PUNCTUATION, "("));
                    addToken(new Token(TEXT, expression.toString()));
                    addToken(new Token(PUNCTUATION, ")"));
                });

                if (counter.getAndIncrement() < size - 1) {
                    addToken(new Token(PUNCTUATION, ","));
                } else {
                    addToken(new Token(PUNCTUATION, ";"));
                }
                addToken(new Token(NEW_LINE, ""));
            });

            unindent();
        }

        private boolean getTypeDeclaration(TypeDeclaration<?> typeDeclaration) {
            // Skip if the class is private or package-private, unless it is a nested type defined inside a public interface
            if (!isTypeAPublicAPI(typeDeclaration)) {
                return true;
            }

            // public class or interface or enum
            addToken(makeWhitespace());

            // Get modifiers
            getModifiers(typeDeclaration.getModifiers());

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
            final String packageName = getPackageName(typeDeclaration);
            final String classId = makeId(typeDeclaration);
            ChildItem classNav = new ChildItem(classId, className, typeKind);
            if (parentNav == null) {
                packageNameToNav.get(packageName).addChildItem(classNav);
            } else {
                parentNav.addChildItem(classNav);
            }
            parentNav = classNav;

            if (typeDeclaration.isAnnotationDeclaration()) {
                addToken(new Token(KEYWORD, "@"));
            }

            addToken(new Token(KEYWORD, typeKind.getName()));
            addToken(new Token(WHITESPACE, " "));
            addToken(new Token(TYPE_NAME, className, classId));

            NodeList<ClassOrInterfaceType> implementedTypes = null;
            // Type parameters of class definition
            if (typeDeclaration.isClassOrInterfaceDeclaration()) {
                final ClassOrInterfaceDeclaration classOrInterfaceDeclaration = (ClassOrInterfaceDeclaration)typeDeclaration;

                // Get type parameters
                getTypeParameters(classOrInterfaceDeclaration.getTypeParameters());

                // Extends a class
                final NodeList<ClassOrInterfaceType> extendedTypes = classOrInterfaceDeclaration.getExtendedTypes();
                if (!extendedTypes.isEmpty()) {
                    addToken(new Token(WHITESPACE, " "));
                    addToken(new Token(KEYWORD, "extends"));
                    addToken(new Token(WHITESPACE, " "));

                    // Java only extends one class if it is class, but can extends multiple interfaces if it is interface itself
                    if (extendedTypes.isNonEmpty()) {
                        for (int i = 0, max = extendedTypes.size() ; i < max; i++) {
                            final ClassOrInterfaceType extendedType = extendedTypes.get(i);
                            getType(extendedType);

                            if (i < max - 1) {
                                addToken(new Token(PUNCTUATION, ","));
                                addToken(new Token(WHITESPACE, " "));
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
                addToken(new Token(WHITESPACE, " "));
                addToken(new Token(KEYWORD, "implements"));
                addToken(new Token(WHITESPACE, " "));

                for (final ClassOrInterfaceType implementedType : implementedTypes) {
                    getType(implementedType);
                    addToken(new Token(PUNCTUATION, ","));
                    addToken(new Token(WHITESPACE, " "));
                }
                if (!implementedTypes.isEmpty()) {
                    apiListing.getTokens().remove(apiListing.getTokens().size() - 1);
                    apiListing.getTokens().remove(apiListing.getTokens().size() - 1);
                }
            }
            // open ClassOrInterfaceDeclaration
            addToken(new Token(WHITESPACE, " "));
            addToken(new Token(PUNCTUATION, "{"));
            addToken(new Token(NEW_LINE, ""));

            return false;
        }

        private void tokeniseAnnotationMember(AnnotationDeclaration annotationDeclaration) {
            indent();
            // Member methods in the annotation declaration
            NodeList<BodyDeclaration<?>> annotationDeclarationMembers = annotationDeclaration.getMembers();
            for (BodyDeclaration<?> bodyDeclaration : annotationDeclarationMembers) {
                Optional<AnnotationMemberDeclaration> annotationMemberDeclarationOptional = bodyDeclaration.toAnnotationMemberDeclaration();
                if (!annotationMemberDeclarationOptional.isPresent()) {
                    continue;
                }
                final AnnotationMemberDeclaration annotationMemberDeclaration = annotationMemberDeclarationOptional.get();

                addToken(makeWhitespace());
                getClassType(annotationMemberDeclaration.getType());
                addToken(new Token(WHITESPACE, " "));

                final String name = annotationMemberDeclaration.getNameAsString();
                final String definitionId = makeId(annotationDeclaration);

                addToken(new Token(MEMBER_NAME, name, definitionId));
                addToken(new Token(PUNCTUATION, "("));
                addToken(new Token(PUNCTUATION, ")"));

                // default value
                final Optional<Expression> defaultValueOptional = annotationMemberDeclaration.getDefaultValue();
                if (defaultValueOptional.isPresent()) {
                    addToken(new Token(WHITESPACE, " "));
                    addToken(new Token(KEYWORD, "default"));
                    addToken(new Token(WHITESPACE, " "));

                    final Expression defaultValueExpr = defaultValueOptional.get();
                    final String value = defaultValueExpr.toString();
                    addToken(new Token(KEYWORD, value));
                }

                addToken(new Token(PUNCTUATION, ";"));
                addToken(new Token(NEW_LINE, ""));
            }
            unindent();
        }

        private void tokeniseFields(boolean isInterfaceDeclaration, TypeDeclaration<?> typeDeclaration) {
            final List<? extends FieldDeclaration> fieldDeclarations = typeDeclaration.getFields();
            final String fullPathName = typeDeclaration.getFullyQualifiedName().get();

            indent();
            for (FieldDeclaration fieldDeclaration : fieldDeclarations) {
                // By default , interface has public abstract methods if there is no access specifier declared
                if (isInterfaceDeclaration) {
                    // no-op - we take all methods in the method
                } else if (isPrivateOrPackagePrivate(fieldDeclaration.getAccessSpecifier())) {
                    // Skip if not public API
                    continue;
                }

                addToken(makeWhitespace());

                final NodeList<Modifier> fieldModifiers = fieldDeclaration.getModifiers();
                // public, protected, static, final
                for (final Modifier fieldModifier: fieldModifiers) {
                    addToken(new Token(KEYWORD, fieldModifier.toString()));
                }

                // field type and name
                final NodeList<VariableDeclarator> variableDeclarators = fieldDeclaration.getVariables();

                if (variableDeclarators.size() > 1) {
                    getType(fieldDeclaration);

                    for (VariableDeclarator variableDeclarator : variableDeclarators) {
                        final String name = variableDeclarator.getNameAsString();
                        final String definitionId = makeId(fullPathName + "." + variableDeclarator.getName());
                        addToken(new Token(MEMBER_NAME, name, definitionId));
                        addToken(new Token(PUNCTUATION, ","));
                        addToken(new Token(WHITESPACE, " "));
                    }
                    apiListing.getTokens().remove(apiListing.getTokens().size() - 1);
                    apiListing.getTokens().remove(apiListing.getTokens().size() - 1);
                } else if (variableDeclarators.size() == 1) {
                    getType(fieldDeclaration);
                    final VariableDeclarator variableDeclarator = variableDeclarators.get(0);
                    final String name = variableDeclarator.getNameAsString();
                    final String definitionId = makeId(fullPathName + "." + variableDeclarator.getName());
                    addToken(new Token(MEMBER_NAME, name, definitionId));

                    final Optional<Expression> variableDeclaratorOption = variableDeclarator.getInitializer();
                    if (variableDeclaratorOption.isPresent()) {
                        addToken(new Token(WHITESPACE, " "));
                        addToken(new Token(PUNCTUATION, "="));
                        addToken(new Token(WHITESPACE, " "));
                        addToken(new Token(TEXT, variableDeclaratorOption.get().toString()));
                    }
                }

                // close the variable declaration
                addToken(new Token(PUNCTUATION, ";"));
                addToken(new Token(NEW_LINE, ""));
            }
            unindent();
        }

        private void tokeniseConstructorsOrMethods(boolean isInterfaceDeclaration, List<? extends CallableDeclaration<?>> callableDeclarations) {
            indent();

            boolean isAllPrivateOrPackagePrivate = callableDeclarations.stream()
                    .filter(BodyDeclaration::isConstructorDeclaration)
                    .allMatch(callableDeclaration -> isPrivateOrPackagePrivate(callableDeclaration.getAccessSpecifier()));

            for (final CallableDeclaration<?> callableDeclaration : callableDeclarations) {
                // By default , interface has public abstract methods if there is no access specifier declared
                if (isInterfaceDeclaration) {
                    // no-op - we take all methods in the method
                } else if (callableDeclaration.isConstructorDeclaration()) {
                    // if there is at least one public constructor, only explore the public but skip all private constructors
                    if (!isAllPrivateOrPackagePrivate && isPrivateOrPackagePrivate(callableDeclaration.getAccessSpecifier())) {
                        continue;
                    }
                } else if (isPrivateOrPackagePrivate(callableDeclaration.getAccessSpecifier())) {
                    // Skip if not public API
                    continue;
                }

                addToken(makeWhitespace());

                // modifiers
                getModifiers(callableDeclaration.getModifiers());

                // type parameters of methods
                getTypeParameters(callableDeclaration.getTypeParameters());

                // type name
                if (callableDeclaration instanceof MethodDeclaration) {
                    getType(callableDeclaration);
                }

                // method name and parameters
                getDeclarationNameAndParameters(callableDeclaration, callableDeclaration.getParameters());

                // throw exceptions
                getThrowException(callableDeclaration);

                // close statements
                addToken(new Token(PUNCTUATION, "{"));
                addToken(new Token(PUNCTUATION, "}"));
                addToken(new Token(NEW_LINE, ""));
            }
            unindent();
        }

        private void tokeniseInnerClasses(NodeList<BodyDeclaration<?>> bodyDeclarations) {
            for (final BodyDeclaration<?> bodyDeclaration : bodyDeclarations) {
                if (bodyDeclaration.isEnumDeclaration() || bodyDeclaration.isClassOrInterfaceDeclaration()) {
                    indent();
                    new ClassOrInterfaceVisitor(parentNav).visitClassOrInterfaceOrEnumDeclaration(bodyDeclaration.asTypeDeclaration());
                    unindent();
                }
            }
        }

        private void getModifiers(NodeList<Modifier> modifiers) {
            for (final Modifier modifier : modifiers) {
                addToken(new Token(KEYWORD, modifier.toString()));
            }
        }

        private void getDeclarationNameAndParameters(CallableDeclaration callableDeclaration, NodeList<Parameter> parameters) {
            // create a unique definition id
            final String name = callableDeclaration.getNameAsString();
            final String definitionId = makeId(callableDeclaration);
            addToken(new Token(MEMBER_NAME, name, definitionId));

            addToken(new Token(PUNCTUATION, "("));

            if (!parameters.isEmpty()) {
                for (int i = 0, max = parameters.size(); i < max; i++) {
                    final Parameter parameter = parameters.get(i);
                    getType(parameter);
                    addToken(new Token(WHITESPACE, " "));
                    addToken(new Token(TEXT, parameter.getNameAsString()));

                    if (i < max - 1) {
                        addToken(new Token(PUNCTUATION, ","));
                        addToken(new Token(WHITESPACE, " "));
                    }
                }
            }

            // close declaration
            addToken(new Token(PUNCTUATION, ")"));
            addToken(new Token(WHITESPACE, " "));
        }

        private void getTypeParameters(NodeList<TypeParameter> typeParameters) {
            final int size = typeParameters.size();
            if (size == 0) {
                return;
            }
            addToken(new Token(PUNCTUATION, "<"));
            for (int i = 0; i < size; i++) {
                final TypeParameter typeParameter = typeParameters.get(i);
                getGenericTypeParameter(typeParameter);
                if (i != size - 1) {
                    addToken(new Token(PUNCTUATION, ","));
                    addToken(new Token(WHITESPACE, " "));
                }
            }
            addToken(new Token(PUNCTUATION, ">"));
        }

        private void getGenericTypeParameter(TypeParameter typeParameter) {
            // set navigateToId
            final String typeName = typeParameter.getNameAsString();
            final Token token = new Token(TYPE_NAME, typeName);
            if (apiListing.getKnownTypes().containsKey(typeName)) {
                token.setNavigateToId(apiListing.getKnownTypes().get(typeName));
            }
            addToken(token);

            // get type bounds
            final NodeList<ClassOrInterfaceType> typeBounds = typeParameter.getTypeBound();
            final int size = typeBounds.size();
            if (size != 0) {
                addToken(new Token(WHITESPACE, " "));
                addToken(new Token(KEYWORD, "extends"));
                addToken(new Token(WHITESPACE, " "));
                for (int i = 0; i < size; i++) {
                    getType(typeBounds.get(i));
                }
            }
        }

        private void getThrowException(CallableDeclaration callableDeclaration) {
            final NodeList<ReferenceType> thrownExceptions = callableDeclaration.getThrownExceptions();
            if (thrownExceptions.size() == 0) {
                return;
            }

            addToken(new Token(KEYWORD, "throws"));
            addToken(new Token(WHITESPACE, " "));

            for (int i = 0, max = thrownExceptions.size(); i < max; i++) {
                addToken(new Token(TYPE_NAME, thrownExceptions.get(i).getElementType().toString()));
                if (i < max - 1) {
                    addToken(new Token(PUNCTUATION, ","));
                    addToken(new Token(WHITESPACE, " "));
                }
            }
            addToken(new Token(WHITESPACE, " "));
        }

        private void getType(Object type) {
            if (type instanceof Parameter) {
                getClassType(((NodeWithType) type).getType());
                if (((Parameter) type).isVarArgs()) {
                    addToken(new Token(PUNCTUATION, "..."));
                }
            } else if (type instanceof MethodDeclaration) {
                getClassType(((MethodDeclaration)type).getType());
                addToken(new Token(WHITESPACE, " "));
            } else if (type instanceof FieldDeclaration) {
                getClassType(((FieldDeclaration)type).getElementType());
                addToken(new Token(WHITESPACE, " "));
            } else if (type instanceof ClassOrInterfaceType) {
                getClassType(((Type)type));
            } else {
                System.err.println("Unknown type " + type + " of type " + type.getClass());
            }
        }

        private void getClassType(Type type) {
            if (type.isArrayType()) {
                getClassType(type.getElementType());
                //TODO: need to correct int[][] scenario
                addToken(new Token(PUNCTUATION, "[]"));
            } else if (type.isPrimitiveType() || type.isVoidType()) {
                addToken(new Token(TYPE_NAME, type.toString()));
            } else if (type.isReferenceType() || type.isTypeParameter() || type.isWildcardType()) {
                getTypeDFS(type);
            } else {
                System.err.println("Unknown type");
            }
        }

        private void getTypeDFS(Node node) {
            final List<Node> nodes = node.getChildNodes();
            final int childrenSize = nodes.size();
            if (childrenSize <= 1) {
                final String typeName = node.toString();
                final Token token = new Token(TYPE_NAME, typeName);
                if (apiListing.getKnownTypes().containsKey(typeName)) {
                    token.setNavigateToId(apiListing.getKnownTypes().get(typeName));
                }
                addToken(token);
                return;
            }

            for (int i = 0; i < childrenSize; i++) {
                final Node currentNode = nodes.get(i);

                if (i == 1) {
                    addToken(new Token(PUNCTUATION, "<"));
                }

                getTypeDFS(currentNode);

                if (i != 0) {
                    if (i == childrenSize - 1) {
                        addToken(new Token(PUNCTUATION, ">"));
                    } else {
                        addToken(new Token(PUNCTUATION, ","));
                        addToken(new Token(WHITESPACE, " "));
                    }
                }
            }
        }

        private void addDefaultConstructor(TypeDeclaration<?> typeDeclaration) {
            indent();

            addToken(makeWhitespace());
            addToken(new Token(KEYWORD, "public"));
            addToken(new Token(WHITESPACE, " "));
            final String name = typeDeclaration.getNameAsString();
            final String definitionId = makeId(typeDeclaration.getNameAsString());
            addToken(new Token(MEMBER_NAME, name, definitionId));
            addToken(new Token(PUNCTUATION, "("));
            addToken(new Token(PUNCTUATION, ")"));
            addToken(new Token(WHITESPACE, " "));

            // close statements
            addToken(new Token(PUNCTUATION, "{"));
            addToken(new Token(PUNCTUATION, "}"));
            addToken(new Token(NEW_LINE, ""));

            unindent();
        }
    }

    private class ScanForClassTypeVisitor extends VoidVisitorAdapter<Map<String, String>> {
        @Override
        public void visit(CompilationUnit compilationUnit, Map<String, String> arg) {
            for (final TypeDeclaration<?> typeDeclaration : compilationUnit.getTypes()) {
                getTypeDeclaration(typeDeclaration);
            }

            // we build up a map between types and the packages they are in, for use in our diagnostic rules
            compilationUnit.getImports().stream()
                    .map(ImportDeclaration::getName)
                    .forEach(name -> name.getQualifier().ifPresent(packageName -> {
                        apiListing.addPackageTypeMapping(packageName.toString(), name.getIdentifier());
                    }));
        }
    }

    /*
     * This method is only called in relation to building up the types for linking, it does not build up the actual
     * text output that is displayed to the user.
     */
    private void getTypeDeclaration(TypeDeclaration<?> typeDeclaration) {
        // Skip if the class is private or package-private, unless it is a nested type defined inside a public interface
        if (!isTypeAPublicAPI(typeDeclaration)) {
            return;
        }

        final boolean isInterfaceType = typeDeclaration.isClassOrInterfaceDeclaration();
        if (! (isInterfaceType || typeDeclaration.isEnumDeclaration())) {
            return;
        }

        final String fullQualifiedName = typeDeclaration.getFullyQualifiedName().get();

        // determine the package name for this class
        final String typeName = typeDeclaration.getNameAsString();
        final String packageName = fullQualifiedName.substring(0, fullQualifiedName.lastIndexOf("."));
        apiListing.addPackageTypeMapping(packageName, typeName);

        // generate a navigation item for each new package, but we don't add them to the parent yet
        packageNameToNav.computeIfAbsent(packageName, name -> new ChildItem(packageName, TypeKind.NAMESPACE));

        apiListing.getKnownTypes().put(typeName, makeId(typeDeclaration));

        // now do internal types
        typeDeclaration.getMembers().stream()
                .filter(m -> m.isEnumDeclaration() || m.isClassOrInterfaceDeclaration())
                .forEach(m -> getTypeDeclaration(m.asTypeDeclaration()));
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

    private void addToken(Token token) {
        apiListing.getTokens().add(token);
    }
}
