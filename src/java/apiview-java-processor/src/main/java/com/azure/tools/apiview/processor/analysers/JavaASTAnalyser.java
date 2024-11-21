package com.azure.tools.apiview.processor.analysers;

import com.azure.tools.apiview.processor.analysers.models.*;
import com.azure.tools.apiview.processor.analysers.util.APIListingValidator;
import com.azure.tools.apiview.processor.analysers.util.MiscUtils;
import com.azure.tools.apiview.processor.diagnostics.Diagnostics;
import com.azure.tools.apiview.processor.model.*;
import com.azure.tools.apiview.processor.model.maven.*;
import com.azure.tools.apiview.processor.model.traits.Parent;
import com.github.javaparser.JavaParser;
import com.github.javaparser.JavaParserAdapter;
import com.github.javaparser.ParserConfiguration;
import com.github.javaparser.ast.*;
import com.github.javaparser.ast.body.*;
import com.github.javaparser.ast.comments.*;
import com.github.javaparser.ast.expr.*;
import com.github.javaparser.ast.modules.*;
import com.github.javaparser.ast.nodeTypes.*;
import com.github.javaparser.ast.stmt.Statement;
import com.github.javaparser.ast.type.*;
import org.unbescape.html.HtmlEscape;

import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.*;
import java.util.concurrent.atomic.AtomicInteger;
import java.util.function.BiConsumer;
import java.util.function.Consumer;
import java.util.function.Function;
import java.util.regex.Matcher;
import java.util.stream.Collector;
import java.util.stream.Collectors;
import java.util.stream.Stream;

import static com.azure.tools.apiview.processor.analysers.util.ASTUtils.*;
import static com.azure.tools.apiview.processor.analysers.util.MiscUtils.upperCase;
import static com.azure.tools.apiview.processor.model.TokenKind.*;
import static com.azure.tools.apiview.processor.analysers.models.Constants.*;

public class JavaASTAnalyser implements Analyser {

    /************************************************************************************************
     *
     * Constants
     *
     **********************************************************************************************/

    public static final String MAVEN_KEY = "Maven";
    public static final String MODULE_INFO_KEY = "module-info";

    private static final Map<String, AnnotationRule> ANNOTATION_RULE_MAP;
    private static final JavaParserAdapter JAVA_8_PARSER;
    private static final JavaParserAdapter JAVA_11_PARSER;
    static {
        /*
         For some annotations, we want to customise how they are displayed. Sometimes, we don't show them in any
         circumstance. Other times, we want to show them but not their attributes. This map is used to define these
         customisations. These rules override the default output that APIView will do, based on the location
         annotation in the code.
         */
        ANNOTATION_RULE_MAP = new HashMap<>();
        ANNOTATION_RULE_MAP.put(ANNOTATION_SERVICE_METHOD, new AnnotationRule().setHidden(true));
        ANNOTATION_RULE_MAP.put(ANNOTATION_SUPPRESS_WARNINGS, new AnnotationRule().setHidden(true));

        // Retention and Target annotations are always useful to see fully-expanded
        ANNOTATION_RULE_MAP.put(ANNOTATION_RETENTION, new AnnotationRule().setShowOnNewline(true).setShowProperties(true));
        ANNOTATION_RULE_MAP.put(ANNOTATION_TARGET, new AnnotationRule().setShowOnNewline(true).setShowProperties(true));

        // we always want @Metadata annotations to be fully expanded, but in a condensed form
        ANNOTATION_RULE_MAP.put(ANNOTATION_METADATA, new AnnotationRule().setShowProperties(true).setCondensed(true));

        // Configure JavaParser to use type resolution
        JAVA_8_PARSER = JavaParserAdapter.of(new JavaParser(new ParserConfiguration()
            .setLanguageLevel(ParserConfiguration.LanguageLevel.JAVA_8)
            .setDetectOriginalLineSeparator(false)));

        JAVA_11_PARSER = JavaParserAdapter.of(new JavaParser(new ParserConfiguration()
            .setLanguageLevel(ParserConfiguration.LanguageLevel.JAVA_11)
            .setDetectOriginalLineSeparator(false)));
    }


    /************************************************************************************************
     *
     * Fields
     *
     **********************************************************************************************/

    // This is the model that we build up as the AST of all files are analysed. The APIListing is then output as
    // JSON that can be understood by APIView.
    private final APIListing apiListing;
    private final Diagnostics diagnostic;

    private final Map<String,String> shortClassNameToFullyQualfiedNameMap = new HashMap<>();



    /************************************************************************************************
     *
     * Constructors
     *
     **********************************************************************************************/

    public JavaASTAnalyser(APIListing apiListing) {
        this.apiListing = apiListing;
        this.diagnostic = new Diagnostics(apiListing);
    }



    /************************************************************************************************
     *
     * Analysis Methods - Public API
     *
     **********************************************************************************************/

    @Override
    public void analyse(List<Path> allFiles) {
        /*
         * Begin by filtering out file paths that we don't care about.
         * Then build a map of all known types and package names and a map of package names to navigation items.
         * Finally, tokenize each file.
         */
        // a set of all elements discovered before we begin processing them
        Set<ScanElement> scanElements = allFiles.stream()
                .filter(this::filterFilePaths)
                .map(this::scanForTypes)
                .filter(Optional::isPresent)
                .map(Optional::get)
                .collect(Collectors.toSet());

        // put all classes into the short class name -> fully qualified class name map
        scanElements.stream()
            .filter(scanElement -> scanElement.elementType == ScanElementType.CLASS)
            .map(scanElement -> (ScanClass) scanElement)
            .forEach(scanClass -> shortClassNameToFullyQualfiedNameMap.put(scanClass.primaryTypeName, scanClass.fullyQualifiedName));

        // and then group all classes into groups based on their package name, and process each package
        scanElements.stream()
            .collect(Collectors.groupingBy(e -> e.packageName, TreeMap::new, Collectors.toList()))
            .forEach(this::processPackage);

        // we conclude by doing a final pass over all diagnostics to enable them to do any final analysis based on
        // the already-executed individual scans
        diagnostic.scanFinal(apiListing);

        // validate the model
        APIListingValidator.validate(apiListing);
    }



    /************************************************************************************************
     *
     * Inner Classes (to aid in analysis)
     *
     **********************************************************************************************/

    private enum ScanElementType {
        MODULE_INFO,
        CLASS,
        PACKAGE,
        POM;
    }

    private static class ScanElement implements Comparable<ScanElement> {
        final CompilationUnit compilationUnit;
        final Path path;
        final ScanElementType elementType;
        final String packageName;

        public ScanElement(Path path, CompilationUnit compilationUnit, ScanElementType elementType) {
            this.path = path;
            this.compilationUnit = compilationUnit;
            this.elementType = elementType;
            this.packageName = compilationUnit != null ?
                compilationUnit.getPackageDeclaration().map(PackageDeclaration::getNameAsString).orElse("") : "";
        }

        @Override
        public int compareTo(final ScanElement o) {
            return packageName.compareTo(o.packageName);
        }
    }

    // This class represents a class that is going to go through the analysis pipeline, and it collects
    // together all useful properties that were identified so that they can form part of the analysis.
    private static class ScanClass extends ScanElement {
        final String primaryTypeName;
        final String fullyQualifiedName;

        public ScanClass(Path path, CompilationUnit compilationUnit) {
            super(path, compilationUnit, ScanElementType.CLASS);
            this.primaryTypeName = compilationUnit != null ? compilationUnit.getPrimaryTypeName().orElse("") : "";
            this.fullyQualifiedName = getNodeFullyQualifiedName(compilationUnit);
        }
    }



    /************************************************************************************************
     *
     * Implementation - Utility Methods
     *
     **********************************************************************************************/

