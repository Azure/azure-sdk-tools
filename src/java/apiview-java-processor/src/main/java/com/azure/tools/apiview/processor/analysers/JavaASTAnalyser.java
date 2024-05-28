package com.azure.tools.apiview.processor.analysers;

import com.azure.tools.apiview.processor.analysers.models.*;
import com.azure.tools.apiview.processor.analysers.util.MiscUtils;
import com.azure.tools.apiview.processor.diagnostics.Diagnostics;
import com.azure.tools.apiview.processor.model.*;
import com.azure.tools.apiview.processor.model.maven.*;
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

public class JavaASTAnalyser implements Analyser {

    /************************************************************************************************
     *
     * Constants
     *
     **********************************************************************************************/
    public static final String PROPERTY_MODULE_NAME = "module-name";
    public static final String PROPERTY_MODULE_EXPORTS = "module-exports";
    public static final String PROPERTY_MODULE_REQUIRES = "module-requires";
    public static final String PROPERTY_MODULE_OPENS = "module-opens";

    public static final String MAVEN_KEY = "Maven";
    public static final String MODULE_INFO_KEY = "module-info";

    private static final String RENDER_CLASS_DEPRECATED = "deprecated";

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
        ANNOTATION_RULE_MAP.put("ServiceMethod", new AnnotationRule().setHidden(true));
        ANNOTATION_RULE_MAP.put("SuppressWarnings", new AnnotationRule().setHidden(true));

        // Retention and Target annotations are always useful to see fully-expanded
        ANNOTATION_RULE_MAP.put("Retention", new AnnotationRule().setShowOnNewline(true).setShowProperties(true));
        ANNOTATION_RULE_MAP.put("Target", new AnnotationRule().setShowOnNewline(true).setShowProperties(true));

        // we always want @Metadata annotations to be fully expanded, but in a condensed form
        ANNOTATION_RULE_MAP.put("Metadata", new AnnotationRule().setShowProperties(true).setCondensed(true));

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

    private final TreeNode libraryRootNode;
    private TreeNode rootPackageNode;


    /************************************************************************************************
     *
     * Constructors
     *
     **********************************************************************************************/

    public JavaASTAnalyser(APIListing apiListing) {
        this.apiListing = apiListing;
        this.diagnostic = new Diagnostics(apiListing);

        // this is the root node of the library, and it will contain all the other nodes
        final String name = apiListing.getMavenPom().getArtifactId();
        this.libraryRootNode = new TreeNode(name, name, TreeNodeKind.ASSEMBLY);
        apiListing.addTreeNode(libraryRootNode);
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
        allFiles.stream()
            .filter(this::filterFilePaths)
            .map(this::scanForTypes)
            .filter(Optional::isPresent)
            .map(Optional::get)
            .collect(Collectors.groupingBy(e -> e.packageName, TreeMap::new, Collectors.toList()))
            .forEach(this::processPackage);

        // we conclude by doing a final pass over all diagnostics to enable them to do any final analysis based on
        // the already-executed individual scans
        diagnostic.scanFinal(apiListing);
    }



    /************************************************************************************************
     *
     * Inner Classes (to aid in analysis)
     *
     **********************************************************************************************/

    private enum ScanElementType {
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

