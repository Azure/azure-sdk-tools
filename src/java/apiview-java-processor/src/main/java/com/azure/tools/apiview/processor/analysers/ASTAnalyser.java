package com.azure.tools.apiview.processor.analysers;

import com.azure.tools.apiview.processor.analysers.util.MiscUtils;
import com.azure.tools.apiview.processor.analysers.util.TokenModifier;
import com.azure.tools.apiview.processor.diagnostics.Diagnostics;
import com.azure.tools.apiview.processor.model.APIListing;
import com.azure.tools.apiview.processor.model.ChildItem;
import com.azure.tools.apiview.processor.model.Token;
import com.azure.tools.apiview.processor.model.TypeKind;
import com.azure.tools.apiview.processor.model.maven.Dependency;
import com.azure.tools.apiview.processor.model.maven.Pom;
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
import com.github.javaparser.ast.comments.Comment;
import com.github.javaparser.ast.comments.JavadocComment;
import com.github.javaparser.ast.expr.AnnotationExpr;
import com.github.javaparser.ast.expr.Expression;
import com.github.javaparser.ast.expr.MemberValuePair;
import com.github.javaparser.ast.expr.Name;
import com.github.javaparser.ast.expr.NormalAnnotationExpr;
import com.github.javaparser.ast.modules.ModuleDeclaration;
import com.github.javaparser.ast.nodeTypes.NodeWithAnnotations;
import com.github.javaparser.ast.nodeTypes.NodeWithType;
import com.github.javaparser.ast.type.ClassOrInterfaceType;
import com.github.javaparser.ast.type.ReferenceType;
import com.github.javaparser.ast.type.Type;
import com.github.javaparser.ast.type.TypeParameter;
import com.github.javaparser.ast.visitor.VoidVisitorAdapter;
import com.github.javaparser.symbolsolver.JavaSymbolSolver;
import com.github.javaparser.symbolsolver.resolution.typesolvers.CombinedTypeSolver;
import com.github.javaparser.symbolsolver.resolution.typesolvers.JavaParserTypeSolver;
import com.github.javaparser.symbolsolver.resolution.typesolvers.ReflectionTypeSolver;

import java.io.File;
import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.Arrays;
import java.util.Comparator;
import java.util.HashMap;
import java.util.HashSet;
import java.util.List;
import java.util.Map;
import java.util.Optional;
import java.util.Set;
import java.util.TreeMap;
import java.util.concurrent.atomic.AtomicInteger;
import java.util.function.Consumer;
import java.util.stream.Collector;
import java.util.stream.Collectors;
import java.util.stream.Stream;

import static com.azure.tools.apiview.processor.analysers.util.ASTUtils.getPackageName;
import static com.azure.tools.apiview.processor.analysers.util.ASTUtils.isInterfaceType;
import static com.azure.tools.apiview.processor.analysers.util.ASTUtils.isPrivateOrPackagePrivate;
import static com.azure.tools.apiview.processor.analysers.util.ASTUtils.isTypeAPublicAPI;
import static com.azure.tools.apiview.processor.analysers.util.ASTUtils.makeId;
import static com.azure.tools.apiview.processor.model.TokenKind.COMMENT;
import static com.azure.tools.apiview.processor.model.TokenKind.DEPRECATED_RANGE_END;
import static com.azure.tools.apiview.processor.model.TokenKind.DEPRECATED_RANGE_START;
import static com.azure.tools.apiview.processor.model.TokenKind.DOCUMENTATION_RANGE_END;
import static com.azure.tools.apiview.processor.model.TokenKind.DOCUMENTATION_RANGE_START;
import static com.azure.tools.apiview.processor.model.TokenKind.KEYWORD;
import static com.azure.tools.apiview.processor.model.TokenKind.MEMBER_NAME;
import static com.azure.tools.apiview.processor.model.TokenKind.NEW_LINE;
import static com.azure.tools.apiview.processor.model.TokenKind.PUNCTUATION;
import static com.azure.tools.apiview.processor.model.TokenKind.TEXT;
import static com.azure.tools.apiview.processor.model.TokenKind.TYPE_NAME;
import static com.azure.tools.apiview.processor.model.TokenKind.WHITESPACE;
import static com.azure.tools.apiview.processor.model.TokenKind.SKIP_DIFF_START;
import static com.azure.tools.apiview.processor.model.TokenKind.SKIP_DIFF_END;

import static com.azure.tools.apiview.processor.analysers.util.TokenModifier.*;

public class ASTAnalyser implements Analyser {
    public static final String MAVEN_KEY = "Maven";
    public static final String MODULE_INFO_KEY = "module-info";

    private static final boolean SHOW_JAVADOC = true;
    private static final Set<String> BLOCKED_ANNOTATIONS = new HashSet<String>() {{
        add("ServiceMethod");
        add("SuppressWarnings");
    }};

    private final APIListing apiListing;

    private final Map<String, JavadocComment> packageNameToPackageInfoJavaDoc;

    private final Diagnostics diagnostic;

    private int indent;

    public ASTAnalyser(File inputFile, APIListing apiListing) {
        this.apiListing = apiListing;
        this.indent = 0;
        this.packageNameToPackageInfoJavaDoc = new HashMap<>();
        this.diagnostic = new Diagnostics();
    }

    @Override
    public void analyse(List<Path> allFiles) {
        /*
         * Begin by filtering out file paths that we don't care about.
         *
         * Then build a map of all known types and package names and a map of package names to navigation items.
         *
         * Finally tokenize each file.
         */
        allFiles.stream()
            .filter(this::filterFilePaths)
            .map(this::scanForTypes)
            .filter(Optional::isPresent)
            .map(Optional::get)
            .collect(Collectors.groupingBy(ScanClass::getPackageName, TreeMap::new, Collectors.toList()))
            .forEach(this::processPackage);

        // we conclude by doing a final pass over all diagnostics to enable them to do any final analysis based on
        // the already-executed individual scans
        diagnostic.scanFinal(apiListing);
    }