    private boolean filterFilePaths(Path filePath) {
        String fileName = filePath.toString();
        // Skip paths that are directories, in implementation, or are not pom.xml files contained within META-INF
        if (Files.isDirectory(filePath)
                || fileName.contains("implementation")
                || (!fileName.endsWith("pom.xml") && fileName.contains("META-INF"))) {
            return false;
        } else {
            // Only include Java files.
            return fileName.endsWith(".java") || fileName.endsWith("pom.xml");
        }
    }

    private Optional<ScanElement> scanForTypes(Path path) {
        if (path.toString().endsWith("pom.xml")) {
            return Optional.of(new ScanElement(path, null, ScanElementType.POM));
        }
        try {
            boolean isModuleInfo = path.endsWith("module-info.java");

            CompilationUnit compilationUnit = isModuleInfo
                ? JAVA_11_PARSER.parse(Files.newBufferedReader(path))
                : JAVA_8_PARSER.parse(Files.newBufferedReader(path));

            compilationUnit.setStorage(path, StandardCharsets.UTF_8);

            // we build up a map between types and the packages they are in, for use in our diagnostic rules
            compilationUnit.getImports().stream()
                    .map(ImportDeclaration::getName)
                    .forEach(name -> name.getQualifier().ifPresent(packageName ->
                            apiListing.addPackageTypeMapping(packageName.toString(), name.getIdentifier())));

            if (isModuleInfo) {
                return Optional.of(new ScanElement(path, compilationUnit, ScanElementType.MODULE_INFO));
            } else if (path.endsWith("package-info.java")) {
                return Optional.of(new ScanElement(path, compilationUnit, ScanElementType.PACKAGE));
            } else {
                return Optional.of(new ScanClass(path, compilationUnit));
            }
        } catch (IOException e) {
            e.printStackTrace();
            return Optional.empty();
        }
    }