        public ScanClass(Path path, CompilationUnit compilationUnit) {
            super(path, compilationUnit, ScanElementType.CLASS);
            this.primaryTypeName = compilationUnit != null ? compilationUnit.getPrimaryTypeName().orElse("") : "";
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
            CompilationUnit compilationUnit = path.endsWith("module-info.java")
                ? JAVA_11_PARSER.parse(Files.newBufferedReader(path))
                : JAVA_8_PARSER.parse(Files.newBufferedReader(path));

            compilationUnit.setStorage(path, StandardCharsets.UTF_8);

            // we build up a map between types and the packages they are in, for use in our diagnostic rules
            compilationUnit.getImports().stream()
                    .map(ImportDeclaration::getName)
                    .forEach(name -> name.getQualifier().ifPresent(packageName ->
                            apiListing.addPackageTypeMapping(packageName.toString(), name.getIdentifier())));

            if (path.endsWith("package-info.java")) {
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
        final TreeNode packageNode = addChild(libraryRootNode, packageName, makeId("package-"+packageName), TreeNodeKind.NAMESPACE);

        // lets see if we have javadoc for this packageName
        scanElements.stream()
                .filter(scanElement -> scanElement.elementType == ScanElementType.PACKAGE)
                .findFirst()
                .flatMap(scanElement -> scanElement.compilationUnit.getPackageDeclaration().flatMap(Node::getComment))
                .filter(Comment::isJavadocComment)
                .ifPresent(javadocComment -> visitJavaDoc((JavadocComment) javadocComment, packageNode));

        packageNode.addTopToken(KEYWORD, "package").addSpace();

        if (packageName.isEmpty()) {
            packageNode.hideFromNavigation().addTopToken(PACKAGE_NAME, "<root package>").addSpace().addTopToken(PUNCTUATION, "{");
            this.rootPackageNode = packageNode;

            // look for the maven pom.xml file, and put that in first, in the root package
            tokeniseMavenPom(apiListing.getMavenPom());
        } else {
            packageNode.addTopToken(PACKAGE_NAME, packageName, packageName).addSpace().addTopToken(PUNCTUATION, "{");
        }

        // then do all classes in the package
        scanElements.stream()
            .filter(scanElement -> scanElement.elementType == ScanElementType.CLASS)
            .map(scanElement -> (ScanClass) scanElement)
            .sorted(Comparator.comparing(e -> e.primaryTypeName))
            .forEach(scanClass -> visitCompilationUnit(scanClass.compilationUnit, packageNode));

        packageNode.addBottomToken(PUNCTUATION, "}");
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

    private void possiblyDeprecate(Token token, NodeWithAnnotations<?> n) {
        if (n.isAnnotationPresent("Deprecated")) {
            token.addRenderClass(RENDER_CLASS_DEPRECATED);
        }
    }



    /************************************************************************************************
     *
     * Analysis implementation - Maven POM support
     *
     **********************************************************************************************/

    private void tokeniseMavenPom(Pom mavenPom) {
        TreeNode mavenNode = addChild(rootPackageNode, MAVEN_KEY, MAVEN_KEY, TreeNodeKind.MAVEN);

        mavenNode.addTopToken(TokenKind.MAVEN_KEY, "maven", MAVEN_KEY).addSpace()
                .addTopToken(PUNCTUATION, "{")
                .addBottomToken(PUNCTUATION, "}");

//            addToken(new Token(SKIP_DIFF_START));
        // parent
        String gavStr = mavenPom.getParent().getGroupId() + ":" + mavenPom.getParent().getArtifactId() + ":"
                + mavenPom.getParent().getVersion();
        tokeniseKeyValue(mavenNode, "parent", gavStr, "");

        // properties
        gavStr = mavenPom.getGroupId() + ":" + mavenPom.getArtifactId() + ":" + mavenPom.getVersion();
        tokeniseKeyValue(mavenNode, "properties", gavStr, "");

        // configuration
        boolean showJacoco = mavenPom.getJacocoMinLineCoverage() != null
                && mavenPom.getJacocoMinBranchCoverage() != null;
        boolean showCheckStyle = mavenPom.getCheckstyleExcludes() != null && !mavenPom.getCheckstyleExcludes()
                .isEmpty();

        if (showJacoco || showCheckStyle) {
            TreeNode configurationNode = addChild(mavenNode, "configuration", "configuration", TreeNodeKind.MAVEN);
            configurationNode.addTopToken(TokenKind.MAVEN_KEY, "configuration")
                    .hideFromNavigation().addSpace()
                    .addTopToken(PUNCTUATION, "{")
                    .addBottomToken(PUNCTUATION, "}");

            if (showCheckStyle) {
                tokeniseKeyValue(configurationNode, "checkstyle-excludes", mavenPom.getCheckstyleExcludes(), "");
            }
            if (showJacoco) {
                TreeNode jacocoNode = addChild(configurationNode, "jacoco", "jacoco", TreeNodeKind.MAVEN);
                jacocoNode.addTopToken(TokenKind.MAVEN_KEY, "jacoco")
                        .hideFromNavigation().addSpace()
                        .addTopToken(PUNCTUATION, "{");

                tokeniseKeyValue(jacocoNode, "min-line-coverage", mavenPom.getJacocoMinLineCoverage(), "jacoco");
                tokeniseKeyValue(jacocoNode, "min-branch-coverage", mavenPom.getJacocoMinBranchCoverage(), "jacoco");
            }
        }

        // Maven name
        tokeniseKeyValue(mavenNode, "name", mavenPom.getName(), "");

        // Maven description
        tokeniseKeyValue(mavenNode, "description", mavenPom.getDescription(), "");

        // dependencies
        TreeNode dependenciesNode = addChild(mavenNode,"dependencies", "dependencies", TreeNodeKind.MAVEN);
        dependenciesNode
                .hideFromNavigation()
                .addTopToken(TokenKind.MAVEN_KEY, "dependencies").addSpace()
                .addTopToken(PUNCTUATION, "{")
                .addBottomToken(PUNCTUATION, "}");

        mavenPom.getDependencies()
                .stream()
                .collect(Collectors.groupingBy(Dependency::getScope))
                .forEach((k, v) -> {
                    if ("test".equals(k)) {
                        // we don't care to present test scope dependencies
                        return;
                    }
                    String scope = k.isEmpty() ? "compile" : k;

                    dependenciesNode.addChild(new TreeNode(scope, scope, TreeNodeKind.MAVEN)
                            .addTopToken(COMMENT, "// " + scope + " scope"));

                    for (Dependency d : v) {
                        String gav = d.getGroupId() + ":" + d.getArtifactId() + ":" + d.getVersion();
                        dependenciesNode.addChild(new TreeNode(gav, gav, TreeNodeKind.MAVEN)
                                .addTopToken(MAVEN_DEPENDENCY, gav, gav));
                    }
                });
//
//            addToken(new Token(SKIP_DIFF_END));
    }

    private void tokeniseKeyValue(TreeNode parentNode, String key, Object value, String linkPrefix) {
        parentNode.addChild(TreeNode.createHiddenNode()
                .addTopToken(TokenKind.MAVEN_KEY, key)
                .addTopToken(PUNCTUATION, ":").addSpace()
                .addTopToken(MiscUtils.tokeniseMavenKeyValue(key, value, linkPrefix)));
    }



    /************************************************************************************************
     *
     * Analysis implementation - Module declaration support
     *
     **********************************************************************************************/

    private void visitModuleDeclaration(ModuleDeclaration moduleDeclaration) {
        TreeNode moduleNode = addChild(rootPackageNode, "module-info", MODULE_INFO_KEY, TreeNodeKind.MODULE_INFO);
        moduleNode.addProperty(PROPERTY_MODULE_NAME, moduleDeclaration.getNameAsString());

        moduleNode.addTopToken(KEYWORD, "module").addSpace()
            .addTopToken(MODULE_NAME, moduleDeclaration.getNameAsString(), MODULE_INFO_KEY).addSpace()
            .addTopToken(PUNCTUATION, "{")
            .addBottomToken(PUNCTUATION, "}");

        // Sometimes an exports or opens statement is conditional, so we need to handle that case
        // in a single location here, to remove duplication.
        BiConsumer<TreeNode, NodeList<Name>> conditionalExportsToOrOpensToConsumer = (node, names) -> {
            if (!names.isEmpty()) {
                node.addSpace()
                    .addTopToken(KEYWORD, "to").addSpace();
                commaSeparateList(node, TYPE_NAME, names, Object::toString);
            }
        };

        moduleDeclaration.getDirectives().forEach(moduleDirective -> {
            moduleDirective.ifModuleRequiresStmt(d -> {
                TreeNode moduleChildNode;
                String id = makeId(MODULE_INFO_KEY + "-requires-" + d.getNameAsString());
                moduleNode.addChild(moduleChildNode = new TreeNode(PROPERTY_MODULE_REQUIRES, id, TreeNodeKind.MODULE_REQUIRES)
                    .hideFromNavigation()
                    .addTopToken(KEYWORD, "requires").addSpace());

                if (d.hasModifier(Modifier.Keyword.STATIC)) {
                    moduleChildNode.addTopToken(KEYWORD, "static").addSpace();
                }

                if (d.isTransitive()) {
                    moduleChildNode.addTopToken(KEYWORD, "transitive").addSpace();
                }

                // adding property just to make diagnostics easier
                moduleChildNode.addProperty(PROPERTY_MODULE_REQUIRES, d.getNameAsString());
                moduleChildNode.addProperty("static", d.hasModifier(Modifier.Keyword.STATIC) ? "true" : "false");
                moduleChildNode.addProperty("transitive", d.isTransitive() ? "true" : "false");

                moduleChildNode.addTopToken(TYPE_NAME, d.getNameAsString());
                moduleChildNode.addTopToken(PUNCTUATION, ";");
            });

            moduleDirective.ifModuleExportsStmt(d -> {
                TreeNode moduleChildNode;
                String id = makeId(MODULE_INFO_KEY + "-exports-" + d.getNameAsString());
                moduleNode.addChild(moduleChildNode = new TreeNode("exports", id, TreeNodeKind.MODULE_EXPORTS)
                    .hideFromNavigation()
                    .addTopToken(KEYWORD, "exports").addSpace()
                    .addTopToken(TYPE_NAME, d.getNameAsString()));

                // adding property just to make diagnostics easier
                moduleChildNode.addProperty(PROPERTY_MODULE_EXPORTS, d.getNameAsString());

                conditionalExportsToOrOpensToConsumer.accept(moduleChildNode, d.getModuleNames());
                moduleChildNode.addTopToken(PUNCTUATION, ";");
            });

            moduleDirective.ifModuleOpensStmt(d -> {
                TreeNode moduleChildNode;
                String id = makeId(MODULE_INFO_KEY + "-opens-" + d.getNameAsString());
                moduleNode.addChild(moduleChildNode = new TreeNode("opens", id, TreeNodeKind.MODULE_OPENS)
                    .hideFromNavigation()
                    .addTopToken(KEYWORD, "opens").addSpace()
                    .addTopToken(TYPE_NAME, d.getNameAsString()));

                // adding property just to make diagnostics easier
                moduleChildNode.addProperty(PROPERTY_MODULE_OPENS, d.getNameAsString());

                conditionalExportsToOrOpensToConsumer.accept(moduleChildNode, d.getModuleNames());
                moduleChildNode.addTopToken(PUNCTUATION, ";");
            });

            moduleDirective.ifModuleUsesStmt(d -> {
                String id = makeId(MODULE_INFO_KEY + "-uses-" + d.getNameAsString());
                moduleNode.addChild(new TreeNode("uses", id, TreeNodeKind.MODULE_USES)
                    .hideFromNavigation()
                    .addTopToken(KEYWORD, "uses").addSpace()
                    .addTopToken(TYPE_NAME, d.getNameAsString())
                    .addTopToken(PUNCTUATION, ";"));
            });

            moduleDirective.ifModuleProvidesStmt(d -> {
                TreeNode moduleChildNode;
                String id = makeId(MODULE_INFO_KEY + "-provides-" + d.getNameAsString());
                moduleNode.addChild(moduleChildNode = new TreeNode("provides", id, TreeNodeKind.MODULE_PROVIDES)
                    .hideFromNavigation()
                    .addTopToken(KEYWORD, "provides").addSpace()
                    .addTopToken(TYPE_NAME, d.getNameAsString()).addSpace()
                    .addTopToken(KEYWORD, "with").addSpace());

                commaSeparateList(moduleChildNode, TYPE_NAME, d.getWith(), Object::toString);
                moduleChildNode.addTopToken(PUNCTUATION, ";");
            });
        });
    }

    private <T> void commaSeparateList(TreeNode node,
                                   TokenKind kind,
                                   Iterable<T> it,
                                   Function<T, String> func) {
        Iterator<T> iterator = it.iterator();
        if (iterator.hasNext()) {
            node.addTopToken(kind, func.apply(iterator.next()));
            while (iterator.hasNext()) {
                node.addTopToken(PUNCTUATION, ",").addSpace()
                    .addTopToken(kind, func.apply(iterator.next()));
            }
        }
    }

    /************************************************************************************************
     *
     * Analysis implementation - Standard Java support
     *
     **********************************************************************************************/

    private void visitCompilationUnit(CompilationUnit compilationUnit, TreeNode parentNode) {
        compilationUnit.getModule().ifPresent(this::visitModuleDeclaration);

        NodeList<TypeDeclaration<?>> types = compilationUnit.getTypes();
        for (final TypeDeclaration<?> typeDeclaration : types) {
            visitDefinition(typeDeclaration, parentNode);
        }

        diagnostic.scanIndividual(compilationUnit, apiListing);
    }

    // This method is for constructors, fields, methods, etc.
    private void visitDefinition(BodyDeclaration<?> definition, TreeNode parentNode) {
        // firstly, we need to make a definition node and attach it to its parent node. The kind of definitionNode we
        // make depends on the kind of definition we are processing.
        final TreeNode definitionNode;

        boolean isTypeDeclaration = false;
        switch (definition) {
            case TypeDeclaration<?> typeDeclaration -> {
                // Skip if the class is private or package-private, unless it is a nested type defined inside a public interface
                if (!isTypeAPublicAPI(typeDeclaration)) {
                    return;
                }

                final String id = makeId(typeDeclaration);
                definitionNode = new TreeNode(typeDeclaration.getNameAsString(), id, getTreeNodeKind(typeDeclaration));
                apiListing.getKnownTypes().put(typeDeclaration.getFullyQualifiedName().orElse(""), id);
                isTypeDeclaration = true;
            }
            case FieldDeclaration fieldDeclaration ->
                    definitionNode = new TreeNode(fieldDeclaration.toString(), makeId(fieldDeclaration), TreeNodeKind.FIELD)
                            .hideFromNavigation();
            case CallableDeclaration<?> callableDeclaration ->
                    definitionNode = new TreeNode(callableDeclaration.toString(), makeId(callableDeclaration), TreeNodeKind.METHOD)
                            .hideFromNavigation();
            case null, default -> {
                System.out.println("Unknown definition type: " + definition.getClass().getName());
                System.exit(-1);
                return;
            }
        }

        parentNode.addChild(definitionNode);

        final TreeNodeKind kind = definitionNode.getKind();

        // now we are ready to start visiting the various parts of the definition
        visitJavaDoc(definition, definitionNode);
        visitAnnotations(definition, isTypeDeclaration, isTypeDeclaration, definitionNode);

        // Add modifiers
        for (final Modifier modifier : ((NodeWithModifiers<?>)definition).getModifiers()) {
            definitionNode.addTopToken(KEYWORD, modifier.toString());
        }

        // for type declarations, add in if it is a class, annotation, enum, interface, etc
        if (definition instanceof TypeDeclaration<?> typeDeclaration) {
            definitionNode.addTopToken(KEYWORD, kind.getTypeDeclarationString()).addSpace();

            Token typeNameToken = new Token(TYPE_NAME, typeDeclaration.getNameAsString(), makeId(typeDeclaration));
            possiblyDeprecate(typeNameToken, typeDeclaration);

//            checkForCrossLanguageDefinitionId(typeNameToken, typeDeclaration);
            definitionNode.addTopToken(typeNameToken);
        }

        // Add type parameters for definition
        if (definition instanceof NodeWithTypeParameters<?> d) {
            visitTypeParameters(d.getTypeParameters(), definitionNode);
        }

        // Add type for definition
        visitType(definition, definitionNode, RETURN_TYPE);

        if (definition instanceof FieldDeclaration fieldDeclaration) {
            // For Fields - we add the field name and type
            visitDeclarationNameAndVariables(fieldDeclaration, definitionNode);
            definitionNode.addTopToken(PUNCTUATION, ";");
        } else if (definition instanceof CallableDeclaration<?> n) {
            // For Methods - Add name and parameters for definition
            visitDeclarationNameAndParameters(n, n.getParameters(), definitionNode);

            // Add throw exceptions for definition
            visitThrowException(n, definitionNode);
        } else if (definition instanceof TypeDeclaration<?> d) {
            // add in types that we are extending or implementing
            visitExtendsAndImplements((TypeDeclaration<?>) definition, definitionNode);

            // finish up with opening and closing brackets
            definitionNode.addSpace().addTopToken(PUNCTUATION, "{");
            definitionNode.addBottomToken(new Token(PUNCTUATION, "}"));

            // now process inner values (e.g. if it is a class, interface, enum, etc
            if (d.isEnumDeclaration()) {
                visitEnumEntries((EnumDeclaration) d, definitionNode);
            }

            // Get if the declaration is interface or not
            boolean isInterfaceDeclaration = isInterfaceType(d);

            // public custom annotation @interface's members
            if (d.isAnnotationDeclaration() && isPublicOrProtected(d.getAccessSpecifier())) {
                final AnnotationDeclaration annotationDeclaration = (AnnotationDeclaration) d;
                visitAnnotationMember(annotationDeclaration, definitionNode);
            }

            // get fields
            visitFields(isInterfaceDeclaration, d, definitionNode);

            // get Constructors
            final List<ConstructorDeclaration> constructors = d.getConstructors();
            if (constructors.isEmpty()) {
                // add default constructor if there is no constructor at all, except interface and enum
                if (!isInterfaceDeclaration && !d.isEnumDeclaration() && !d.isAnnotationDeclaration()) {
                    addDefaultConstructor(d, definitionNode);
                } else {
                    // skip and do nothing if there is no constructor in the interface.
                }
            } else {
                visitConstructorsOrMethods(d, isInterfaceDeclaration, true, constructors, definitionNode);
            }

            // get Methods
            visitConstructorsOrMethods(d, isInterfaceDeclaration, false, d.getMethods(), definitionNode);

            // get Inner classes
            d.getChildNodes()
                .stream()
                .filter(n -> n instanceof TypeDeclaration)
                .map(n -> (TypeDeclaration<?>) n)
                .forEach(innerType -> {
                    if (innerType.isEnumDeclaration() || innerType.isClassOrInterfaceDeclaration()) {
                        visitDefinition(innerType, definitionNode);
                    }
                });

            if (isInterfaceDeclaration) {
                if (d.getMembers().isEmpty()) {
                    // we have an empty interface declaration, it is probably a marker interface and we will leave a
                    // comment to that effect
                    definitionNode.addChild(TreeNode.createHiddenNode()
                            .addTopToken(COMMENT, "// This interface does not declare any API."));
                }
            }
        }
    }

    private void visitEnumEntries(EnumDeclaration enumDeclaration, TreeNode parentNode) {
        final NodeList<EnumConstantDeclaration> enumConstantDeclarations = enumDeclaration.getEntries();
        int size = enumConstantDeclarations.size();

        AtomicInteger counter = new AtomicInteger();

        enumConstantDeclarations.forEach(enumConstantDeclaration -> {
            TreeNode enumConstantNode = addChild(parentNode, enumConstantDeclaration.getNameAsString(), makeId(enumConstantDeclaration), TreeNodeKind.ENUM_CONSTANT);
            enumConstantNode.hideFromNavigation();

            visitJavaDoc(enumConstantDeclaration, enumConstantNode);

            // annotations
            visitAnnotations(enumConstantDeclaration, false, false, enumConstantNode);

            // create a unique id for enum constants by using the fully-qualified constant name
            // (package, enum name, and enum constant name)
            final String name = enumConstantDeclaration.getNameAsString();
            final String definitionId = makeId(enumConstantDeclaration);

            Token enumToken = new Token(ENUM_CONSTANT, name, definitionId);
            enumConstantNode.addTopToken(enumToken);
            possiblyDeprecate(enumToken, enumConstantDeclaration);

            // add tokens for comma-separated list of arguments
            int argumentsSize = enumConstantDeclaration.getArguments().size();
            if (argumentsSize > 0) {
                enumConstantNode.addTopToken(PUNCTUATION, "(");
                for (int i = 0, max = enumConstantDeclaration.getArguments().size(); i < max; i++) {
                    visitExpression(enumConstantDeclaration.getArguments().get(i), enumConstantNode);
                    if (i < max - 1) {
                        enumConstantNode.addTopToken(PUNCTUATION, ",").addSpace();
                    }
                }
                enumConstantNode.addTopToken(PUNCTUATION, ")");
            }

            if (counter.getAndIncrement() < size - 1) {
                enumConstantNode.addTopToken(PUNCTUATION, ",");
            } else {
                enumConstantNode.addTopToken(PUNCTUATION, ";");
            }
        });
    }

//
//        /*
//         * This method is used to add 'cross language definition id' to the token if it is defined in the
//         * apiview_properties.json file. This is used most commonly in conjunction with TypeSpec-generated libraries,
//         * so that we may review cross languages with some level of confidence that the types and methods are the same.
//         */
//        private void checkForCrossLanguageDefinitionId(Token typeNameToken, NodeWithSimpleName<?> node) {
//            Optional<String> fqn;
//            if (node instanceof TypeDeclaration) {
//                fqn = ((TypeDeclaration<?>) node).getFullyQualifiedName();
//            } else if (node instanceof CallableDeclaration) {
//                fqn = Optional.of(getNodeFullyQualifiedName((CallableDeclaration<?>) node));
//            } else {
//                fqn = Optional.empty();
//            }
//
//            fqn.flatMap(_fqn -> apiListing.getApiViewProperties().getCrossLanguageDefinitionId(_fqn))
//               .ifPresent(typeNameToken::setCrossLanguageDefinitionId);
//        }
//
    private void visitAnnotationMember(AnnotationDeclaration annotationDeclaration, final TreeNode parentNode) {
        // Member methods in the annotation declaration
        NodeList<BodyDeclaration<?>> annotationDeclarationMembers = annotationDeclaration.getMembers();
        for (BodyDeclaration<?> bodyDeclaration : annotationDeclarationMembers) {
            Optional<AnnotationMemberDeclaration> annotationMemberDeclarationOptional = bodyDeclaration.toAnnotationMemberDeclaration();
            if (annotationMemberDeclarationOptional.isEmpty()) {
                continue;
            }
            final AnnotationMemberDeclaration annotationMemberDeclaration = annotationMemberDeclarationOptional.get();

            TreeNode annotationMemberNode;
            parentNode.addChild(annotationMemberNode = new TreeNode(
                    annotationMemberDeclaration.getNameAsString(),
                    makeId(annotationMemberDeclaration),
                    TreeNodeKind.ANNOTATION));
            annotationMemberNode.hideFromNavigation();

            visitClassType(annotationMemberDeclaration.getType(), annotationMemberNode, RETURN_TYPE);
            annotationMemberNode.addSpace();

            annotationMemberNode.addTopToken(METHOD_NAME, annotationMemberDeclaration.getNameAsString(), makeId(annotationMemberDeclaration));
            annotationMemberNode.addTopToken(PUNCTUATION, "()");

            // default value
            final Optional<Expression> defaultValueOptional = annotationMemberDeclaration.getDefaultValue();
            if (defaultValueOptional.isPresent()) {
                annotationMemberNode.addSpace().addTopToken(KEYWORD, "default").addSpace();

                final Expression defaultValueExpr = defaultValueOptional.get();
                visitExpression(defaultValueExpr, annotationMemberNode);
//                final String value = defaultValueExpr.toString();
//                annotationMemberNode.addTopToken(KEYWORD, value);
            }
            annotationMemberNode.addTopToken(PUNCTUATION, ";");
        }
    }

    private void visitFields(final boolean isInterfaceDeclaration,
                             final TypeDeclaration<?> typeDeclaration,
                             final TreeNode parentNode) {
        final List<? extends FieldDeclaration> fieldDeclarations = typeDeclaration.getFields();

        for (FieldDeclaration fieldDeclaration : fieldDeclarations) {
            // By default , interface has public abstract methods if there is no access specifier declared
            if (isInterfaceDeclaration) {
                // no-op - we take all methods in the method
            } else if (isPrivateOrPackagePrivate(fieldDeclaration.getAccessSpecifier())) {
                // Skip if not public API
                continue;
            }

            visitDefinition(fieldDeclaration, parentNode);
        }
    }

    private void visitConstructorsOrMethods(final TypeDeclaration<?> typeDeclaration,
                                            final boolean isInterfaceDeclaration,
                                            final boolean isConstructor,
                                            final List<? extends CallableDeclaration<?>> callableDeclarations,
                                            final TreeNode parentNode) {
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

                parentNode.addChild(TreeNode.createHiddenNode()
                    .addTopToken(COMMENT, "// This class does not have any public constructors, and is not able to be instantiated using 'new'."));
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
                    parentNode.addChild(TreeNode.createHiddenNode().addTopToken(COMMENT, "// " + groupName + ":"));
                }
                group.forEach(callableDeclaration -> visitDefinition(callableDeclaration, parentNode));
            });
    }