    private boolean filterFilePaths(Path filePath) {
        String fileName = filePath.toString();
        // Skip paths that are directories or are in implementation.
        if (Files.isDirectory(filePath) || fileName.contains("implementation")) {
            return false;
        } else {
            // Only include Java files.
            return fileName.endsWith(".java") || fileName.endsWith("pom.xml");
        }
    }

    // This class represents a class that is going to go through the analysis pipeline, and it collects
    // together all useful properties that were identified so that they can form part of the analysis.
    private static class ScanClass implements Comparable<ScanClass> {
        private final CompilationUnit compilationUnit;
        private final Path path;
        private String primaryTypeName;
        private String packageName = "";

        public ScanClass(Path path, CompilationUnit compilationUnit) {
            this.compilationUnit = compilationUnit;
            this.path = path;

            if (compilationUnit != null) {
                compilationUnit.getPackageDeclaration().ifPresent(packageDeclaration -> {
                    packageName = packageDeclaration.getNameAsString();
                });
                compilationUnit.getPrimaryTypeName().ifPresent(name -> primaryTypeName = name);
            } else {
                primaryTypeName = "";
            }
        }

        public CompilationUnit getCompilationUnit() {
            return compilationUnit;
        }

        public Path getPath() {
            return path;
        }

        public String getPackageName() {
            return packageName;
        }

        @Override
        public int compareTo(final ScanClass o) {
            return packageName.compareTo(o.packageName);
        }
    }

    private Optional<ScanClass> scanForTypes(Path path) {
        if (path.toString().endsWith("pom.xml")) {
            return Optional.of(new ScanClass(path, null));
        }
        try {
            // Set up a minimal type solver that only looks at the classes used to run this sample.
            CombinedTypeSolver combinedTypeSolver = new CombinedTypeSolver();
            combinedTypeSolver.add(new ReflectionTypeSolver(false));
//            combinedTypeSolver.add(new SourceJarTypeSolver(inputFile));

            ParserConfiguration parserConfiguration = new ParserConfiguration()
                .setStoreTokens(true)
                .setSymbolResolver(new JavaSymbolSolver(combinedTypeSolver))
                .setLanguageLevel(ParserConfiguration.LanguageLevel.JAVA_11);

            // Configure JavaParser to use type resolution
            StaticJavaParser.setConfiguration(parserConfiguration);

            CompilationUnit compilationUnit = StaticJavaParser.parse(path);
            new ScanForClassTypeVisitor().visit(compilationUnit, null);

            if (path.endsWith("package-info.java")) {
                compilationUnit.getPackageDeclaration().ifPresent(pd -> {
                    compilationUnit.getAllComments().stream()
                        .filter(Comment::isJavadocComment)
                        .map(Comment::asJavadocComment)
                        .findFirst()
                        .ifPresent(comment -> packageNameToPackageInfoJavaDoc.put(pd.getNameAsString(), comment));
                });

                return Optional.empty();
            } else {
                return Optional.of(new ScanClass(path, compilationUnit));
            }
        } catch (IOException e) {
            e.printStackTrace();
            return Optional.empty();
        }
    }

    private void processPackage(String packageName, List<ScanClass> scanClasses) {
        // lets see if we have javadoc for this packageName
        if (packageNameToPackageInfoJavaDoc.containsKey(packageName)) {
            visitJavaDoc(packageNameToPackageInfoJavaDoc.get(packageName));
        }

        addToken(new Token(KEYWORD, "package"), SPACE);

        Token packageToken;
        if (packageName.isEmpty()) {
            packageToken = new Token(TEXT, "<root package>");
        } else {
            packageToken = new Token(TYPE_NAME, packageName, packageName);
            packageToken.setNavigateToId(packageName);
        }
        addToken(packageToken, SPACE);
        addToken(new Token(PUNCTUATION, "{"), NEWLINE);

        indent();

        scanClasses.stream()
            .sorted(Comparator.comparing(s -> s.primaryTypeName))
            .forEach(this::processSingleFile);

        unindent();

        addToken(new Token(PUNCTUATION, "}"), NEWLINE);
    }

    private void processSingleFile(ScanClass scanClass) {
        if (scanClass.getPath().toString().endsWith("pom.xml")) {
            // we want to represent the pom.xml file in short form
            tokeniseMavenPom(apiListing.getMavenPom());
        } else {
            new ClassOrInterfaceVisitor().visit(scanClass.compilationUnit, null);
        }
    }