    private void processPackage(String packageName, List<ScanElement> scanElements) {
        if (packageName.isEmpty()) {
            // we are dealing with the root package, so we are looking at the maven pom and module-info.

            // look for the maven pom.xml file, and put that in first, in the root package
            tokeniseMavenPom(apiListing.getMavenPom());

            // look for the module-info.java file, and put that second, after the maven pom
            scanElements.stream()
                    .filter(scanElement -> scanElement.elementType == ScanElementType.MODULE_INFO)
                    .findFirst()
                    .flatMap(scanElement -> scanElement.compilationUnit.getModule()).ifPresent(this::visitModuleDeclaration);
        } else {
            final String lineId = makeId("package-" + packageName);

            // lets see if we have javadoc for this packageName
            scanElements.stream()
                .filter(scanElement -> scanElement.elementType == ScanElementType.PACKAGE)
                .findFirst()
                .flatMap(scanElement -> scanElement.compilationUnit.getPackageDeclaration().flatMap(Node::getComment))
                .filter(Comment::isJavadocComment)
                .ifPresent(javadocComment -> visitJavaDoc((JavadocComment) javadocComment, apiListing, lineId));

            final ReviewLine packageLine = apiListing.addChildLine(lineId);

            packageLine
                .addToken(KEYWORD, "package")
                .addToken(new ReviewToken(PACKAGE_NAME, packageName)
                    .setNavigationDisplayName(packageName))
                .addContextStartTokens();

            // then do all classes in the package
            scanElements.stream()
                .filter(scanElement -> scanElement.elementType == ScanElementType.CLASS)
                .map(scanElement -> (ScanClass) scanElement)
                .sorted(Comparator.comparing(e -> e.primaryTypeName))
                .forEach(scanClass -> visitCompilationUnit(scanClass.compilationUnit, packageLine));

            packageLine.addContextEndTokens();
        }
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

    private void possiblyDeprecate(ReviewToken token, NodeWithAnnotations<?> n) {
        if (n.isAnnotationPresent(ANNOTATION_DEPRECATED)) {
            token.setDeprecated();
        }
    }

    private void possiblyAddNavigationLink(ReviewToken token, Node node) {
        if (node instanceof ClassOrInterfaceType) {
            ClassOrInterfaceType classOrInterfaceType = (ClassOrInterfaceType) node;
            String shortName = classOrInterfaceType.getNameAsString();

            // Check if the short name exists in the map
            if (shortClassNameToFullyQualfiedNameMap.containsKey(shortName)) {
                // If it does, get the fully qualified name from the map. This is also the ID we should have used
                // when we defined the token that we want to link to
                String fullyQualifiedName = shortClassNameToFullyQualfiedNameMap.get(shortName);
//                token.addProperty(PROPERTY_NAVIGATE_TO_ID, fullyQualifiedName);
                token.setNavigateToId(fullyQualifiedName);
            }
        }
    }



    /************************************************************************************************
     *
     * Analysis implementation - Maven POM support
     *
     **********************************************************************************************/

    private void tokeniseMavenPom(Pom mavenPom) {
        ReviewLine mavenLine = apiListing.addChildLine();

        mavenLine
            .addToken(new ReviewToken(TokenKind.MAVEN, "maven")
                .setNavigationDisplayName(MAVEN_KEY)
                .setSkipDiff())
            .addContextStartTokens();

        // parent
        String gavStr = mavenPom.getParent().getGroupId() + ":" + mavenPom.getParent().getArtifactId() + ":"
                + mavenPom.getParent().getVersion();
        MiscUtils.tokeniseMavenKeyValue(mavenLine, "parent", gavStr);

        // properties
        gavStr = mavenPom.getGroupId() + ":" + mavenPom.getArtifactId() + ":" + mavenPom.getVersion();
        MiscUtils.tokeniseMavenKeyValue(mavenLine, "properties", gavStr);

        // configuration
        boolean showJacoco = mavenPom.getJacocoMinLineCoverage() != null
                && mavenPom.getJacocoMinBranchCoverage() != null;
        boolean showCheckStyle = mavenPom.getCheckstyleExcludes() != null && !mavenPom.getCheckstyleExcludes()
                .isEmpty();

        if (showJacoco || showCheckStyle) {
            ReviewLine configurationLine = mavenLine.addChildLine()
                .addToken(TokenKind.MAVEN_KEY, "configuration")
                .addContextStartTokens();

            if (showCheckStyle) {
                MiscUtils.tokeniseMavenKeyValue(configurationLine, "checkstyle-excludes", mavenPom.getCheckstyleExcludes());
            }
            if (showJacoco) {
                ReviewLine jacocoLine = configurationLine.addChildLine()
                    .addToken(TokenKind.MAVEN_KEY, "jacoco")
                    .addContextStartTokens();

                MiscUtils.tokeniseMavenKeyValue(jacocoLine, "min-line-coverage", mavenPom.getJacocoMinLineCoverage());
                MiscUtils.tokeniseMavenKeyValue(jacocoLine, "min-branch-coverage", mavenPom.getJacocoMinBranchCoverage());
                jacocoLine.addContextEndTokens();
            }

            configurationLine.addContextEndTokens();
        }

        // Maven name
        MiscUtils.tokeniseMavenKeyValue(mavenLine, "name", mavenPom.getName())
                .addProperty(PROPERTY_MAVEN_NAME, mavenPom.getName());

        // Maven description
        MiscUtils.tokeniseMavenKeyValue(mavenLine, "description", mavenPom.getDescription())
                .addProperty(PROPERTY_MAVEN_DESCRIPTION, mavenPom.getDescription());

        // dependencies
        ReviewLine dependenciesLine = mavenLine.addChildLine()
            .addToken(TokenKind.MAVEN_KEY, "dependencies")
            .addContextStartTokens();

        mavenPom.getDependencies()
                .stream()
                .collect(Collectors.groupingBy(Dependency::getScope))
                .forEach((k, v) -> {
                    if ("test".equals(k)) {
                        // we don't care to present test scope dependencies
                        return;
                    }
                    String scope = k.isEmpty() ? "compile" : k;

                    dependenciesLine.addChildLine()
                            .addToken(TokenKind.COMMENT, "// " + scope + " scope");

                    for (Dependency d : v) {
                        String gav = d.getGroupId() + ":" + d.getArtifactId() + ":" + d.getVersion();
                        dependenciesLine.addChildLine("maven-lineid-" + gav)
                                .addToken(TokenKind.MAVEN_DEPENDENCY, gav);
                    }
                });
        dependenciesLine.addContextEndTokens();

        mavenLine.addContextEndTokens();
    }



    /************************************************************************************************
     *
     * Analysis implementation - Module declaration support
     *
     **********************************************************************************************/

    private void visitModuleDeclaration(ModuleDeclaration moduleDeclaration) {
//        moduleNode.addProperty(PROPERTY_MODULE_NAME, moduleDeclaration.getNameAsString());
        ReviewLine moduleLine = apiListing.addChildLine(MODULE_INFO_KEY)
            .addToken(KEYWORD, "module")
            .addToken(new ReviewToken(MODULE_NAME, moduleDeclaration.getNameAsString())
                    .setNavigationDisplayName("module-info"))
            .addProperty(MODULE_INFO_KEY, "true")
            .addProperty(PROPERTY_MODULE_NAME, moduleDeclaration.getNameAsString())
            .addContextStartTokens();

        // Sometimes an exports or opens statement is conditional, so we need to handle that case
        // in a single location here, to remove duplication.
        BiConsumer<ReviewLine, NodeList<Name>> conditionalExportsToOrOpensToConsumer = (reviewLine, names) -> {
            if (!names.isEmpty()) {
                reviewLine.addToken(KEYWORD, "to", Spacing.SPACE_BEFORE_AND_AFTER);
                commaSeparateList(reviewLine, TYPE_NAME, names, Name::asString);
            }
        };

        moduleDeclaration.getDirectives().forEach(moduleDirective -> {
            moduleDirective.ifModuleRequiresStmt(d -> {
                String id = makeId(MODULE_INFO_KEY + "-requires-" + d.getNameAsString());
                ReviewLine moduleChildLine = moduleLine.addChildLine(id)
                    .addToken(KEYWORD, "requires");

                if (d.hasModifier(Modifier.Keyword.STATIC)) {
                    moduleChildLine.addToken(KEYWORD, "static");
                }

                if (d.isTransitive()) {
                    moduleChildLine.addToken(KEYWORD, "transitive");
                }

                // adding property just to make diagnostics easier
                moduleChildLine.addProperty(PROPERTY_MODULE_REQUIRES, d.getNameAsString());
                moduleChildLine.addProperty("static", d.hasModifier(Modifier.Keyword.STATIC) ? "true" : "false");
                moduleChildLine.addProperty("transitive", d.isTransitive() ? "true" : "false");

                moduleChildLine.addToken(MODULE_REFERENCE, d.getNameAsString(), Spacing.NO_SPACE);
                moduleChildLine.addToken(PUNCTUATION, ";", Spacing.NO_SPACE);
            });

            moduleDirective.ifModuleExportsStmt(d -> {
                String id = makeId(MODULE_INFO_KEY + "-exports-" + d.getNameAsString());
                ReviewLine moduleChildLine = moduleLine.addChildLine(id)
                    .addToken(KEYWORD, "exports")
                    .addToken(MODULE_REFERENCE, d.getNameAsString(), Spacing.NO_SPACE);

                // adding property just to make diagnostics easier
                moduleChildLine.addProperty(PROPERTY_MODULE_EXPORTS, d.getNameAsString());

                conditionalExportsToOrOpensToConsumer.accept(moduleChildLine, d.getModuleNames());
                moduleChildLine.addToken(PUNCTUATION, ";", Spacing.NO_SPACE);
            });

            moduleDirective.ifModuleOpensStmt(d -> {
                String id = makeId(MODULE_INFO_KEY + "-opens-" + d.getNameAsString());
                ReviewLine moduleChildLine = moduleLine.addChildLine(id)
                    .addToken(KEYWORD, "opens")
                    .addToken(MODULE_REFERENCE, d.getNameAsString(), Spacing.NO_SPACE);

                // adding property just to make diagnostics easier
                moduleChildLine.addProperty(PROPERTY_MODULE_OPENS, d.getNameAsString());

                conditionalExportsToOrOpensToConsumer.accept(moduleChildLine, d.getModuleNames());
                moduleChildLine.addToken(PUNCTUATION, ";", Spacing.NO_SPACE);
            });

            moduleDirective.ifModuleUsesStmt(d -> {
                String id = makeId(MODULE_INFO_KEY + "-uses-" + d.getNameAsString());
                moduleLine.addChildLine(id)
                    .addToken(KEYWORD, "uses")
                    .addToken(MODULE_REFERENCE, d.getNameAsString(), Spacing.NO_SPACE)
                    .addToken(PUNCTUATION, ";", Spacing.NO_SPACE);
            });

            moduleDirective.ifModuleProvidesStmt(d -> {
                String id = makeId(MODULE_INFO_KEY + "-provides-" + d.getNameAsString());
                ReviewLine moduleChildLine = moduleLine.addChildLine(id)
                    .addToken(KEYWORD, "provides")
                    .addToken(MODULE_REFERENCE, d.getNameAsString())
                    .addToken(KEYWORD, "with");

                commaSeparateList(moduleChildLine, MODULE_REFERENCE, d.getWith(), Object::toString);
                moduleChildLine.addToken(PUNCTUATION, ";", Spacing.NO_SPACE);
            });
        });

        moduleLine.addContextEndTokens();
    }

    private <T> void commaSeparateList(ReviewLine reviewLine,
                                   TokenKind kind,
                                   Iterable<T> it,
                                   Function<T, String> func) {
        Iterator<T> iterator = it.iterator();
        if (iterator.hasNext()) {
            reviewLine.addToken(kind, func.apply(iterator.next()), Spacing.NO_SPACE);
            while (iterator.hasNext()) {
                reviewLine
                    .addToken(PUNCTUATION, ",")
                    .addToken(kind, func.apply(iterator.next()), Spacing.NO_SPACE);
            }
        }
    }

    /************************************************************************************************
     *
     * Analysis implementation - Standard Java support
     *
     **********************************************************************************************/

    private void visitCompilationUnit(CompilationUnit compilationUnit, ReviewLine parentNode) {
        NodeList<TypeDeclaration<?>> types = compilationUnit.getTypes();
        for (final TypeDeclaration<?> typeDeclaration : types) {
            visitDefinition(typeDeclaration, parentNode);
        }

        diagnostic.scanIndividual(compilationUnit, apiListing);
    }

    // This method is for constructors, fields, methods, etc.
    private void visitDefinition(BodyDeclaration<?> definition, ReviewLine parentLine) {
        boolean isTypeDeclaration = false;
        String id;
        String name;

        if (definition instanceof TypeDeclaration<?>) {
            TypeDeclaration<?> typeDeclaration = (TypeDeclaration<?>) definition;
            // Skip if the class is private or package-private, unless it is a nested type defined inside a public interface
            if (!isTypeAPublicAPI(typeDeclaration)) {
                return;
            }

            // FIXME getKnownTypes is...icky
            id = makeId(typeDeclaration);
            name = typeDeclaration.getNameAsString();
            apiListing.getKnownTypes().put(typeDeclaration.getFullyQualifiedName().orElse(""), id);
            isTypeDeclaration = true;
        } else if (definition instanceof FieldDeclaration) {
            FieldDeclaration fieldDeclaration = (FieldDeclaration) definition;
            id = makeId(fieldDeclaration);
            name = fieldDeclaration.toString();
        } else if (definition instanceof CallableDeclaration<?>) {
            CallableDeclaration<?> callableDeclaration = (CallableDeclaration<?>) definition;
            id = makeId(callableDeclaration);
            name = callableDeclaration.getNameAsString();
        } else {
            System.out.println("Unknown definition type: " + definition.getClass().getName());
            System.exit(-1);
            return;
        }

        // when we are dealing with a type declaration, annotations go on the line *before* the definition,
        // as opposed to fields and methods where they go on the same line
        final boolean showAnnotationsOnNewLine = isTypeDeclaration;

        final ReviewLine definitionLine;

        if (isTypeDeclaration) {
            visitAnnotations(definition, isTypeDeclaration, showAnnotationsOnNewLine, parentLine, id);
            visitJavaDoc(definition, parentLine, id);
            definitionLine = parentLine.addChildLine(new ReviewLine(parentLine, id));
        } else {
            visitJavaDoc(definition, parentLine, id);
            definitionLine = parentLine.addChildLine(new ReviewLine(parentLine, id));
            visitAnnotations(definition, isTypeDeclaration, showAnnotationsOnNewLine, definitionLine, definitionLine);
        }

        // Add modifiers - public, protected, static, final, etc
        for (final Modifier modifier : ((NodeWithModifiers<?>)definition).getModifiers()) {
            definitionLine.addToken(new ReviewToken(KEYWORD, modifier.getKeyword().asString()));
        }

        // for type declarations, add in if it is a class, annotation, enum, interface, etc
        if (definition instanceof TypeDeclaration<?>) {
            TypeDeclaration<?> typeDeclaration = (TypeDeclaration<?>) definition;
            TokenKind kind = getTokenKind(typeDeclaration);
            definitionLine.addToken(new ReviewToken(KEYWORD, kind.getTypeDeclarationString()));

            // Note that it is not necessary to specify the ID here, as it is already specified on the TreeNode
            ReviewToken typeNameToken = new ReviewToken(kind, name)
                .setSpacing(Spacing.NO_SPACE); // no space here - we have to see if there is any type parameters below first

            typeNameToken.setNavigationDisplayName(name);

            possiblyAddNavigationLink(typeNameToken, typeDeclaration);
            possiblyDeprecate(typeNameToken, typeDeclaration);

            checkForCrossLanguageDefinitionId(definitionLine, typeDeclaration);
            definitionLine.addToken(typeNameToken);
        }

        boolean addedSpace = false;

        // Add type parameters for definition
        if (definition instanceof NodeWithTypeParameters<?>) {
            NodeWithTypeParameters<?> d = (NodeWithTypeParameters<?>) definition;
            spacingState = SpacingState.SKIP_NEXT_SUFFIX;
            boolean modified = visitTypeParameters(d.getTypeParameters(), definitionLine);
            spacingState = SpacingState.DEFAULT;

            if (modified) {
                // add the space we skipped earlier, due to the generics
                definitionLine.addSpace();
                addedSpace = true;
            }
        }

        if (!addedSpace && definition instanceof TypeDeclaration<?>) {
            // add the space we skipped earlier, due to the generics
            definitionLine.addSpace();
        }

        // Add type for definition - this is the return type for methods
        visitType(definition, definitionLine, RETURN_TYPE);

        if (definition instanceof FieldDeclaration) {
            // For Fields - we add the field type and name
            visitDeclarationNameAndVariables((FieldDeclaration) definition, definitionLine);
            definitionLine.addToken(new ReviewToken(PUNCTUATION, ";"));
        } else if (definition instanceof CallableDeclaration<?>) {
            // For Methods - Add name and parameters for definition
            CallableDeclaration<?> n = (CallableDeclaration<?>) definition;
            visitDeclarationNameAndParameters(n, n.getParameters(), definitionLine);

            // Add throw exceptions for definition
            visitThrowException(n, definitionLine);
        } else if (definition instanceof TypeDeclaration<?>) {
            TypeDeclaration<?> d = (TypeDeclaration<?>) definition;

            // add in types that we are extending or implementing
            visitExtendsAndImplements((TypeDeclaration<?>) definition, definitionLine);

            definitionLine.addContextStartTokens();

            // now process inner values (e.g. if it is a class, interface, enum, etc
            if (d.isEnumDeclaration()) {
                visitEnumEntries((EnumDeclaration) d, definitionLine);
            }

            // Get if the declaration is interface or not
            boolean isInterfaceDeclaration = isInterfaceType(d);

            // public custom annotation @interface's members
            if (d.isAnnotationDeclaration() && isPublicOrProtected(d.getAccessSpecifier())) {
                final AnnotationDeclaration annotationDeclaration = (AnnotationDeclaration) d;
                visitAnnotationMember(annotationDeclaration, definitionLine);
            }

            // get fields
            visitFields(isInterfaceDeclaration, d, definitionLine);

            // get Constructors
            final List<ConstructorDeclaration> constructors = d.getConstructors();
            if (constructors.isEmpty()) {
                // add default constructor if there is no constructor at all, except interface and enum
                if (!isInterfaceDeclaration && !d.isEnumDeclaration() && !d.isAnnotationDeclaration()) {
                    addDefaultConstructor(d, definitionLine);
                } else {
                    // skip and do nothing if there is no constructor in the interface.
                }
            } else {
                visitConstructorsOrMethods(d, isInterfaceDeclaration, true, constructors, definitionLine);
            }

            // get Methods
            visitConstructorsOrMethods(d, isInterfaceDeclaration, false, d.getMethods(), definitionLine);

            // get Inner classes
            d.getChildNodes()
                .stream()
                .filter(n -> n instanceof TypeDeclaration)
                .map(n -> (TypeDeclaration<?>) n)
                .forEach(innerType -> {
                    if (innerType.isEnumDeclaration() || innerType.isClassOrInterfaceDeclaration()) {
                        visitDefinition(innerType, definitionLine);
                    }
                });

            if (isInterfaceDeclaration) {
                if (d.getMembers().isEmpty()) {
                    // we have an empty interface declaration, it is probably a marker interface and we will leave a
                    // comment to that effect
                    definitionLine.addChildLine().addToken(COMMENT, "// This interface does not declare any API.");
                }
            }
            definitionLine.addContextEndTokens();
        }
    }

    private void visitEnumEntries(EnumDeclaration enumDeclaration, ReviewLine parentLine) {
        final NodeList<EnumConstantDeclaration> enumConstantDeclarations = enumDeclaration.getEntries();
        int size = enumConstantDeclarations.size();

        AtomicInteger counter = new AtomicInteger();

        enumConstantDeclarations.forEach(enumConstantDeclaration -> {
            final String id = makeId(enumConstantDeclaration);
            ReviewLine enumConstantLine = parentLine.addChildLine(id);

            visitJavaDoc(enumConstantDeclaration, enumConstantLine, id);

            // annotations
            visitAnnotations(enumConstantDeclaration, false, false, enumConstantLine, enumConstantLine);

            // create a unique id for enum constants by using the fully-qualified constant name
            // (package, enum name, and enum constant name)
            final String name = enumConstantDeclaration.getNameAsString();

            // Note we do not place the id here, rather it goes on the treeNode
            ReviewToken enumToken = new ReviewToken(ENUM_CONSTANT, name).setSpacing(Spacing.NO_SPACE);
            enumConstantLine.addToken(enumToken);
            possiblyDeprecate(enumToken, enumConstantDeclaration);

            // add tokens for comma-separated list of arguments
            int argumentsSize = enumConstantDeclaration.getArguments().size();
            if (argumentsSize > 0) {
                enumConstantLine.addToken(PUNCTUATION, "(", Spacing.NO_SPACE);
                for (int i = 0, max = enumConstantDeclaration.getArguments().size(); i < max; i++) {
                    visitExpression(enumConstantDeclaration.getArguments().get(i), enumConstantLine);
                    if (i < max - 1) {
                        enumConstantLine.addToken(PUNCTUATION, ",");
                    }
                }
                enumConstantLine.addToken(PUNCTUATION, ")", Spacing.NO_SPACE);
            }

            if (counter.getAndIncrement() < size - 1) {
                enumConstantLine.addToken(PUNCTUATION, ",");
            } else {
                enumConstantLine.addToken(PUNCTUATION, ";");
            }
        });
    }

    /*
     * This method is used to add 'cross language definition id' to the token if it is defined in the
     * apiview_properties.json file. This is used most commonly in conjunction with TypeSpec-generated libraries,
     * so that we may review cross languages with some level of confidence that the types and methods are the same.
     */
    private void checkForCrossLanguageDefinitionId(ReviewLine reviewLine, Node node) {
        final String fqn = getNodeFullyQualifiedName(node);
        apiListing.getApiViewProperties().getCrossLanguageDefinitionId(fqn).ifPresent(reviewLine::setCrossLanguageId);
    }

    private void visitAnnotationMember(AnnotationDeclaration annotationDeclaration, final ReviewLine parentLine) {
        // Member methods in the annotation declaration
        NodeList<BodyDeclaration<?>> annotationDeclarationMembers = annotationDeclaration.getMembers();
        if (annotationDeclarationMembers.isEmpty()) {
            parentLine.addChildLine().addToken(COMMENT, "// This annotation does not declare any members.");
            return;
        }

        for (BodyDeclaration<?> bodyDeclaration : annotationDeclarationMembers) {
            Optional<AnnotationMemberDeclaration> annotationMemberDeclarationOptional = bodyDeclaration.toAnnotationMemberDeclaration();
            if (!annotationMemberDeclarationOptional.isPresent()) {
                continue;
            }
            final AnnotationMemberDeclaration annotationMemberDeclaration = annotationMemberDeclarationOptional.get();

            final String id = makeId(annotationMemberDeclaration);
            ReviewLine annotationMemberLine = parentLine.addChildLine(id);

            visitClassType(annotationMemberDeclaration.getType(), annotationMemberLine, RETURN_TYPE);

            annotationMemberLine.addToken(METHOD_NAME, annotationMemberDeclaration.getNameAsString(), Spacing.NO_SPACE);
            annotationMemberLine.addToken(PUNCTUATION, "()", Spacing.NO_SPACE);

            // default value
            final Optional<Expression> defaultValueOptional = annotationMemberDeclaration.getDefaultValue();
            if (defaultValueOptional.isPresent()) {
                annotationMemberLine.addToken(KEYWORD, "default", Spacing.SPACE_BEFORE_AND_AFTER);

                final Expression defaultValueExpr = defaultValueOptional.get();
                visitExpression(defaultValueExpr, annotationMemberLine);
            }
        }
    }

    private void visitFields(final boolean isInterfaceDeclaration,
                             final TypeDeclaration<?> typeDeclaration,
                             final ReviewLine parentLine) {
        final List<? extends FieldDeclaration> fieldDeclarations = typeDeclaration.getFields();
        if (fieldDeclarations.isEmpty()) {
            return;
        }

        for (FieldDeclaration fieldDeclaration : fieldDeclarations) {
            // By default , interface has public abstract methods if there is no access specifier declared
            if (isInterfaceDeclaration) {
                // no-op - we take all methods in the method
            } else if (isPrivateOrPackagePrivate(fieldDeclaration.getAccessSpecifier())) {
                // Skip if not public API
                continue;
            }

            visitDefinition(fieldDeclaration, parentLine);
        }
    }

    private void visitConstructorsOrMethods(final TypeDeclaration<?> typeDeclaration,
                                            final boolean isInterfaceDeclaration,
                                            final boolean isConstructor,
                                            final List<? extends CallableDeclaration<?>> callableDeclarations,
                                            final ReviewLine parentLine) {
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
                    return;
                }

                parentLine.addChildLine()
                    .addToken(COMMENT, "// This class does not have any public constructors, and is not able to be instantiated using 'new'.");
                return;
            }
        }

        // if the class we are looking at is annotated with @ServiceClient, we will break up the methods that are
        // displayed into service methods and non-service methods
        final boolean showGroupings = !isConstructor && typeDeclaration.isAnnotationPresent("ServiceClient");
        Collector<CallableDeclaration<?>, ?, Map<String, List<CallableDeclaration<?>>>> collector = Collectors.groupingBy((CallableDeclaration<?> cd) ->
                showGroupings ? cd.isAnnotationPresent("ServiceMethod") ? "Service Methods" : "Non-Service Methods" : "");

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
                    // we group inside the APIView each of the groups, so that we can visualise their operations more clearly
                    parentLine.addChildLine()
                        .addToken(COMMENT, "// " + groupName + ":");
                }
                group.forEach(callableDeclaration -> visitDefinition(callableDeclaration, parentLine));
            });
    }

    private void visitExtendsAndImplements(TypeDeclaration<?> typeDeclaration, ReviewLine definitionLine) {
        BiConsumer<NodeList<ClassOrInterfaceType>, TokenKind> c = (nodeList, kind) -> {
            for (int i = 0, max = nodeList.size(); i < max; i++) {
                final ClassOrInterfaceType node = nodeList.get(i);
                visitType(node, definitionLine, kind);

                if (i < max - 1) {
                    definitionLine.addToken(PUNCTUATION, ",");
                }
            }
        };

        // Extends a class
        if (typeDeclaration instanceof NodeWithExtends<?>) {
            final NodeWithExtends<?> d = (NodeWithExtends<?>) typeDeclaration;
            if (d.getExtendedTypes().isNonEmpty()) {
                definitionLine.addToken(KEYWORD, "extends");
                c.accept(d.getExtendedTypes(), EXTENDS_TYPE);
            }
        }

        // implements a class
        if (typeDeclaration instanceof NodeWithImplements<?>) {
            final NodeWithImplements<?> d = (NodeWithImplements<?>) typeDeclaration;
            if (d.getImplementedTypes().isNonEmpty()) {
                definitionLine.addToken(KEYWORD, "implements");
                c.accept(d.getImplementedTypes(), IMPLEMENTS_TYPE);
            }
        }
    }

    private void visitDeclarationNameAndVariables(FieldDeclaration fieldDeclaration, ReviewLine definitionLine) {
        final NodeList<VariableDeclarator> variables = fieldDeclaration.getVariables();
        if (variables.size() > 1) {
            for (int i = 0; i < variables.size(); i++) {
                final VariableDeclarator variableDeclarator = variables.get(i);
                ReviewToken token = new ReviewToken(FIELD_NAME, variableDeclarator.getNameAsString());
                definitionLine.addToken(token);
                possiblyDeprecate(token, fieldDeclaration);

                if (i < variables.size() - 1) {
                    definitionLine.addToken(PUNCTUATION, ",");
                }
            }
        } else if (variables.size() == 1) {
            final VariableDeclarator variableDeclarator = variables.get(0);
            ReviewToken token = new ReviewToken(FIELD_NAME, variableDeclarator.getNameAsString());
            definitionLine.addToken(token);
            possiblyDeprecate(token, fieldDeclaration);

            final Optional<Expression> variableDeclaratorOption = variableDeclarator.getInitializer();
            if (variableDeclaratorOption.isPresent()) {
                Expression e = variableDeclaratorOption.get();

                if (e.isObjectCreationExpr() && e.asObjectCreationExpr().getAnonymousClassBody().isPresent()) {
                    // no-op because we don't want to include all of the anonymous inner class details
                } else {
                    // we want to make string constants look like strings, so we need to look for them within the
                    // variable declarator
                    definitionLine.addToken(PUNCTUATION, "=");
                    visitExpression(e, definitionLine);
                }
            }
        }
    }

    private void visitExpression(Expression expression, ReviewLine definitionLine) {
        visitExpression(expression, definitionLine, false);
    }

    private void visitExpression(Expression expression, ReviewLine definitionLine, boolean condensed) {
        if (expression instanceof MethodCallExpr) {
            MethodCallExpr methodCallExpr = (MethodCallExpr) expression;
            definitionLine.addToken(METHOD_NAME, methodCallExpr.getNameAsString(), Spacing.NO_SPACE);
            definitionLine.addToken(PUNCTUATION, "(", Spacing.NO_SPACE);
            NodeList<Expression> arguments = methodCallExpr.getArguments();
            for (int i = 0; i < arguments.size(); i++) {
                Expression argument = arguments.get(i);
                if (argument instanceof StringLiteralExpr) {
                    definitionLine.addToken(STRING_LITERAL, argument.toString(), Spacing.NO_SPACE);
                } else {
                    definitionLine.addToken(TEXT, argument.toString(), Spacing.NO_SPACE);
                }
                if (i < arguments.size() - 1) {
                    definitionLine.addToken(PUNCTUATION, ",");
                }
            }
            definitionLine.addToken(PUNCTUATION, ")", Spacing.NO_SPACE);
            return;
        } else if (expression instanceof StringLiteralExpr) {
            StringLiteralExpr stringLiteralExpr = (StringLiteralExpr) expression;
            definitionLine.addToken(STRING_LITERAL, stringLiteralExpr.toString(), Spacing.NO_SPACE);
            return;
        } else if (expression instanceof ArrayInitializerExpr) {
            ArrayInitializerExpr arrayInitializerExpr = (ArrayInitializerExpr) expression;
            if (!condensed) {
                definitionLine.addToken(PUNCTUATION, "{");
            }
            for (int i = 0; i < arrayInitializerExpr.getChildNodes().size(); i++) {
                Node n = arrayInitializerExpr.getChildNodes().get(i);

                if (n instanceof Expression) {
                    visitExpression((Expression) n, definitionLine, condensed);
                } else {
                    definitionLine.addToken(TEXT, arrayInitializerExpr.toString()); // FIXME This was ANNOTATION_PARAMETER_VALUE
                }

                if (i < arrayInitializerExpr.getChildNodes().size() - 1) {
                    definitionLine.addToken(PUNCTUATION, ",");
                }
            }
            if (!condensed) {
                definitionLine.addToken(PUNCTUATION, "}", Spacing.SPACE_BEFORE);
            }
            return;
        } else if (expression instanceof IntegerLiteralExpr ||
                   expression instanceof LongLiteralExpr ||
                   expression instanceof DoubleLiteralExpr) {
            definitionLine.addToken(NUMBER, expression.toString(), Spacing.NO_SPACE);
            return;
        } else if (expression instanceof FieldAccessExpr) {
            FieldAccessExpr fieldAccessExpr = (FieldAccessExpr) expression;
            visitExpression(fieldAccessExpr.getScope(), definitionLine);
            definitionLine
                .addToken(PUNCTUATION, ".", Spacing.NO_SPACE)
                .addToken(FIELD_NAME, fieldAccessExpr.getNameAsString(), Spacing.NO_SPACE);
            return;
        } else if (expression instanceof BinaryExpr) {
            BinaryExpr binaryExpr = (BinaryExpr) expression;
            visitExpression(binaryExpr.getLeft(), definitionLine);
            definitionLine.addToken(TEXT, " " + binaryExpr.getOperator().asString() + " ");
            visitExpression(binaryExpr.getRight(), definitionLine);
            return;
        } else if (expression instanceof ClassExpr) {
            ClassExpr classExpr = (ClassExpr) expression;
            // lookup to see if the type is known about, if so, make it a link, otherwise leave it as text
            String typeName = classExpr.getChildNodes().get(0).toString();
//            if (apiListing.getKnownTypes().containsKey(typeName)) {
            definitionLine.addToken(TYPE_NAME, typeName, null, Spacing.NO_SPACE); // FIXME add ID
            return;
//            }
//        } else {
//            node.addToken(TEXT, expression.toString());
        } else if (expression instanceof NameExpr) {
            NameExpr nameExpr = (NameExpr) expression;
            definitionLine.addToken(TYPE_NAME, nameExpr.toString(), Spacing.NO_SPACE);
            return;
        } else if (expression instanceof BooleanLiteralExpr) {
            BooleanLiteralExpr booleanLiteralExpr = (BooleanLiteralExpr) expression;
            definitionLine.addToken(KEYWORD, booleanLiteralExpr.toString(), Spacing.NO_SPACE);
            return;
        } else if (expression instanceof ObjectCreationExpr) {
            definitionLine.addToken(KEYWORD, "new")
                .addToken(TYPE_NAME, ((ObjectCreationExpr) expression).getTypeAsString())
                .addToken(PUNCTUATION, "(")
                .addToken(COMMENT, "/* Elided */")
                .addToken(PUNCTUATION, ")");
            return;
        }

        // if we fall through to here, just treat it as a string.
        // If we are in condensed mode, we strip off everything before the last period
        String value = expression.toString();
        if (condensed) {
            int lastPeriod = value.lastIndexOf('.');
            if (lastPeriod != -1) {
                value = value.substring(lastPeriod + 1);
            }
            definitionLine.addToken(TEXT, upperCase(value));
        } else {
            definitionLine.addToken(TEXT, value);
        }
    }

    private void visitAnnotations(final NodeWithAnnotations<?> nodeWithAnnotations,
                                  final boolean showAnnotationProperties,
                                  final boolean addNewline,
                                  final ReviewLine parentLine,
                                  final ReviewLine targetLine) {
        visitAnnotations(nodeWithAnnotations, showAnnotationProperties, addNewline, parentLine, targetLine.getLineId());
    }

    private void visitAnnotations(final NodeWithAnnotations<?> nodeWithAnnotations,
                                  final boolean showAnnotationProperties,
                                  final boolean addNewline,
                                  final ReviewLine parentLine,
                                  final String targetLine) {

        final ReviewLine[] currentReviewLine = new ReviewLine[] { parentLine };

        Consumer<AnnotationExpr> consumer = annotation -> {
            // Check the annotation rules map for any overrides
            final String annotationName = annotation.getName().toString();
            AnnotationRule annotationRule = ANNOTATION_RULE_MAP.get(annotationName);

            AnnotationRendererModel model = new AnnotationRendererModel(
                    annotation, nodeWithAnnotations, annotationRule, showAnnotationProperties, addNewline);

            if (model.isHidden()) {
                return;
            }

            if (model.isAddNewline()) {
                currentReviewLine[0] = parentLine.addChildLine();
                currentReviewLine[0].setRelatedToLine(targetLine);
            }

            renderAnnotation(model, currentReviewLine[0]);
        };

        nodeWithAnnotations.getAnnotations()
            .stream()
            .filter(annotationExpr -> !annotationExpr.getName().getIdentifier().startsWith("Json"))
            .sorted(Comparator.comparing(a -> a.getName().getIdentifier())) // we sort the annotations alphabetically
            .forEach(consumer);
    }

    private void renderAnnotation(AnnotationRendererModel m, ReviewLine reviewLine) {
        final AnnotationExpr a = m.getAnnotation();

        reviewLine.addToken(ANNOTATION_NAME, "@" + a.getNameAsString(), Spacing.NO_SPACE);
        if (m.isShowProperties()) {
            if (a instanceof NormalAnnotationExpr) {
                NormalAnnotationExpr d = (NormalAnnotationExpr) a;
                reviewLine.addToken(PUNCTUATION, "(", Spacing.NO_SPACE);
                NodeList<MemberValuePair> pairs = d.getPairs();
                for (int i = 0; i < pairs.size(); i++) {
                    MemberValuePair pair = pairs.get(i);

                    // If the pair is a boolean expression, and we are condensed, we only take the name.
                    // If we are not a boolean expression, and we are condensed, we only take the value.
                    // If we are not condensed, we take both.
                    final Expression valueExpr = pair.getValue();
                    if (m.isCondensed()) {
                        if (valueExpr.isBooleanLiteralExpr()) {
                            reviewLine.addToken(ANNOTATION_PARAMETER_NAME, upperCase(pair.getNameAsString()));
                        } else {
                            visitExpression(valueExpr, reviewLine, m.isCondensed());
                        }
                    } else {
                        reviewLine.addToken(ANNOTATION_PARAMETER_NAME, pair.getNameAsString());
                        reviewLine.addToken(PUNCTUATION, " = ");
                        visitExpression(valueExpr, reviewLine, m.isCondensed());
                    }

                    if (i < pairs.size() - 1) {
                        reviewLine.addToken(PUNCTUATION, ",");
                    }
                }

                reviewLine.addToken(PUNCTUATION, ")");
            } else if (a instanceof SingleMemberAnnotationExpr) {
                SingleMemberAnnotationExpr d = (SingleMemberAnnotationExpr) a;
                reviewLine.addToken(PUNCTUATION, "(", Spacing.NO_SPACE);
                visitExpression(d.getMemberValue(), reviewLine, m.isCondensed());
                reviewLine.addToken(PUNCTUATION, ")", Spacing.NO_SPACE);
            }
        } else {
            reviewLine.addSpace();
        }
    }

    private void visitDeclarationNameAndParameters(CallableDeclaration<?> callableDeclaration,
                                                   NodeList<Parameter> parameters,
                                                   ReviewLine definitionLine) {
        String name = callableDeclaration.getNameAsString();
        ReviewToken nameToken = new ReviewToken(METHOD_NAME, name).setSpacing(Spacing.NO_SPACE);
        checkForCrossLanguageDefinitionId(definitionLine, callableDeclaration);
        definitionLine.addToken(nameToken);
        possiblyDeprecate(nameToken, callableDeclaration);

        definitionLine.addToken(PUNCTUATION, "(", Spacing.NO_SPACE);

        if (!parameters.isEmpty()) {
            for (int i = 0, max = parameters.size(); i < max; i++) {
                final Parameter parameter = parameters.get(i);
                visitType(parameter, definitionLine, PARAMETER_TYPE);
                definitionLine.addToken(PARAMETER_NAME, parameter.getNameAsString(), Spacing.NO_SPACE);

                if (i < max - 1) {
                    definitionLine.addToken(PUNCTUATION, ",");//.addToken(TokenKind.PARAMETER_SEPARATOR, " ");
                }
            }
        }

        // FIXME this is a bit hacky - it would be nice to have 'MethodRule' like we do for Annotations.
        // we want to special-case here for methods called `getLatest()` which are within a class that implements the
        // `ServiceVersion` interface. We want to add a comment to the method to indicate that it is a special method
        // that is used to get the latest version of a service.
        Node parentNode = callableDeclaration.getParentNode().orElse(null);
        if (callableDeclaration instanceof MethodDeclaration) {
            MethodDeclaration m = (MethodDeclaration) callableDeclaration;
            if (callableDeclaration.getNameAsString().equals("getLatest")) {
                if (parentNode instanceof EnumDeclaration) {
                    EnumDeclaration d = (EnumDeclaration) parentNode;
                    if (d.getImplementedTypes().stream().anyMatch(implementedType -> implementedType.getName().toString().equals("ServiceVersion"))) {
                        m.getBody().flatMap(blockStmt -> blockStmt.getStatements().stream()
                                    .filter(Statement::isReturnStmt)
                                    .findFirst())
                            .ifPresent(ret -> definitionLine.addToken(COMMENT, "// returns " + ret.getChildNodes().get(0)));
                    }
                }
            }
        }

        // close declaration
        definitionLine.addToken(PUNCTUATION, ")");
    }

    private boolean visitTypeParameters(NodeList<TypeParameter> typeParameters, ReviewLine reviewLine) {
        final int size = typeParameters.size();
        if (size == 0) {
            return false;
        }
        reviewLine.addToken(PUNCTUATION, "<", Spacing.NO_SPACE);
        for (int i = 0; i < size; i++) {
            final TypeParameter typeParameter = typeParameters.get(i);
            visitGenericTypeParameter(typeParameter, reviewLine);
            if (i != size - 1) {
                reviewLine.addToken(PUNCTUATION, ",");
            }
        }
        reviewLine.addToken(PUNCTUATION, ">", spacingState.getSpacing());
        return true;
    }

    private void visitGenericTypeParameter(TypeParameter typeParameter, ReviewLine reviewLine) {
        reviewLine.addToken(TYPE_NAME, typeParameter.getNameAsString(), spacingState.getSpacing());

        // get type bounds
        final NodeList<ClassOrInterfaceType> typeBounds = typeParameter.getTypeBound();
        final int size = typeBounds.size();
        if (size != 0) {
            Spacing nextSpacing = spacingState == SpacingState.SKIP_NEXT_SUFFIX ? Spacing.SPACE_BEFORE : Spacing.NO_SPACE;
            reviewLine.addToken(KEYWORD, "extends", nextSpacing);
            for (ClassOrInterfaceType typeBound : typeBounds) {
                visitType(typeBound, reviewLine, TYPE_NAME);
            }
        }
    }

    private void visitThrowException(NodeWithThrownExceptions<?> node, ReviewLine methodLine) {
        final NodeList<ReferenceType> thrownExceptions = node.getThrownExceptions();
        if (thrownExceptions.isEmpty()) {
            return;
        }

        methodLine.addToken(KEYWORD, "throws");
        commaSeparateList(methodLine, TYPE_NAME, thrownExceptions, e -> e.getElementType().toString());
    }

    private void visitType(Object type, ReviewLine reviewLine, TokenKind kind) {
        if (type instanceof Parameter) {
            Parameter d = (Parameter) type;
            boolean isVarArgs = d.isVarArgs();
            spacingState = isVarArgs ? SpacingState.SKIP_NEXT_SUFFIX : SpacingState.DEFAULT;
            visitClassType(d.getType(), reviewLine, kind);
            spacingState = SpacingState.DEFAULT;
            if (isVarArgs) {
                reviewLine.addToken(kind, "...");
            }
        } else if (type instanceof MethodDeclaration) {
            visitClassType(((MethodDeclaration)type).getType(), reviewLine, kind);
        } else if (type instanceof FieldDeclaration) {
            visitClassType(((FieldDeclaration)type).getElementType(), reviewLine, kind);
        } else if (type instanceof ClassOrInterfaceType) {
            visitClassType(((ClassOrInterfaceType)type), reviewLine, kind);
        } else if (type instanceof AnnotationDeclaration ||
                   type instanceof ConstructorDeclaration ||
                   type instanceof ClassOrInterfaceDeclaration ||
                   type instanceof EnumDeclaration) {
            // no-op
        } else {
            System.err.println("Unknown type " + type + " of type " + type.getClass());
        }
    }

    private void visitClassType(Type type, ReviewLine reviewLine, TokenKind kind) {
        SpacingState spacingBefore = spacingState;

        if (type.isPrimitiveType()) {
            reviewLine.addToken(kind, type.asPrimitiveType().toString(), spacingState.getSpacing());
        } else if (type.isVoidType()) {
            reviewLine.addToken(kind, "void", spacingState.getSpacing());
        } else if (type.isReferenceType()) {
            // Array Type
            type.ifArrayType(arrayType -> {
                // No space between the array type and the brackets
                spacingState = SpacingState.SKIP_NEXT_SUFFIX;
                visitClassType(arrayType.getComponentType(), reviewLine, kind);
                reviewLine.addToken(kind, "[]", Spacing.DEFAULT);
            });
            // Class or Interface type
            type.ifClassOrInterfaceType(t -> visitTypeDFS(t, reviewLine, kind));
        } else if (type.isWildcardType()) {
            // TODO: add wild card type implementation, #756
        } else if (type.isUnionType()) {
            // TODO: add union type implementation, #756
        } else if (type.isIntersectionType()) {
            // TODO: add intersection type implementation, #756
        } else {
            System.err.println("Unknown type");
        }
        spacingState = spacingBefore;
    }

    private void visitTypeDFS(Node node, ReviewLine reviewLine, TokenKind kind) {
        final List<Node> nodes = node.getChildNodes();
        final int childrenSize = nodes.size();
        // Recursion's base case: leaf node
        if (childrenSize <= 1) {
            if (node instanceof WildcardType) {
                final WildcardType d = (WildcardType) node;
                if (d.getExtendedType().isPresent()) {
                    reviewLine.addToken(kind, "?")
                              .addToken(KEYWORD, "extends");
                    visitTypeDFS(d.getExtendedType().get(), reviewLine, kind);
                } else if (d.getSuperType().isPresent()) {
                    reviewLine.addToken(kind, "?")
                              .addToken(KEYWORD, "super");
                    visitTypeDFS(d.getSuperType().get(), reviewLine, kind);
                } else {
                    reviewLine.addToken(kind, "?", Spacing.NO_SPACE);
                }
            } else {
                final ReviewToken token = new ReviewToken(kind, node.toString());
                token.setSpacing(spacingState.getSpacing());
                reviewLine.addToken(token);
                possiblyAddNavigationLink(token, node);
            }
            return;
        }

        /*
         * A type, "Map<String, Map<Integer, Double>>", will be treated as three nodes at the same level and has
         * two type arguments, String and Map<Integer, Double>:
         *
         * node one = "Map", node two = "String", and node three = "Map<Integer, Double>"
         * node three, "Map<Integer, Double>", has two type arguments: Integer and Double.
         *
         * But a type with full package name will be treated as two nodes:
         *
         * E.x., "com.azure.example.TypeA", it has two nodes:
         * node one = "com.azure.example", node two = "TypeA"
         * Furthermore, node one has two children: "com.azure" and "example".
         *
         * A type with full package name and type arguments will have (2 + number of type arguments) nodes:
         *
         * E.x., "java.util.List<String>" has three nodes:
         * node one = "java.util"
         * node two = List
         * node three = String
         *
         * "java.util.Map<String, Integer>" has four nodes:
         * node one = java.util
         * node two = Map
         * node three = String
         * node four = Integer
         */

        Optional<NodeList<Type>> typeArguments = ((ClassOrInterfaceType) node).getTypeArguments();
        List<Node> childNodes = node.getChildNodes();

        if (typeArguments.isPresent()) {
            NodeList<Type> types = typeArguments.get();
            int genericTypeArgsCount = types.size();
            for (int i = 0; i < childNodes.size() - genericTypeArgsCount; i++) {
                // for each child node that is not a type arg, use "." separator
                if (i > 0) {
                    reviewLine.addToken(PUNCTUATION, ".", Spacing.NO_SPACE);
                }
                SpacingState before = spacingState;
                spacingState = SpacingState.SKIP_NEXT_SUFFIX;
                visitTypeDFS(childNodes.get(i), reviewLine, kind);
                spacingState = before;
            }
            // Now, add "<" before adding the type arg nodes
            reviewLine.addToken(PUNCTUATION, "<", Spacing.NO_SPACE);
            for (int i = childNodes.size() - genericTypeArgsCount; i < childNodes.size(); i++) {
                if (i > childNodes.size() - genericTypeArgsCount) {
                    // Add a "," separator if there are more two or more type args
                    reviewLine.addToken(PUNCTUATION, ",");
                }
                SpacingState before = spacingState;
                spacingState = SpacingState.SKIP_NEXT_SUFFIX;
                visitTypeDFS(childNodes.get(i), reviewLine, kind);
                spacingState = before;
            }
            // Close the type args with ">"
            reviewLine.addToken(PUNCTUATION, ">", spacingState.getSpacing());
        } else {
            // No type args, all child nodes are just part of the fully qualified class name
            for (int i = 0; i < childNodes.size(); i++) {
                if (i > 0) {
                    reviewLine.addToken(PUNCTUATION, ".", Spacing.NO_SPACE);
                }
                SpacingState before = spacingState;
                spacingState = SpacingState.SKIP_NEXT_SUFFIX;
                visitTypeDFS(childNodes.get(i), reviewLine, kind);

                if (i == childNodes.size() - 1) {
                    reviewLine.addSpace();
                }

                spacingState = before;
            }
        }
    }

    private void addDefaultConstructor(TypeDeclaration<?> typeDeclaration, ReviewLine definitionLine) {
        final String name = typeDeclaration.getNameAsString();
        final String definitionId = makeId(typeDeclaration.getNameAsString());

        definitionLine.addChildLine(makeId(typeDeclaration)+"_default_ctor")
            .addToken(KEYWORD, "public")
            .addToken(METHOD_NAME, name, definitionId)
            .addToken(PUNCTUATION, "()");
    }

    private void visitJavaDoc(final BodyDeclaration<?> bodyDeclaration, final ReviewLine reviewLine, String targetLineId) {
        attemptToFindJavadocComment(bodyDeclaration).ifPresent(jd -> visitJavaDoc(jd, reviewLine, targetLineId));
    }

    private void visitJavaDoc(JavadocComment javadoc, Parent parent, String targetLineId) {
        String str = javadoc.toString();
        if (str == null || str.isEmpty()) {
            return;
        }

        String[] lines = str.split("\n");
        if (lines.length == 0) {
            return;
        }

        Stream.of(lines).forEach(line -> {
            final ReviewLine reviewLine = parent.addChildLine().setRelatedToLine(targetLineId);

            if (line.contains("&")) {
                line = HtmlEscape.unescapeHtml(line);
            }

            // convert http/s links to external clickable links
            if (JAVADOC_EXTRACT_LINKS) {
                Matcher urlMatch = MiscUtils.URL_MATCH.matcher(line);
                int currentIndex = 0;
                while (urlMatch.find(currentIndex)) {
                    int start = urlMatch.start();
                    int end = urlMatch.end();

                    // if the current search index != start of match, there was text between two hyperlinks
                    if (currentIndex != start) {
                        String betweenValue = line.substring(currentIndex, start);
                        // FIXME
//                        parentNode.addTopToken(new ReviewToken(JAVADOC, betweenValue).addTag(TAG_SKIP_DIFF));
                    }

                    String matchedValue = line.substring(start, end);
                    // FIXME
//                    parentNode.addTopToken(new ReviewToken(URL, matchedValue)
//                            .addProperty(PROPERTY_URL_LINK_TEXT, matchedValue)
//                            .addTag(TAG_SKIP_DIFF));

                    currentIndex = end;
                }

                // end of line will be anything between the end of the last found link, and the end of the string
                String finalValue = line.substring(currentIndex);
                reviewLine.addToken(new ReviewToken(JAVADOC, finalValue).setDocumentation().setSkipDiff());
            } else {
                reviewLine.addToken(new ReviewToken(JAVADOC, line).setDocumentation().setSkipDiff());
            }
        });
    }

    private static TokenKind getTokenKind(TypeDeclaration<?> typeDeclaration) {
        if (typeDeclaration.isClassOrInterfaceDeclaration()) {
            return ((ClassOrInterfaceDeclaration) typeDeclaration).isInterface() ? TokenKind.INTERFACE : TokenKind.CLASS;
        } else if (typeDeclaration.isEnumDeclaration()) {
            return TokenKind.ENUM;
        } else if (typeDeclaration.isAnnotationDeclaration()) {
            return TokenKind.ANNOTATION;
        } else {
            return TokenKind.CLASS;
        }
    }

    private static SpacingState spacingState = SpacingState.DEFAULT;
    private enum SpacingState {
        DEFAULT(Spacing.DEFAULT),

        SKIP_NEXT_SUFFIX(Spacing.NO_SPACE);

        private final Spacing spacing;

        SpacingState(Spacing spacing) {
            this.spacing = spacing;
        }

        public Spacing getSpacing() {
            return spacing;
        }
    }
}