    private void visitExtendsAndImplements(TypeDeclaration<?> typeDeclaration, TreeNode definitionNode) {
        BiConsumer<NodeList<ClassOrInterfaceType>, TokenKind> c = (nodeList, kind) -> {
            for (int i = 0, max = nodeList.size(); i < max; i++) {
                final ClassOrInterfaceType node = nodeList.get(i);
                visitType(node, definitionNode, kind);

                if (i < max - 1) {
                    definitionNode.addTopToken(PUNCTUATION, ",").addSpace();
                }
            }
        };

        // Extends a class
        if (typeDeclaration instanceof NodeWithExtends<?> d) {
            if (d.getExtendedTypes().isNonEmpty()) {
                definitionNode.addSpace().addTopToken(KEYWORD, "extends").addSpace();
                c.accept(d.getExtendedTypes(), EXTENDS_TYPE);
            }
        }

        // implements a class
        if (typeDeclaration instanceof NodeWithImplements<?> d) {
            if (d.getImplementedTypes().isNonEmpty()) {
                definitionNode.addSpace().addTopToken(KEYWORD, "implements").addSpace();
                c.accept(d.getImplementedTypes(), IMPLEMENTS_TYPE);
            }
        }
    }

    private void visitDeclarationNameAndVariables(FieldDeclaration fieldDeclaration, TreeNode definitionNode) {
        final NodeList<VariableDeclarator> variables = fieldDeclaration.getVariables();
        if (variables.size() > 1) {
            for (int i = 0; i < variables.size(); i++) {
                final VariableDeclarator variableDeclarator = variables.get(i);
                Token token = new Token(FIELD_NAME, variableDeclarator.getNameAsString(), makeId(variableDeclarator));
                definitionNode.addTopToken(token);
                possiblyDeprecate(token, fieldDeclaration);

                if (i < variables.size() - 1) {
                    definitionNode.addTopToken(PUNCTUATION, ",").addSpace();
                }
            }
        } else if (variables.size() == 1) {
            final VariableDeclarator variableDeclarator = variables.get(0);
            Token token = new Token(FIELD_NAME, variableDeclarator.getNameAsString(), makeId(variableDeclarator));
            definitionNode.addTopToken(token);
            possiblyDeprecate(token, fieldDeclaration);

            final Optional<Expression> variableDeclaratorOption = variableDeclarator.getInitializer();
            if (variableDeclaratorOption.isPresent()) {
                Expression e = variableDeclaratorOption.get();

                if (e.isObjectCreationExpr() && e.asObjectCreationExpr().getAnonymousClassBody().isPresent()) {
                    // no-op because we don't want to include all of the anonymous inner class details
                } else {
                    // we want to make string constants look like strings, so we need to look for them within the
                    // variable declarator
                    definitionNode.addSpace().addTopToken(PUNCTUATION, "=").addSpace();
                    visitExpression(e, definitionNode);
                }
            }
        }
    }