    private void tokeniseMavenPom(Pom mavenPom) {
        apiListing.addChildItem(new ChildItem(MAVEN_KEY, MAVEN_KEY, TypeKind.ASSEMBLY));

        addToken(makeWhitespace());
        addToken(new Token(KEYWORD, "maven", MAVEN_KEY), SPACE);
        addToken(new Token(PUNCTUATION, "{"), NEWLINE);
        indent();

       addToken(new Token(SKIP_DIFF_START));
       // parent
       String gavStr = mavenPom.getParent().getGroupId() + ":" + mavenPom.getParent().getArtifactId() + ":" + mavenPom.getParent().getVersion();
       tokeniseKeyValue("parent", gavStr, "");

       // properties
       gavStr = mavenPom.getGroupId() + ":" + mavenPom.getArtifactId() + ":" + mavenPom.getVersion();
       tokeniseKeyValue("properties", gavStr, "");

        // configuration
        boolean showJacoco = mavenPom.getJacocoMinLineCoverage() != null &&
                                 mavenPom.getJacocoMinBranchCoverage() != null;
        boolean showCheckStyle = mavenPom.getCheckstyleExcludes() != null &&
                                 !mavenPom.getCheckstyleExcludes().isEmpty();

        if (showJacoco || showCheckStyle) {
            addToken(INDENT, new Token(KEYWORD, "configuration"), SPACE);
            addToken(new Token(PUNCTUATION, "{"), NEWLINE);
            indent();
            if (showCheckStyle) {
                tokeniseKeyValue("checkstyle-excludes", mavenPom.getCheckstyleExcludes(), "");
            }
            if (showJacoco) {
                addToken(INDENT, new Token(KEYWORD, "jacoco"), SPACE);
                addToken(new Token(PUNCTUATION, "{"), NEWLINE);
                indent();
                tokeniseKeyValue("min-line-coverage", mavenPom.getJacocoMinLineCoverage(), "jacoco");
                tokeniseKeyValue("min-branch-coverage", mavenPom.getJacocoMinBranchCoverage(), "jacoco");
                unindent();
                addToken(INDENT, new Token(PUNCTUATION, "}"), NEWLINE);
            }
            unindent();
            addToken(INDENT, new Token(PUNCTUATION, "}"), NEWLINE);
        }

        // dependencies
        addToken(INDENT, new Token(KEYWORD, "dependencies"), SPACE);
        addToken(new Token(PUNCTUATION, "{"), NEWLINE);

        indent();
        mavenPom.getDependencies().stream().collect(Collectors.groupingBy(Dependency::getScope)).forEach((k,v) -> {
            if ("test".equals(k)) {
                // we don't care to present test scope dependencies
                return;
            }
            String scope = k.equals("") ? "compile" : k;

            addToken(makeWhitespace());
            addToken(new Token(COMMENT, "// " + scope + " scope"), NEWLINE);

            v.forEach(d -> {
                addToken(makeWhitespace());
                String gav = d.getGroupId() + ":" + d.getArtifactId() + ":" + d.getVersion();
                addToken(new Token(TEXT, gav, gav));
                addNewLine();
            });

            addNewLine();
        });

        unindent();
        addToken(INDENT, new Token(PUNCTUATION, "}"), NEWLINE);
        addToken(new Token(SKIP_DIFF_END));

        // allowed dependencies (in maven-enforcer)
//        if (!mavenPom.getAllowedDependencies().isEmpty()) {
//            addToken(INDENT, new Token(KEYWORD, "allowed-dependencies"), SPACE);
//            addToken(new Token(PUNCTUATION, "{"), NEWLINE);
//            indent();
//            mavenPom.getAllowedDependencies().stream().forEach(value -> {
//                addToken(INDENT, new Token(TEXT, value, value), NEWLINE);
//            });
//            unindent();
//            addToken(INDENT, new Token(PUNCTUATION, "}"), NEWLINE);
//        }

        // close maven
        unindent();
        addToken(INDENT, new Token(PUNCTUATION, "}"), NEWLINE);
    }

    private void tokeniseKeyValue(String key, Object value, String linkPrefix) {
        addToken(makeWhitespace());
        addToken(new Token(KEYWORD, key));
        addToken(new Token(PUNCTUATION, ":"), SPACE);
        addToken(new Token(TEXT, value == null ? "<default value>" : value.toString(), linkPrefix + "-" + key + "-" + value), NEWLINE);
    }

    private class ClassOrInterfaceVisitor extends VoidVisitorAdapter<Void> {
        private ChildItem parentNav;

        ClassOrInterfaceVisitor() {
            this(null);
        }

        ClassOrInterfaceVisitor(ChildItem parentNav) {
            this.parentNav = parentNav;
        }

        @Override
        public void visit(CompilationUnit compilationUnit, Void args) {
            compilationUnit.getModule().ifPresent(this::visitModuleDeclaration);

            NodeList<TypeDeclaration<?>> types = compilationUnit.getTypes();
            for (final TypeDeclaration<?> typeDeclaration : types) {
                visitClassOrInterfaceOrEnumDeclaration(typeDeclaration);
            }

            diagnostic.scanIndividual(compilationUnit, apiListing);
        }

        private void visitClassOrInterfaceOrEnumDeclaration(TypeDeclaration<?> typeDeclaration) {
            // Skip if the class is private or package-private, unless it is a nested type defined inside a public interface
            if (!isTypeAPublicAPI(typeDeclaration)) {
                return;
            }

            visitJavaDoc(typeDeclaration.getJavadocComment());

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
                    addToken(INDENT, new Token(KEYWORD, name), NEWLINE);
                }
            }

            getTypeDeclaration(typeDeclaration);

            if (typeDeclaration.isEnumDeclaration()) {
                getEnumEntries((EnumDeclaration) typeDeclaration);
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
                // add default constructor if there is no constructor at all, except interface and enum
                if (!isInterfaceDeclaration && !typeDeclaration.isEnumDeclaration() && !typeDeclaration.isAnnotationDeclaration()) {
                    addDefaultConstructor(typeDeclaration);
                } else {
                    // skip and do nothing if there is no constructor in the interface.
                }
            } else {
                tokeniseConstructorsOrMethods(typeDeclaration, isInterfaceDeclaration, true, constructors);
            }

            // get Methods
            tokeniseConstructorsOrMethods(typeDeclaration, isInterfaceDeclaration, false, typeDeclaration.getMethods());

            // get Inner classes
            tokeniseInnerClasses(typeDeclaration.getChildNodes()
                .stream()
                .filter(n -> n instanceof TypeDeclaration)
                .map(n -> (TypeDeclaration<?>) n));

            if (isInterfaceDeclaration) {
                if (typeDeclaration.getMembers().isEmpty()) {
                    // we have an empty interface declaration, it is probably a marker interface and we will leave a
                    // comment to that effect
                    indent();
                    addComment("// This interface is does not declare any API.");
                    unindent();
                }
            }