    private void visitExpression(Expression expression, TreeNode node) {
        visitExpression(expression, node, false);
    }

    private void visitExpression(Expression expression, TreeNode node, boolean condensed) {
        if (expression instanceof MethodCallExpr methodCallExpr) {
            node.addTopToken(METHOD_NAME, methodCallExpr.getNameAsString());
            node.addTopToken(PUNCTUATION, "(");
            NodeList<Expression> arguments = methodCallExpr.getArguments();
            for (int i = 0; i < arguments.size(); i++) {
                Expression argument = arguments.get(i);
                if (argument instanceof StringLiteralExpr) {
                    node.addTopToken(STRING_LITERAL, argument.toString());
                } else {
                    node.addTopToken(TEXT, argument.toString());
                }
                if (i < arguments.size() - 1) {
                    node.addTopToken(PUNCTUATION, ",").addSpace();
                }
            }
            node.addTopToken(PUNCTUATION, ")");
            return;
        } else if (expression instanceof StringLiteralExpr stringLiteralExpr) {
            node.addTopToken(STRING_LITERAL, stringLiteralExpr.toString());
            return;
        } else if (expression instanceof ArrayInitializerExpr arrayInitializerExpr) {
//            node.addTopToken(STRING_LITERAL, arrayInitializerExpr.toString());
            if (!condensed) {
                node.addTopToken(PUNCTUATION, "{ ");
            }
            for (int i = 0; i < arrayInitializerExpr.getChildNodes().size(); i++) {
                Node n = arrayInitializerExpr.getChildNodes().get(i);

                if (n instanceof Expression) {
                    visitExpression((Expression) n, node, condensed);
                } else {
                    node.addTopToken(TEXT, arrayInitializerExpr.toString()); // FIXME This was ANNOTATION_PARAMETER_VALUE
                }

                if (i < arrayInitializerExpr.getChildNodes().size() - 1) {
                    node.addTopToken(PUNCTUATION, ", ");
                }
            }
            if (!condensed) {
                node.addTopToken(PUNCTUATION, " }");
            }
            return;
        } else if (expression instanceof IntegerLiteralExpr ||
                   expression instanceof LongLiteralExpr ||
                   expression instanceof DoubleLiteralExpr) {
            node.addTopToken(NUMBER, expression.toString());
            return;
        } else if (expression instanceof FieldAccessExpr fieldAccessExpr) {
            visitExpression(fieldAccessExpr.getScope(), node);
            node.addTopToken(PUNCTUATION, ".")
                .addTopToken(FIELD_NAME, fieldAccessExpr.getNameAsString());
            return;
        } else if (expression instanceof BinaryExpr binaryExpr) {
            visitExpression(binaryExpr.getLeft(), node);
            node.addTopToken(TEXT, " " + binaryExpr.getOperator().asString() + " ");
            visitExpression(binaryExpr.getRight(), node);
            return;
        } else if (expression instanceof ClassExpr classExpr) {
            // lookup to see if the type is known about, if so, make it a link, otherwise leave it as text
            String typeName = classExpr.getChildNodes().getFirst().toString();
//            if (apiListing.getKnownTypes().containsKey(typeName)) {
            node.addTopToken(TYPE_NAME, typeName, null); // FIXME add ID
            return;
//            }
//        } else {
//            node.addTopToken(TEXT, expression.toString());
        } else if (expression instanceof NameExpr nameExpr) {
            node.addTopToken(TYPE_NAME, nameExpr.toString());
            return;
        } else if (expression instanceof BooleanLiteralExpr booleanLiteralExpr) {
            node.addTopToken(KEYWORD, booleanLiteralExpr.toString());
            return;
        } else if (expression instanceof ObjectCreationExpr) {
            node.addTopToken(KEYWORD, "new").addSpace()
                .addTopToken(TYPE_NAME, ((ObjectCreationExpr) expression).getTypeAsString())
                .addTopToken(PUNCTUATION, "(")
                .addTopToken(COMMENT, "/* Elided */")
                .addTopToken(PUNCTUATION, ")");
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
            node.addTopToken(TEXT, upperCase(value));
        } else {
            node.addTopToken(TEXT, value);
        }
    }

    private void visitAnnotations(final NodeWithAnnotations<?> nodeWithAnnotations,
                                  final boolean showAnnotationProperties,
                                  final boolean addNewline,
                                  final TreeNode methodNode) {
        Consumer<AnnotationExpr> consumer = annotation -> {
            // Check the annotation rules map for any overrides
            final String annotationName = annotation.getName().toString();
            AnnotationRule annotationRule = ANNOTATION_RULE_MAP.get(annotationName);

            AnnotationRendererModel model = new AnnotationRendererModel(
                    annotation, nodeWithAnnotations, annotationRule, showAnnotationProperties, addNewline);

            if (model.isHidden()) {
                return;
            }

            renderAnnotation(model, methodNode);

            if (model.isAddNewline()) {
                methodNode.addNewline();
            } else {
                methodNode.addTopToken(WHITESPACE, " ");
            }
        };

        nodeWithAnnotations.getAnnotations()
            .stream()
            .filter(annotationExpr -> !annotationExpr.getName().getIdentifier().startsWith("Json"))
            .sorted(Comparator.comparing(a -> a.getName().getIdentifier())) // we sort the annotations alphabetically
            .forEach(consumer);
    }

    private void renderAnnotation(AnnotationRendererModel m, TreeNode node) {
        final AnnotationExpr a = m.getAnnotation();
//        List<Token> tokens = new ArrayList<>();
        node.addTopToken(ANNOTATION_NAME, "@" + a.getNameAsString());
        if (m.isShowProperties()) {
            if (a instanceof NormalAnnotationExpr d) {
                node.addTopToken(PUNCTUATION, "(");
                NodeList<MemberValuePair> pairs = d.getPairs();
                for (int i = 0; i < pairs.size(); i++) {
                    MemberValuePair pair = pairs.get(i);

                    // If the pair is a boolean expression, and we are condensed, we only take the name.
                    // If we are not a boolean expression, and we are condensed, we only take the value.
                    // If we are not condensed, we take both.
                    final Expression valueExpr = pair.getValue();
                    if (m.isCondensed()) {
                        if (valueExpr.isBooleanLiteralExpr()) {
                            node.addTopToken(ANNOTATION_PARAMETER_NAME, upperCase(pair.getNameAsString()));
                        } else {
                            visitExpression(valueExpr, node, m.isCondensed());
                        }
                    } else {
                        node.addTopToken(ANNOTATION_PARAMETER_NAME, pair.getNameAsString());
                        node.addTopToken(PUNCTUATION, " = ");
                        visitExpression(valueExpr, node, m.isCondensed());
                    }

                    if (i < pairs.size() - 1) {
                        node.addTopToken(PUNCTUATION, ", ");
                    }
                }

                node.addTopToken(PUNCTUATION, ")");
            } else if (a instanceof SingleMemberAnnotationExpr d) {
                node.addTopToken(PUNCTUATION, "(");
                visitExpression(d.getMemberValue(), node, m.isCondensed());
                node.addTopToken(PUNCTUATION, ")");
            }
        }
    }