            // close class
            addToken(makeWhitespace());
            addToken(new Token(PUNCTUATION, "}"), NEWLINE);
        }

        private void visitModuleDeclaration(ModuleDeclaration moduleDeclaration) {
            addToken(makeWhitespace());
            addToken(new Token(KEYWORD, "module"), SPACE);
            addToken(new Token(TYPE_NAME, moduleDeclaration.getNameAsString(), MODULE_INFO_KEY), SPACE);
            addToken(new Token(PUNCTUATION, "{"), NEWLINE);

            moduleDeclaration.getDirectives().forEach(moduleDirective -> {
                indent();
                addToken(makeWhitespace());

                moduleDirective.ifModuleRequiresStmt(d -> {
                    addToken(new Token(KEYWORD, "requires"), SPACE);

                    if (d.isTransitive()) {
                        addToken(new Token(KEYWORD, "transitive"), SPACE);
                    }

                    addToken(new Token(TYPE_NAME, d.getNameAsString(), makeId(MODULE_INFO_KEY + "-requires-" + d.getNameAsString())));
                    addToken(new Token(PUNCTUATION, ";"), NEWLINE);
                });

                moduleDirective.ifModuleExportsStmt(d -> {
                    addToken(new Token(KEYWORD, "exports"), SPACE);
                    addToken(new Token(TYPE_NAME, d.getNameAsString(), makeId(MODULE_INFO_KEY + "-exports-" + d.getNameAsString())));

                    NodeList<Name> names = d.getModuleNames();

                    if (!names.isEmpty()) {
                        addToken(new Token(WHITESPACE, " "));
                        addToken(new Token(KEYWORD, "to"), SPACE);

                        for (int i = 0; i < names.size(); i++) {
                            addToken(new Token(TYPE_NAME, names.get(i).toString()));

                            if (i < names.size() - 1) {
                                addToken(new Token(PUNCTUATION, ","), SPACE);
                            }
                        }
                    }

                    addToken(new Token(PUNCTUATION, ";"), NEWLINE);
                });

                moduleDirective.ifModuleOpensStmt(d -> {
                    addToken(new Token(KEYWORD, "opens"), SPACE);
                    addToken(new Token(TYPE_NAME, d.getNameAsString(), makeId(MODULE_INFO_KEY + "-opens-" + d.getNameAsString())));

                    NodeList<Name> names = d.getModuleNames();
                    if (names.size() > 0) {
                        addToken(new Token(WHITESPACE, " "));
                        addToken(new Token(KEYWORD, "to"), SPACE);

                        for (int i = 0; i < names.size(); i++) {
                            addToken(new Token(TYPE_NAME, names.get(i).toString()));

                            if (i < names.size() - 1) {
                                addToken(new Token(PUNCTUATION, ","), SPACE);
                            }
                        }
                    }

                    addToken(new Token(PUNCTUATION, ";"), NEWLINE);
                });

                moduleDirective.ifModuleUsesStmt(d -> {
                    addToken(new Token(KEYWORD, "uses"), SPACE);
                    addToken(new Token(TYPE_NAME, d.getNameAsString(), makeId(MODULE_INFO_KEY + "-uses-" + d.getNameAsString())));
                    addToken(new Token(PUNCTUATION, ";"), NEWLINE);
                });

                moduleDirective.ifModuleProvidesStmt(d -> {
                    addToken(new Token(KEYWORD, "provides"), SPACE);
                    addToken(new Token(TYPE_NAME, d.getNameAsString(), makeId(MODULE_INFO_KEY + "-provides-" + d.getNameAsString())), SPACE);
                    addToken(new Token(KEYWORD, "with"), SPACE);

                    NodeList<Name> names = d.getWith();
                    for (int i = 0; i < names.size(); i++) {
                        addToken(new Token(TYPE_NAME, names.get(i).toString()));

                        if (i < names.size() - 1) {
                            addToken(new Token(PUNCTUATION, ","), SPACE);
                        }
                    }

                    addToken(new Token(PUNCTUATION, ";"), NEWLINE);
                });

                unindent();
            });

            // close module
            addToken(INDENT, new Token(PUNCTUATION, "}"), NEWLINE);
        }

        private void getEnumEntries(EnumDeclaration enumDeclaration) {
            final NodeList<EnumConstantDeclaration> enumConstantDeclarations = enumDeclaration.getEntries();
            int size = enumConstantDeclarations.size();
            indent();

            AtomicInteger counter = new AtomicInteger();

            enumConstantDeclarations.forEach(enumConstantDeclaration -> {
                visitJavaDoc(enumConstantDeclaration.getJavadocComment());

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
                addNewLine();
            });

            unindent();
        }

        private void getTypeDeclaration(TypeDeclaration<?> typeDeclaration) {
            // public class or interface or enum
            getAnnotations(typeDeclaration, true, true);

            // Get modifiers
            addToken(makeWhitespace());
            getModifiers(typeDeclaration.getModifiers());

            // Get type kind
            TypeKind typeKind;
            if (typeDeclaration.isClassOrInterfaceDeclaration()) {
                typeKind = ((ClassOrInterfaceDeclaration) typeDeclaration).isInterface() ? TypeKind.INTERFACE : TypeKind.CLASS;
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
                apiListing.addChildItem(packageName, classNav);
            } else {
                parentNav.addChildItem(classNav);
            }
            parentNav = classNav;

            if (typeDeclaration.isAnnotationDeclaration()) {
                addToken(new Token(KEYWORD, "@"));
            }

            final boolean isDeprecated = typeDeclaration.isAnnotationPresent("Deprecated");

            addToken(new Token(KEYWORD, typeKind.getName()), SPACE);

            if (isDeprecated) {
                addToken(new Token(DEPRECATED_RANGE_START));
            }

            addToken(new Token(TYPE_NAME, className, classId));

            if (isDeprecated) {
                addToken(new Token(DEPRECATED_RANGE_END));
            }

            NodeList<ClassOrInterfaceType> implementedTypes = null;
            // Type parameters of class definition
            if (typeDeclaration.isClassOrInterfaceDeclaration()) {
                final ClassOrInterfaceDeclaration classOrInterfaceDeclaration = (ClassOrInterfaceDeclaration) typeDeclaration;

                // Get type parameters
                getTypeParameters(classOrInterfaceDeclaration.getTypeParameters());

                // Extends a class
                final NodeList<ClassOrInterfaceType> extendedTypes = classOrInterfaceDeclaration.getExtendedTypes();
                if (!extendedTypes.isEmpty()) {
                    addToken(SPACE, new Token(KEYWORD, "extends"), SPACE);

                    // Java only extends one class if it is class, but can extends multiple interfaces if it is interface itself
                    if (extendedTypes.isNonEmpty()) {
                        for (int i = 0, max = extendedTypes.size(); i < max; i++) {
                            final ClassOrInterfaceType extendedType = extendedTypes.get(i);
                            getType(extendedType);

                            if (i < max - 1) {
                                addToken(new Token(PUNCTUATION, ","), SPACE);
                            }
                        }
                    }
                }
                // Assign implement types
                implementedTypes = classOrInterfaceDeclaration.getImplementedTypes();
            } else if (typeDeclaration.isEnumDeclaration()) {
                final EnumDeclaration enumDeclaration = (EnumDeclaration) typeDeclaration;
                // Assign implement types
                implementedTypes = enumDeclaration.getImplementedTypes();
            } else if (typeDeclaration.isAnnotationDeclaration()) {
                // no-op
            } else {
                System.err.println("Not a class, interface or enum declaration");
            }

            // implements interfaces
            if (implementedTypes != null && !implementedTypes.isEmpty()) {
                addToken(SPACE, new Token(KEYWORD, "implements"), SPACE);

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
            addToken(SPACE, new Token(PUNCTUATION, "{"), NEWLINE);
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
                final String definitionId = makeId(annotationDeclaration.getFullyQualifiedName().get() + "." + name);

                addToken(new Token(MEMBER_NAME, name, definitionId));
                addToken(new Token(PUNCTUATION, "("));
                addToken(new Token(PUNCTUATION, ")"));

                // default value
                final Optional<Expression> defaultValueOptional = annotationMemberDeclaration.getDefaultValue();
                if (defaultValueOptional.isPresent()) {
                    addToken(SPACE, new Token(KEYWORD, "default"), SPACE);

                    final Expression defaultValueExpr = defaultValueOptional.get();
                    final String value = defaultValueExpr.toString();
                    addToken(new Token(KEYWORD, value));
                }

                addToken(new Token(PUNCTUATION, ";"), NEWLINE);
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

                visitJavaDoc(fieldDeclaration.getJavadocComment());

                addToken(makeWhitespace());

                // Add annotation for field declaration
                getAnnotations(fieldDeclaration, false, false);

                final NodeList<Modifier> fieldModifiers = fieldDeclaration.getModifiers();
                // public, protected, static, final
                for (final Modifier fieldModifier : fieldModifiers) {
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
                        addToken(new Token(PUNCTUATION, ","), SPACE);
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
                        addToken(SPACE, new Token(PUNCTUATION, "="), SPACE);
                        addToken(new Token(TEXT, variableDeclaratorOption.get().toString()));
                    }
                }

                // close the variable declaration
                addToken(new Token(PUNCTUATION, ";"), NEWLINE);
            }
            unindent();
        }

        private void tokeniseConstructorsOrMethods(final TypeDeclaration<?> typeDeclaration,
            final boolean isInterfaceDeclaration,
            final boolean isConstructor,
            final List<? extends CallableDeclaration<?>> callableDeclarations) {
            indent();

            if (isConstructor) {
                // determining if we are looking at a set of constructors that are all private, indicating that the
                // class is unlikely to be instantiable via 'new' calls.
                // We also must check if there are no constructors, because this indicates that there is the default,
                // no-args public constructor
                final boolean isAllPrivateOrPackagePrivate = !callableDeclarations.isEmpty() && callableDeclarations.stream()
                    .filter(BodyDeclaration::isConstructorDeclaration)
                    .allMatch(callableDeclaration -> isPrivateOrPackagePrivate(callableDeclaration.getAccessSpecifier()));

                if (isAllPrivateOrPackagePrivate) {
                    if (typeDeclaration.isEnumDeclaration()) {
                        unindent();
                        return;
                    }

                    addComment("// This class does not have any public constructors, and is not able to be instantiated using 'new'.");
                    unindent();
                    return;
                }
            }

            // if the class we are looking at is annotated with @ServiceClient, we will break up the methods that are
            // displayed into service methods and non-service methods
            final boolean showGroupings = !isConstructor && typeDeclaration.isAnnotationPresent("ServiceClient");
            Collector<CallableDeclaration<?>, ?, Map<String, List<CallableDeclaration<?>>>> collector = Collectors.groupingBy((CallableDeclaration<?> cd) -> {
                if (showGroupings) {
                    if (cd.isAnnotationPresent("ServiceMethod")) {
                        return "Service Methods";
                    } else {
                        return "Non-Service Methods";
                    }
                } else {
                    return "";
                }
            });

            callableDeclarations.stream()
                .filter(callableDeclaration -> {
                    if (isInterfaceDeclaration) {
                        // By default , interface has public abstract methods if there is no access specifier declared.
                        // we take all methods in the interface.
                        return true;
                    } else if (isPrivateOrPackagePrivate(callableDeclaration.getAccessSpecifier())) {
                        // Skip if not public API
                        return false;
                    }
                    return true;
                })
                .sorted(this::sortMethods)
                .collect(collector)
                .forEach((groupName, group) -> {
                    if (showGroupings && !group.isEmpty()) {
                        // we group inside the APIView each of the groups, so that we can visualise their operations
                        // more clearly
                        addComment("// " + groupName + ":");
                    }

                    group.forEach(callableDeclaration -> {
                        // print the JavaDoc above each method / constructor
                        visitJavaDoc(callableDeclaration.getJavadocComment());

                        addToken(makeWhitespace());

                        // annotations
                        getAnnotations(callableDeclaration, false, false);

                        // modifiers
                        getModifiers(callableDeclaration.getModifiers());

                        // type parameters of methods
                        getTypeParameters(callableDeclaration.getTypeParameters());

                        // if type parameters of method is not empty, we need to add a space before adding type name
                        if (!callableDeclaration.getTypeParameters().isEmpty()) {
                            addToken(new Token(WHITESPACE, " "));
                        }

                        // type name
                        if (callableDeclaration instanceof MethodDeclaration) {
                            getType(callableDeclaration);
                        }

                        // method name and parameters
                        getDeclarationNameAndParameters(callableDeclaration, callableDeclaration.getParameters());

                        // throw exceptions
                        getThrowException(callableDeclaration);

                        // close statements
                        addNewLine();
                    });
                });

            unindent();
        }

        private int sortMethods(CallableDeclaration<?> c1, CallableDeclaration<?> c2) {
            // we try our best to sort the callable methods using the following rules:
            //  * If the method starts with 'set', 'get', or 'is', we strip off the prefix for the sake of comparison
            //  * We do all comparisons in a case-insensitive manner
            //  * Constructors always go at the top
            //  * build* methods always go at the bottom

            final int methodParamCountCompare = Integer.compare(c1.getParameters().size(), c2.getParameters().size());

            if (c1.isConstructorDeclaration()) {
                if (c2.isConstructorDeclaration()) {
                    // if both are constructors, we sort in order of the number of arguments
                    return methodParamCountCompare;
                } else {
                    // only c1 is a constructor, so it goes first
                    return -1;
                }
            } else if (c2.isConstructorDeclaration()) {
                // only c2 is a constructor, so it goes first
                return 1;
            }

            final String fullName1 = c1.getNameAsString();
            String s1 = (fullName1.startsWith("set") || fullName1.startsWith("get") ? fullName1.substring(3)
                : fullName1.startsWith("is") ? fullName1.substring(2) : fullName1).toLowerCase();

            final String fullName2 = c2.getNameAsString();
            String s2 = (fullName2.startsWith("set") || fullName2.startsWith("get") ? fullName2.substring(3)
                : fullName2.startsWith("is") ? fullName2.substring(2) : fullName2).toLowerCase();

            if (s1.startsWith("build")) {
                if (s2.startsWith("build")) {
                    // two 'build' methods, sort alphabetically
                    return s1.compareTo(s2);
                } else {
                    // only s1 is a build method, so it goes last
                    return 1;
                }
            } else if (s2.startsWith("build")) {
                // only s2 is a build method, so it goes last
                return -1;
            }

            int methodNameCompare = s1.compareTo(s2);
            if (methodNameCompare == 0) {
                // they have the same name, so here we firstly compare by the full name (including prefix), and then
                // we compare by number of args
                methodNameCompare = fullName1.compareTo(fullName2);
                if (methodNameCompare == 0) {
                    // compare by number of args
                    return methodParamCountCompare;
                }
            }
            return methodNameCompare;
        }

        private void tokeniseInnerClasses(Stream<TypeDeclaration<?>> innerTypes) {
            innerTypes.forEach(innerType -> {
                if (innerType.isEnumDeclaration() || innerType.isClassOrInterfaceDeclaration()) {
                    indent();
                    new ClassOrInterfaceVisitor(parentNav).visitClassOrInterfaceOrEnumDeclaration(innerType);
                    unindent();
                }
            });
        }

        private void getAnnotations(final NodeWithAnnotations<?> nodeWithAnnotations,
            final boolean showAnnotationProperties,
            final boolean addNewline) {
            Consumer<AnnotationExpr> consumer = annotation -> {
                if (addNewline) {
                    addToken(makeWhitespace());
                }

                addToken(new Token(TYPE_NAME, "@" + annotation.getName().toString()));
                if (showAnnotationProperties) {
                    if (annotation instanceof NormalAnnotationExpr) {
                        addToken(new Token(PUNCTUATION, "("));
                        NodeList<MemberValuePair> pairs = ((NormalAnnotationExpr) annotation).getPairs();
                        for (int i = 0; i < pairs.size(); i++) {
                            MemberValuePair pair = pairs.get(i);

                            addToken(new Token(TEXT, pair.getNameAsString()));
                            addToken(new Token(PUNCTUATION, " = "));

                            Expression valueExpr = pair.getValue();
                            processAnnotationValueExpression(valueExpr);

                            if (i < pairs.size() - 1) {
                                addToken(new Token(PUNCTUATION, ", "));
                            }
                        }

                        addToken(new Token(PUNCTUATION, ")"));
                    }
                }

                if (addNewline) {
                    addNewLine();
                } else {
                    addToken(new Token(WHITESPACE, " "));
                }
            };

            nodeWithAnnotations.getAnnotations()
                    .stream()
                    .filter(annotationExpr -> !BLOCKED_ANNOTATIONS.contains(annotationExpr.getName().getIdentifier())
                            && !annotationExpr.getName().getIdentifier().startsWith("Json"))
                    .forEach(consumer);
        }

        private void processAnnotationValueExpression(Expression valueExpr) {
            if (valueExpr.isClassExpr()) {
                // lookup to see if the type is known about, if so, make it a link, otherwise leave it as text
                String typeName = valueExpr.getChildNodes().get(0).toString();
                if (apiListing.getKnownTypes().containsKey(typeName)) {
                    final Token token = new Token(TYPE_NAME, typeName);
                    token.setNavigateToId(apiListing.getKnownTypes().get(typeName));
                    addToken(token);
                    return;
                }
            } else if (valueExpr.isArrayInitializerExpr()) {
                addToken(new Token(PUNCTUATION, "{ "));
                for (int i = 0; i < valueExpr.getChildNodes().size(); i++) {
                    Node n = valueExpr.getChildNodes().get(i);

                    if (n instanceof Expression) {
                        processAnnotationValueExpression((Expression) n);
                    } else {
                        addToken(new Token(TEXT, valueExpr.toString()));
                    }

                    if (i < valueExpr.getChildNodes().size() - 1) {
                        addToken(new Token(PUNCTUATION, ", "));
                    }
                }
                addToken(new Token(PUNCTUATION, " }"));
                return;
            }

            // if we fall through to here, just treat it as a string
            addToken(new Token(TEXT, valueExpr.toString()));
        }

        private void getModifiers(NodeList<Modifier> modifiers) {
            for (final Modifier modifier : modifiers) {
                addToken(new Token(KEYWORD, modifier.toString()));
            }
        }

        private void getDeclarationNameAndParameters(CallableDeclaration callableDeclaration, NodeList<Parameter> parameters) {
            final boolean isDeprecated = callableDeclaration.isAnnotationPresent("Deprecated");

            // create an unique definition id
            final String name = callableDeclaration.getNameAsString();
            final String definitionId = makeId(callableDeclaration);

            if (isDeprecated) {
                addToken(new Token(DEPRECATED_RANGE_START));
            }

            addToken(new Token(MEMBER_NAME, name, definitionId));

            if (isDeprecated) {
                addToken(new Token(DEPRECATED_RANGE_END));
            }

            addToken(new Token(PUNCTUATION, "("));

            if (!parameters.isEmpty()) {
                for (int i = 0, max = parameters.size(); i < max; i++) {
                    final Parameter parameter = parameters.get(i);
                    getType(parameter);
                    addToken(new Token(WHITESPACE, " "));
                    addToken(new Token(TEXT, parameter.getNameAsString()));

                    if (i < max - 1) {
                        addToken(new Token(PUNCTUATION, ","), SPACE);
                    }
                }
            }

            // close declaration
            addToken(new Token(PUNCTUATION, ")"), SPACE);
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
                    addToken(new Token(PUNCTUATION, ","), SPACE);
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
                addToken(SPACE, new Token(KEYWORD, "extends"), SPACE);
                for (ClassOrInterfaceType typeBound : typeBounds) {
                    getType(typeBound);
                }
            }
        }

        private void getThrowException(CallableDeclaration callableDeclaration) {
            final NodeList<ReferenceType> thrownExceptions = callableDeclaration.getThrownExceptions();
            if (thrownExceptions.size() == 0) {
                return;
            }

            addToken(new Token(KEYWORD, "throws"), SPACE);

            for (int i = 0, max = thrownExceptions.size(); i < max; i++) {
                final String exceptionName = thrownExceptions.get(i).getElementType().toString();
                final Token throwsToken = new Token(TYPE_NAME, exceptionName);

                // we look up the package name in case it is a custom type in the same library,
                // so that we can link to it
                if (apiListing.getTypeToPackageNameMap().containsKey(exceptionName)) {
                    String fullPath = apiListing.getTypeToPackageNameMap().get(exceptionName);
                    throwsToken.setNavigateToId(makeId(fullPath + "." + exceptionName));
                }

                addToken(throwsToken);
                if (i < max - 1) {
                    addToken(new Token(PUNCTUATION, ","), SPACE);
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
                getClassType(((MethodDeclaration) type).getType());
                addToken(new Token(WHITESPACE, " "));
            } else if (type instanceof FieldDeclaration) {
                getClassType(((FieldDeclaration) type).getElementType());
                addToken(new Token(WHITESPACE, " "));
            } else if (type instanceof ClassOrInterfaceType) {
                getClassType(((Type) type));
            } else {
                System.err.println("Unknown type " + type + " of type " + type.getClass());
            }
        }

        private void getClassType(Type type) {
            if (type.isPrimitiveType() || type.isVoidType()) {
                addToken(new Token(TYPE_NAME, type.toString()));
            } else if (type.isReferenceType()) {
                // Array Type
                type.ifArrayType(arrayType -> {
                    getClassType(arrayType.getComponentType());
                    addToken(new Token(PUNCTUATION, "[]"));
                });
                // Class or Interface type
                type.ifClassOrInterfaceType(this::getTypeDFS);

            } else if (type.isWildcardType()) {
                // TODO: add wild card type implementation, #756
            } else if (type.isUnionType()) {
                // TODO: add union type implementation, #756
            } else if (type.isIntersectionType()) {
                // TODO: add intersection type implementation, #756
            } else {
                System.err.println("Unknown type");
            }
        }

        private void getTypeDFS(Node node) {
            final List<Node> nodes = node.getChildNodes();
            final int childrenSize = nodes.size();
            // Recursion's base case: leaf node
            if (childrenSize <= 1) {
                final String typeName = node.toString();
                final Token token = new Token(TYPE_NAME, typeName);
                if (apiListing.getKnownTypes().containsKey(typeName)) {
                    token.setNavigateToId(apiListing.getKnownTypes().get(typeName));
                }
                addToken(token);
                return;
            }

            /*
             * A type, "Map<String, Map<Integer, Double>>", will be treated as three nodes at the same level and has
             * two type arguments, String and Map<Integer, Double>:
             *
             * node one = "Map", node two = "String", and node three = "Map<Integer, Double>"
             * node three, "Map<Integer, Double>", has two type arguments: Integer and Double.
             *
             * But a type with full package name will be treated as two nodes and has no type arguments:
             *
             * E.x., "com.azure.example.TypeA", it has two node,
             * node one = "com.azure.example", node two = "TypeA"
             * Further more, node one has two children: "com.azure" and "example".
             */
            for (int i = 0; i < childrenSize; i++) {
                final Node childNode = nodes.get(i);
                // Opening punctuation character:
                // Second node, need to add a punctuation character right after first node such as 'Set<String>' where
                // '<' is the punctuation character that is required.
                if (i == 1) {
                    // If a node has children, it implies it is a class or interface type. so it is safe to cast.
                    Optional<NodeList<Type>> nodeList = ((ClassOrInterfaceType) node).getTypeArguments();
                    if (nodeList.isPresent()) {
                        // type arguments
                        addToken(new Token(PUNCTUATION, "<"));
                    } else {
                        // full-package type name
                        addToken(new Token(PUNCTUATION, "."));
                    }
                }

                // Recursion
                getTypeDFS(childNode);

                // Closing punctuation character
                if (i == 0) {
                    continue;
                } else if (i == childrenSize - 1) {
                    ((ClassOrInterfaceType) node).getTypeArguments().ifPresent(
                        (NodeList<Type> values) -> addToken(new Token(PUNCTUATION, ">")));
                } else {
                    addToken(new Token(PUNCTUATION, ","), SPACE);
                }
            }
        }

        private void addDefaultConstructor(TypeDeclaration<?> typeDeclaration) {
            indent();

            addToken(INDENT, new Token(KEYWORD, "public"), SPACE);
            final String name = typeDeclaration.getNameAsString();
            final String definitionId = makeId(typeDeclaration.getNameAsString());
            addToken(new Token(MEMBER_NAME, name, definitionId));
            addToken(new Token(PUNCTUATION, "("));
            addToken(new Token(PUNCTUATION, ")"), NEWLINE);

            unindent();
        }
    }

    private class ScanForClassTypeVisitor extends VoidVisitorAdapter<Map<String, String>> {
        @Override
        public void visit(CompilationUnit compilationUnit, Map<String, String> arg) {
            compilationUnit.getModule().ifPresent(moduleDeclaration ->
                apiListing.addChildItem(new ChildItem(MODULE_INFO_KEY, MODULE_INFO_KEY, TypeKind.CLASS)));

            for (final TypeDeclaration<?> typeDeclaration : compilationUnit.getTypes()) {
                buildTypeHierarchyForNavigation(typeDeclaration);
            }

            // we build up a map between types and the packages they are in, for use in our diagnostic rules
            compilationUnit.getImports().stream()
                .map(ImportDeclaration::getName)
                .forEach(name -> name.getQualifier().ifPresent(packageName ->
                    apiListing.addPackageTypeMapping(packageName.toString(), name.getIdentifier())));
        }
    }

    /*
     * This method is only called in relation to building up the types for linking, it does not build up the actual
     * text output that is displayed to the user.
     */
    private void buildTypeHierarchyForNavigation(TypeDeclaration<?> typeDeclaration) {
        // Skip if the class is private or package-private, unless it is a nested type defined inside a public interface
        if (!isTypeAPublicAPI(typeDeclaration)) {
            return;
        }

        final boolean isInterfaceType = typeDeclaration.isClassOrInterfaceDeclaration();
        if (!(isInterfaceType || typeDeclaration.isEnumDeclaration() || typeDeclaration.isAnnotationDeclaration())) {
            return;
        }

        final String fullQualifiedName = typeDeclaration.getFullyQualifiedName().get();

        // determine the package name for this class
        final String typeName = typeDeclaration.getNameAsString();
        final String packageName = fullQualifiedName.substring(0, fullQualifiedName.lastIndexOf("."));
        apiListing.addPackageTypeMapping(packageName, typeName);

        apiListing.getKnownTypes().put(typeName, makeId(typeDeclaration));

        // now do internal types
        typeDeclaration.getMembers().stream()
            .filter(m -> m.isEnumDeclaration() || m.isClassOrInterfaceDeclaration())
            .forEach(m -> buildTypeHierarchyForNavigation(m.asTypeDeclaration()));
    }

    private void visitJavaDoc(Optional<JavadocComment> javadocComment) {
        javadocComment.ifPresent(this::visitJavaDoc);
    }

    private void visitJavaDoc(JavadocComment jd) {
        if (!SHOW_JAVADOC) {
            return;
        }

        addToken(new Token(DOCUMENTATION_RANGE_START));
        Arrays.stream(jd.toString().split(MiscUtils.LINEBREAK)).forEach(line -> {
            // we want to wrap our javadocs so that they are easier to read, so we wrap at 120 chars
            final String wrappedString = MiscUtils.wrap(line, 120);
            Arrays.stream(wrappedString.split(MiscUtils.LINEBREAK)).forEach(line2 -> {
                addToken(makeWhitespace());

                addToken(new Token(COMMENT, line2));
                addNewLine();
            });
        });
        addToken(new Token(DOCUMENTATION_RANGE_END));
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

    private void addComment(String comment) {
        addToken(INDENT, new Token(COMMENT, comment), NEWLINE);
    }

    private void addNewLine() {
        addToken(new Token(NEW_LINE));
    }

    private void addToken(Token token) {
        addToken(token, NOTHING);
    }

    private void addToken(Token token, TokenModifier suffix) {
        addToken(NOTHING, token, suffix);
    }

    private void addToken(TokenModifier prefix, Token token, TokenModifier suffix) {
        handleTokenModifier(prefix);
        apiListing.getTokens().add(token);
        handleTokenModifier(suffix);
    }

    private void handleTokenModifier(TokenModifier modifier) {
        switch (modifier) {
            case INDENT:
                addToken(makeWhitespace());
                break;
            case SPACE:
                addToken(new Token(WHITESPACE, " "));
                break;
            case NEWLINE:
                addNewLine();
                break;
            case NOTHING:
                break;
        }
    }
}