    private void visitDeclarationNameAndParameters(CallableDeclaration<?> callableDeclaration,
                                                   NodeList<Parameter> parameters,
                                                   TreeNode node) {
        // create an unique definition id
        String name = callableDeclaration.getNameAsString();
        final String definitionId = makeId(callableDeclaration);

        Token nameToken = new Token(METHOD_NAME, name, definitionId);
//            checkForCrossLanguageDefinitionId(nameToken, callableDeclaration);
        node.addTopToken(nameToken);
        possiblyDeprecate(nameToken, callableDeclaration);

        node.addTopToken(PUNCTUATION, "(");

        if (!parameters.isEmpty()) {
            for (int i = 0, max = parameters.size(); i < max; i++) {
                final Parameter parameter = parameters.get(i);
                visitType(parameter, node, PARAMETER_TYPE);
                node.addTopToken(WHITESPACE, " ");
                node.addTopToken(PARAMETER_NAME, parameter.getNameAsString());

                if (i < max - 1) {
                    node.addTopToken(PUNCTUATION, ",").addSpace();
                }
            }
        }

        // FIXME this is a bit hacky - it would be nice to have 'MethodRule' like we do for Annotations.
        // we want to special-case here for methods called `getLatest()` which are within a class that implements the
        // `ServiceVersion` interface. We want to add a comment to the method to indicate that it is a special method
        // that is used to get the latest version of a service.
        Node parentNode = callableDeclaration.getParentNode().orElse(null);
        if (callableDeclaration instanceof MethodDeclaration m) {
            if (callableDeclaration.getNameAsString().equals("getLatest")) {
                if (parentNode instanceof EnumDeclaration d) {
                    if (d.getImplementedTypes().stream().anyMatch(implementedType -> implementedType.getName().toString().equals("ServiceVersion"))) {
                        m.getBody().flatMap(blockStmt -> blockStmt.getStatements().stream()
                                    .filter(Statement::isReturnStmt)
                                    .findFirst())
                            .ifPresent(ret -> node.addTopToken(COMMENT, "// returns " + ret.getChildNodes().get(0)));
                    }
                }
            }
        }

        // close declaration
        node.addTopToken(PUNCTUATION, ")").addSpace();
    }

    private void visitTypeParameters(NodeList<TypeParameter> typeParameters, TreeNode node) {
        final int size = typeParameters.size();
        if (size == 0) {
            return;
        }
        node.addTopToken(PUNCTUATION, "<");
        for (int i = 0; i < size; i++) {
            final TypeParameter typeParameter = typeParameters.get(i);
            visitGenericTypeParameter(typeParameter, node);
            if (i != size - 1) {
                node.addTopToken(PUNCTUATION, ",").addSpace();
            }
        }
        node.addTopToken(PUNCTUATION, ">");
    }

    private void visitGenericTypeParameter(TypeParameter typeParameter, TreeNode node) {
        // set navigateToId
        node.addTopToken(new Token(TYPE_NAME, typeParameter.getNameAsString()));

        // get type bounds
        final NodeList<ClassOrInterfaceType> typeBounds = typeParameter.getTypeBound();
        final int size = typeBounds.size();
        if (size != 0) {
            node.addSpace().addTopToken(KEYWORD, "extends").addSpace();
            for (ClassOrInterfaceType typeBound : typeBounds) {
                visitType(typeBound, node, TYPE_NAME);
            }
        }
    }

    private void visitThrowException(NodeWithThrownExceptions<?> node, TreeNode methodNode) {
        final NodeList<ReferenceType> thrownExceptions = node.getThrownExceptions();
        if (thrownExceptions.isEmpty()) {
            return;
        }

        methodNode.addTopToken(KEYWORD, "throws").addSpace();
        commaSeparateList(methodNode, TYPE_NAME, thrownExceptions, e -> e.getElementType().toString());
        methodNode.addSpace();
    }

    private void visitType(Object type, TreeNode parentNode, TokenKind kind) {
        if (type instanceof Parameter d) {
            visitClassType(d.getType(), parentNode, kind);
            if (((Parameter) type).isVarArgs()) {
                parentNode.addTopToken(kind, "...");
            }
        } else if (type instanceof MethodDeclaration d) {
            visitClassType(d.getType(), parentNode, kind);
            parentNode.addSpace();
        } else if (type instanceof FieldDeclaration d) {
            visitClassType(d.getElementType(), parentNode, kind);
            parentNode.addSpace();
        } else if (type instanceof ClassOrInterfaceType d) {
            visitClassType(d, parentNode, kind);
        } else if (type instanceof AnnotationDeclaration ||
                   type instanceof ConstructorDeclaration ||
                   type instanceof ClassOrInterfaceDeclaration ||
                   type instanceof EnumDeclaration) {
            // no-op
        } else {
            System.err.println("Unknown type " + type + " of type " + type.getClass());
        }
    }

    private void visitClassType(Type type, TreeNode parentNode, TokenKind kind) {
        if (type.isPrimitiveType()) {
            parentNode.addTopToken(kind, type.asPrimitiveType().toString());
        } else if (type.isVoidType()) {
            parentNode.addTopToken(kind, "void");
        } else if (type.isReferenceType()) {
            // Array Type
            type.ifArrayType(arrayType -> {
                visitClassType(arrayType.getComponentType(), parentNode, kind);
                parentNode.addTopToken(kind, "[]");
            });
            // Class or Interface type
            type.ifClassOrInterfaceType(t -> visitTypeDFS(t, parentNode, kind));

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

    private void visitTypeDFS(Node node, TreeNode parentNode, TokenKind kind) {
        final List<Node> nodes = node.getChildNodes();
        final int childrenSize = nodes.size();
        // Recursion's base case: leaf node
        if (childrenSize <= 1) {
            if (node instanceof WildcardType d) {
                if (d.getExtendedType().isPresent()) {
                    parentNode.addTopToken(kind, "?").addSpace()
                              .addTopToken(KEYWORD, "extends").addSpace();
                    visitTypeDFS(d.getExtendedType().get(), parentNode, kind);
                } else if (d.getSuperType().isPresent()) {
                    parentNode.addTopToken(kind, "?").addSpace()
                              .addTopToken(KEYWORD, "super").addSpace();
                    visitTypeDFS(d.getSuperType().get(), parentNode, kind);
                } else {
                    parentNode.addTopToken(kind, "?");
                }
            } else {
                final String typeName = node.toString();
                final Token token = new Token(kind, typeName);
                parentNode.addTopToken(token);
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
                    parentNode.addTopToken(PUNCTUATION, ".");
                }
                visitTypeDFS(childNodes.get(i), parentNode, kind);
            }
            // Now, add "<" before adding the type arg nodes
            parentNode.addTopToken(PUNCTUATION, "<");
            for (int i = childNodes.size() - genericTypeArgsCount; i < childNodes.size(); i++) {
                if (i > childNodes.size() - genericTypeArgsCount) {
                    // Add a "," separator if there are more two or more type args
                    parentNode.addTopToken(PUNCTUATION, ",").addSpace();
                }
                visitTypeDFS(childNodes.get(i), parentNode, kind);
            }
            // Close the type args with ">"
            parentNode.addTopToken(PUNCTUATION, ">");
        } else {
            // No type args, all child nodes are just part of the fully qualified class name
            for (int i = 0; i < childNodes.size(); i++) {
                if (i > 0) {
                    parentNode.addTopToken(PUNCTUATION, ".");
                }
                visitTypeDFS(childNodes.get(i), parentNode, kind);
            }
        }
    }

    private void addDefaultConstructor(TypeDeclaration<?> typeDeclaration, TreeNode parentNode) {
        final String name = typeDeclaration.getNameAsString();
        final String definitionId = makeId(typeDeclaration.getNameAsString());

        parentNode.addChild(new TreeNode(null, makeId(typeDeclaration)+"_default_ctor", TreeNodeKind.METHOD)
            .hideFromNavigation()
            .addTopToken(KEYWORD, "public").addSpace()
            .addTopToken(METHOD_NAME, name, definitionId)
            .addTopToken(PUNCTUATION, "()"));
    }

    private void visitJavaDoc(BodyDeclaration<?> bodyDeclaration, TreeNode parentNode) {
        attemptToFindJavadocComment(bodyDeclaration).ifPresent(jd -> visitJavaDoc(jd, parentNode));
    }

    private void visitJavaDoc(JavadocComment javadoc, TreeNode parentNode) {
        String str = javadoc.toString();
        if (str == null || str.isEmpty()) {
            return;
        }

        Stream.of(str.split("\n")).forEach(line -> {
            // we want to wrap our javadocs so that they are easier to read, so we wrap at 120 chars
            MiscUtils.wrap(line, 120).forEach(line2 -> {
                if (line2.contains("&")) {
                    line2 = HtmlEscape.unescapeHtml(line2);
                }

                // convert http/s links to external clickable links
                Matcher urlMatch = MiscUtils.URL_MATCH.matcher(line2);
                int currentIndex = 0;
                while(urlMatch.find(currentIndex)) {
                    int start = urlMatch.start();
                    int end = urlMatch.end();

                    // if the current search index != start of match, there was text between two hyperlinks
                    if(currentIndex != start) {
                        String betweenValue = line2.substring(currentIndex, start);
                        parentNode.addTopToken(JAVADOC, betweenValue);
                    }

                    String matchedValue = line2.substring(start, end);
//                    addToken(new Token(EXTERNAL_LINK_START, matchedValue)); // TODO
                    parentNode.addTopToken(JAVADOC, matchedValue);
//                    addToken(new Token(EXTERNAL_LINK_END));
                    currentIndex = end;
                }
                // end of line will be anything between the end of the last found link, and the end of the string
                String finalValue = line2.substring(currentIndex);
                parentNode.addTopToken(JAVADOC, finalValue);

                parentNode.addNewline();
            });
        });
    }

    // Note: Returns the CHILD node that was added, not the parent node (which is what TreeNode.addChild does).
    private static TreeNode addChild(TreeNode parent, String name, String id, TreeNodeKind kind) {
        TreeNode child = new TreeNode(Objects.requireNonNull(name), Objects.requireNonNull(id), kind);
        parent.addChild(child);
        return child;
    }

    private static TreeNodeKind getTreeNodeKind(TypeDeclaration<?> typeDeclaration) {
        if (typeDeclaration.isClassOrInterfaceDeclaration()) {
            return ((ClassOrInterfaceDeclaration) typeDeclaration).isInterface() ? TreeNodeKind.INTERFACE : TreeNodeKind.CLASS;
        } else if (typeDeclaration.isEnumDeclaration()) {
            return TreeNodeKind.ENUM;
        } else if (typeDeclaration.isAnnotationDeclaration()) {
            return TreeNodeKind.ANNOTATION;
        } else {
            return TreeNodeKind.UNKNOWN;
        }
    }
}