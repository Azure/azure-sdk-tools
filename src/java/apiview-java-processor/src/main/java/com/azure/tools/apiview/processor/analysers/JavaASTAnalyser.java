package com.azure.tools.apiview.processor.analysers;

import com.azure.tools.apiview.processor.analysers.models.AnnotationRendererModel;
import com.azure.tools.apiview.processor.analysers.models.AnnotationRule;
import com.azure.tools.apiview.processor.analysers.util.MiscUtils;
import com.azure.tools.apiview.processor.diagnostics.Diagnostics;
import com.azure.tools.apiview.processor.model.*;
import com.azure.tools.apiview.processor.model.maven.Dependency;
import com.azure.tools.apiview.processor.model.maven.Pom;
import com.github.javaparser.JavaParser;
import com.github.javaparser.JavaParserAdapter;
import com.github.javaparser.ParserConfiguration;
import com.github.javaparser.TokenRange;
import com.github.javaparser.ast.*;
import com.github.javaparser.ast.body.*;
import com.github.javaparser.ast.comments.Comment;
import com.github.javaparser.ast.comments.JavadocComment;
import com.github.javaparser.ast.expr.*;
import com.github.javaparser.ast.modules.ModuleDeclaration;
import com.github.javaparser.ast.nodeTypes.*;
import com.github.javaparser.ast.type.ClassOrInterfaceType;
import com.github.javaparser.ast.type.ReferenceType;
import com.github.javaparser.ast.type.Type;
import com.github.javaparser.ast.type.TypeParameter;
import com.github.javaparser.ast.visitor.VoidVisitorAdapter;
import org.unbescape.html.HtmlEscape;

import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.ArrayList;
import java.util.Comparator;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.Optional;
import java.util.TreeMap;
import java.util.concurrent.atomic.AtomicInteger;
import java.util.function.BiConsumer;
import java.util.function.Consumer;
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

    private static final boolean SHOW_JAVADOC = true;

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
        final String name = apiListing.getMavenPom().getArtifactId() + " (version " + apiListing.getMavenPom().getVersion() + ")";
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
         *
         * Then build a map of all known types and package names and a map of package names to navigation items.
         *
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

    private class ClassOrInterfaceVisitor extends VoidVisitorAdapter<TreeNode> {
        @Override
        public void visit(CompilationUnit compilationUnit, TreeNode parentNode) {
            compilationUnit.getModule().ifPresent(JavaASTAnalyser.this::visitModuleDeclaration);

            NodeList<TypeDeclaration<?>> types = compilationUnit.getTypes();
            for (final TypeDeclaration<?> typeDeclaration : types) {
                visitDefinition(typeDeclaration, parentNode);
            }

            diagnostic.scanIndividual(compilationUnit, apiListing);
        }
    }

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
            .ifPresent(scanElement -> {
                if (scanElement.compilationUnit.getPackageDeclaration().isPresent()) {
                    PackageDeclaration packageDeclaration = scanElement.compilationUnit.getPackageDeclaration().get();
                    if (packageDeclaration.getComment().isPresent()) {
                        Comment comment = packageDeclaration.getComment().get();
                        if (comment.isJavadocComment()) {
                            visitJavaDoc(comment.asJavadocComment(), packageNode);
                        }
                    }
                }
            });

        packageNode.addTopToken(KEYWORD, "package").addSpace();

        if (packageName.isEmpty()) {
            packageNode.hideFromNavigation().addTopToken(TEXT, "<root package>");
            this.rootPackageNode = packageNode;
        } else {
            packageNode.addTopToken(TYPE_NAME, packageName, packageName);
        }
        packageNode.addSpace().addTopToken(PUNCTUATION, "{");

        scanElements.stream()
            .filter(scanElement -> scanElement.elementType == ScanElementType.CLASS)
            .map(scanElement -> (ScanClass) scanElement)
            .sorted(Comparator.comparing(e -> e.primaryTypeName))
            .forEach(scanClass -> processSingleFile(packageNode, scanClass));

        packageNode.addBottomToken(PUNCTUATION, "}");
    }

    private void processSingleFile(final TreeNode parentNode, ScanClass scanClass) {
        final String path = scanClass.path.toString();
        final String artifactId = apiListing.getMavenPom().getArtifactId();
        if (path.endsWith("/pom.xml")) {
            // We only tokenise the maven pom related to the library, and not any other shaded maven poms
            if (path.endsWith(artifactId + "/pom.xml")) {
                // we want to represent the pom.xml file in short form
                tokeniseMavenPom(apiListing.getMavenPom());
            }
        } else {
            new ClassOrInterfaceVisitor().visit(scanClass.compilationUnit, parentNode);
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



    /************************************************************************************************
     *
     * Analysis implementation - Maven POM support
     *
     **********************************************************************************************/

    private void tokeniseMavenPom(Pom mavenPom) {
        TreeNode mavenNode = addChild(rootPackageNode, MAVEN_KEY, MAVEN_KEY, TreeNodeKind.MAVEN);

        mavenNode.addTopToken(KEYWORD, "maven", MAVEN_KEY)
                .addSpace()
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
            configurationNode.addTopToken(KEYWORD, "configuration")
                    .hideFromNavigation()
                    .addSpace()
                    .addTopToken(PUNCTUATION, "{")
                    .addBottomToken(PUNCTUATION, "}");

            if (showCheckStyle) {
                tokeniseKeyValue(configurationNode, "checkstyle-excludes", mavenPom.getCheckstyleExcludes(), "");
            }
            if (showJacoco) {
                TreeNode jacocoNode = addChild(configurationNode, "jacoco", "jacoco", TreeNodeKind.MAVEN);
                jacocoNode.addTopToken(KEYWORD, "jacoco")
                        .hideFromNavigation()
                        .addSpace()
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
                .addTopToken(KEYWORD, "dependencies")
                .addSpace()
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
                                .addTopToken(TEXT, gav, gav));
                    }
                });
//
//            addToken(new Token(SKIP_DIFF_END));
    }

    private void tokeniseKeyValue(TreeNode parentNode, String key, Object value, String linkPrefix) {
        parentNode.addChild(TreeNode.createHiddenNode()
                .addTopToken(KEYWORD, key)
                .addTopToken(PUNCTUATION, ":")
                .addSpace()
                .addTopToken(MiscUtils.tokeniseKeyValue(key, value, linkPrefix)));
    }



    /************************************************************************************************
     *
     * Analysis implementation - Module declaration support
     *
     **********************************************************************************************/

    private void visitModuleDeclaration(ModuleDeclaration moduleDeclaration) {
        TreeNode moduleNode = addChild(rootPackageNode, "module-info", MODULE_INFO_KEY, TreeNodeKind.MODULE_INFO);
        moduleNode.addProperty(PROPERTY_MODULE_NAME, moduleDeclaration.getNameAsString());

        moduleNode.addTopToken(KEYWORD, "module")
            .addSpace()
            .addTopToken(TYPE_NAME, moduleDeclaration.getNameAsString(), MODULE_INFO_KEY)
            .addSpace()
            .addTopToken(PUNCTUATION, "{")
            .addBottomToken(PUNCTUATION, "}");

        // Sometimes an exports or opens statement is conditional, so we need to handle that case
        // in a single location here, to remove duplication.
        BiConsumer<TreeNode, NodeList<Name>> conditionalExportsToOrOpensToConsumer = (node, names) -> {
            if (!names.isEmpty()) {
                node.addSpace()
                    .addTopToken(KEYWORD, "to")
                    .addSpace();

                for (int i = 0; i < names.size(); i++) {
                    node.addTopToken(TYPE_NAME, names.get(i).toString());

                    if (i < names.size() - 1) {
                        node.addTopToken(PUNCTUATION, ",").addSpace();
                    }
                }
            }
        };

        moduleDeclaration.getDirectives().forEach(moduleDirective -> {
            moduleDirective.ifModuleRequiresStmt(d -> {
                TreeNode moduleChildNode;
                String id = makeId(MODULE_INFO_KEY + "-requires-" + d.getNameAsString());
                moduleNode.addChild(moduleChildNode = new TreeNode(PROPERTY_MODULE_REQUIRES, id, TreeNodeKind.MODULE_REQUIRES)
                    .hideFromNavigation()
                    .addTopToken(KEYWORD, "requires")
                    .addSpace());

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
                    .addTopToken(KEYWORD, "exports")
                    .addSpace()
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
                    .addTopToken(KEYWORD, "opens")
                    .addSpace()
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
                    .addTopToken(KEYWORD, "uses")
                    .addSpace()
                    .addTopToken(TYPE_NAME, d.getNameAsString())
                    .addTopToken(PUNCTUATION, ";"));
            });

            moduleDirective.ifModuleProvidesStmt(d -> {
                TreeNode moduleChildNode;
                String id = makeId(MODULE_INFO_KEY + "-provides-" + d.getNameAsString());
                moduleNode.addChild(moduleChildNode = new TreeNode("provides", id, TreeNodeKind.MODULE_PROVIDES)
                    .hideFromNavigation()
                    .addTopToken(KEYWORD, "provides")
                    .addSpace()
                    .addTopToken(TYPE_NAME, d.getNameAsString())
                    .addSpace()
                    .addTopToken(KEYWORD, "with")
                    .addSpace());

                NodeList<Name> names = d.getWith();
                for (int i = 0; i < names.size(); i++) {
                    moduleChildNode.addTopToken(TYPE_NAME, names.get(i).toString());

                    if (i < names.size() - 1) {
                        moduleChildNode.addTopToken(PUNCTUATION, ",");
                    }
                }

                moduleChildNode.addTopToken(PUNCTUATION, ";");
            });
        });
    }



    /************************************************************************************************
     *
     * Analysis implementation - Standard Java support
     *
     **********************************************************************************************/

    // This method is for constructors, fields, methods, etc.
    private void visitDefinition(BodyDeclaration<?> definition, TreeNode parentNode) {
        // firstly, we need to make a definition node and attach it to its parent node. The kind of definitionNode we
        // make depends on the kind of definition we are processing.
        final TreeNode definitionNode;

        boolean isTypeDeclaration = false;
        if (definition instanceof TypeDeclaration) {
            TypeDeclaration<?> typeDeclaration = (TypeDeclaration<?>) definition;
            // Skip if the class is private or package-private, unless it is a nested type defined inside a public interface
            if (!isTypeAPublicAPI(typeDeclaration)) {
                return;
            }

            definitionNode = new TreeNode(typeDeclaration.getNameAsString(), makeId(typeDeclaration), getTreeNodeKind(typeDeclaration));
            isTypeDeclaration = true;
        } else if (definition instanceof FieldDeclaration) {
            FieldDeclaration fieldDeclaration = (FieldDeclaration) definition;
            definitionNode = new TreeNode(fieldDeclaration.toString(), makeId(fieldDeclaration), TreeNodeKind.FIELD)
                    .hideFromNavigation();
        } else if (definition instanceof CallableDeclaration) {
            CallableDeclaration<?> callableDeclaration = (CallableDeclaration<?>) definition;
            definitionNode = new TreeNode(callableDeclaration.toString(), makeId(callableDeclaration), TreeNodeKind.METHOD)
                    .hideFromNavigation();
        } else {
            System.out.println("Unknown definition type: " + definition.getClass().getName());
            System.exit(-1);
            return;
        }

        parentNode.addChild(definitionNode);

        final TreeNodeKind kind = definitionNode.getKind();
        visitJavaDoc(definition, definitionNode);

        // Add annotation for definition
        final boolean showAnnotationProperties = isTypeDeclaration;
        final boolean addNewline = isTypeDeclaration;
        visitAnnotations(definition, showAnnotationProperties, addNewline, definitionNode);

        // Add modifiers for definition
        for (final Modifier modifier : ((NodeWithModifiers<?>)definition).getModifiers()) {
            definitionNode.addTopToken(KEYWORD, modifier.toString());
        }

        // for type declarations, add in if it is a class, annotation, enum, interface, etc
        if (definition instanceof TypeDeclaration) {
            TypeDeclaration<?> typeDeclaration = (TypeDeclaration<?>) definition;
            definitionNode.addTopToken(KEYWORD, kind.getTypeDeclarationString()).addSpace();

            Token typeNameToken = new Token(TYPE_NAME, typeDeclaration.getNameAsString(), makeId(typeDeclaration));
//            checkForCrossLanguageDefinitionId(typeNameToken, typeDeclaration);
            definitionNode.addTopToken(typeNameToken);
        }

        // Add type parameters for definition
        if (definition instanceof NodeWithTypeParameters) {
            visitTypeParameters(((NodeWithTypeParameters<?>)definition).getTypeParameters(), definitionNode);
        }

        // Add type for definition
        visitType(definition, definitionNode);

        // For Fields - we add the field name and type
        if (definition instanceof FieldDeclaration) {
            NodeWithVariables<?> nodeWithVariables = (NodeWithVariables<?>) definition;
            visitDeclarationNameAndVariables(nodeWithVariables.getVariables(), definitionNode);
        }

        // For Methods - Add name and parameters for definition
        if (definition instanceof CallableDeclaration) {
            CallableDeclaration<?> n = (CallableDeclaration<?>) definition;
            visitDeclarationNameAndParameters(n, n.getParameters(), definitionNode);
        }

        // Add throw exceptions for definition
        if (definition instanceof NodeWithThrownExceptions) {
            visitThrowException((NodeWithThrownExceptions<?>) definition, definitionNode);
        }

        // close out with semi-colon if it is a field
        if (definition instanceof FieldDeclaration) {
            definitionNode.addTopToken(PUNCTUATION, ";");
        }

        if (definition instanceof TypeDeclaration) {
            // add in types that we are extending or implementing
            visitExtendsAndImplements((TypeDeclaration<?>) definition, definitionNode);

            // finish up with opening and closing brackets
            definitionNode.addSpace().addTopToken(PUNCTUATION, "{");
            definitionNode.addBottomToken(new Token(PUNCTUATION, "}"));

            // now process inner values (e.g. if it is a class, interface, enum, etc
            TypeDeclaration<?> typeDeclaration = (TypeDeclaration<?>) definition;
            if (typeDeclaration.isEnumDeclaration()) {
                visitEnumEntries((EnumDeclaration) typeDeclaration, definitionNode);
            }

            // Get if the declaration is interface or not
            boolean isInterfaceDeclaration = isInterfaceType(typeDeclaration);

            // public custom annotation @interface's members
            if (typeDeclaration.isAnnotationDeclaration() && isPublicOrProtected(typeDeclaration.getAccessSpecifier())) {
                final AnnotationDeclaration annotationDeclaration = (AnnotationDeclaration) typeDeclaration;
                visitAnnotationMember(annotationDeclaration, definitionNode);
            }

            // get fields
            visitFields(isInterfaceDeclaration, typeDeclaration, definitionNode);

            // get Constructors
            final List<ConstructorDeclaration> constructors = typeDeclaration.getConstructors();
            if (constructors.isEmpty()) {
                // add default constructor if there is no constructor at all, except interface and enum
                if (!isInterfaceDeclaration && !typeDeclaration.isEnumDeclaration() && !typeDeclaration.isAnnotationDeclaration()) {
                    addDefaultConstructor(typeDeclaration, definitionNode);
                } else {
                    // skip and do nothing if there is no constructor in the interface.
                }
            } else {
                visitConstructorsOrMethods(typeDeclaration, isInterfaceDeclaration, true, constructors, definitionNode);
            }

            // get Methods
            visitConstructorsOrMethods(typeDeclaration, isInterfaceDeclaration, false, typeDeclaration.getMethods(), definitionNode);

            // get Inner classes
            typeDeclaration.getChildNodes()
                .stream()
                .filter(n -> n instanceof TypeDeclaration)
                .map(n -> (TypeDeclaration<?>) n)
                .forEach(innerType -> {
                    if (innerType.isEnumDeclaration() || innerType.isClassOrInterfaceDeclaration()) {
                        visitDefinition(innerType, definitionNode);
                    }
                });

            if (isInterfaceDeclaration) {
                if (typeDeclaration.getMembers().isEmpty()) {
                    // we have an empty interface declaration, it is probably a marker interface and we will leave a
                    // comment to that effect
                    definitionNode.addChild(TreeNode.createHiddenNode()
                            .addTopToken(COMMENT, "// This interface does not declare any API."));
                }
            }
        }
    }

//    private void visitClassOrInterfaceOrEnumDeclaration(TypeDeclaration<?> typeDeclaration, TreeNode parentNode) {
//        // Skip if the class is private or package-private, unless it is a nested type defined inside a public interface
//        if (!isTypeAPublicAPI(typeDeclaration)) {
//            return;
//        }
//
//        final String className = typeDeclaration.getNameAsString();
//        final String classId = makeId(typeDeclaration);
//        final TreeNodeKind treeNodeKind = getTreeNodeKind(typeDeclaration);
//
//        TreeNode definitionNode;
//        parentNode.addChild(definitionNode = new TreeNode(className, makeId(classId), treeNodeKind));
//        visitDefinition(typeDeclaration, definitionNode);
//    }

//    private void visitAnnotationAnnotations(TypeDeclaration<?> typeDeclaration) {
//        // public custom annotation @interface's annotations
//        if (typeDeclaration.isAnnotationDeclaration() && isPublicOrProtected(typeDeclaration.getAccessSpecifier())) {
//            final AnnotationDeclaration annotationDeclaration = (AnnotationDeclaration) typeDeclaration;
//
//            // Annotations on top of AnnotationDeclaration class, for example
//            // @Retention(RUNTIME)
//            // @Target(PARAMETER)
//            // public @interface BodyParam {}
//            final NodeList<AnnotationExpr> annotations = annotationDeclaration.getAnnotations();
//            for (AnnotationExpr annotation : annotations) {
//                final Optional<TokenRange> tokenRange = annotation.getTokenRange();
//                if (!tokenRange.isPresent()) {
//                    continue;
//                }
//                final TokenRange annotationTokenRange = tokenRange.get();
//                // TODO: could be more specified instead of string
//                final String name = annotationTokenRange.toString();
//                currentClassNode.addTopToken(KEYWORD, name);
//                currentClassNode.addNewline();
//            }
//        }
//    }

    private void visitEnumEntries(EnumDeclaration enumDeclaration, TreeNode parentNode) {
        final NodeList<EnumConstantDeclaration> enumConstantDeclarations = enumDeclaration.getEntries();
        int size = enumConstantDeclarations.size();

        AtomicInteger counter = new AtomicInteger();

        enumConstantDeclarations.forEach(enumConstantDeclaration -> {
            TreeNode enumConstantNode = addChild(parentNode,
                    enumConstantDeclaration.getNameAsString(),
                    makeId(enumConstantDeclaration),
                    TreeNodeKind.ENUM_CONSTANT);
            enumConstantNode.hideFromNavigation();

            visitJavaDoc(enumConstantDeclaration, enumConstantNode);

            // annotations
            visitAnnotations(enumConstantDeclaration, false, false, enumConstantNode);

            // create a unique id for enum constants by using the fully-qualified constant name
            // (package, enum name, and enum constant name)
            final String name = enumConstantDeclaration.getNameAsString();
            final String definitionId = makeId(enumConstantDeclaration);
            final boolean isDeprecated = enumConstantDeclaration.isAnnotationPresent("Deprecated");

            Token enumToken = new Token(MEMBER_NAME, name, definitionId);
            enumConstantNode.addTopToken(enumToken);

            if (isDeprecated) {
                enumToken.addRenderClass("Deprecated");
            }

            enumConstantDeclaration.getArguments().forEach(expression -> {
                enumConstantNode.addTopToken(PUNCTUATION, "(");
                enumConstantNode.addTopToken(TEXT, expression.toString());
                enumConstantNode.addTopToken(PUNCTUATION, ")");
            });

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
            Optional<AnnotationMemberDeclaration> annotationMemberDeclarationOptional
                = bodyDeclaration.toAnnotationMemberDeclaration();
            if (!annotationMemberDeclarationOptional.isPresent()) {
                continue;
            }
            final AnnotationMemberDeclaration annotationMemberDeclaration = annotationMemberDeclarationOptional.get();

            TreeNode annotationMemberNode;
            parentNode.addChild(annotationMemberNode = new TreeNode(
                    annotationMemberDeclaration.getNameAsString(),
                    makeId(annotationMemberDeclaration),
                    TreeNodeKind.ANNOTATION));
            annotationMemberNode.hideFromNavigation();

            visitClassType(annotationMemberDeclaration.getType(), annotationMemberNode);
            annotationMemberNode.addSpace();

            annotationMemberNode.addTopToken(MEMBER_NAME, annotationMemberDeclaration.getNameAsString(), makeId(annotationMemberDeclaration));
            annotationMemberNode.addTopToken(PUNCTUATION, "()");

            // default value
            final Optional<Expression> defaultValueOptional = annotationMemberDeclaration.getDefaultValue();
            if (defaultValueOptional.isPresent()) {
                annotationMemberNode.addSpace().addTopToken(KEYWORD, "default").addSpace();

                final Expression defaultValueExpr = defaultValueOptional.get();
                final String value = defaultValueExpr.toString();
                annotationMemberNode.addTopToken(KEYWORD, value);
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
                    parentNode.addChild(TreeNode.createHiddenNode()
                            .addTopToken(COMMENT, "// " + groupName + ":"));
                }

                group.forEach(callableDeclaration -> visitDefinition(callableDeclaration, parentNode));
            });
    }

    private void visitExtendsAndImplements(TypeDeclaration<?> typeDeclaration, TreeNode definitionNode) {
        // Extends a class
        if (typeDeclaration instanceof NodeWithExtends) {
            final NodeList<ClassOrInterfaceType> extendedTypes = ((NodeWithExtends<?>) typeDeclaration).getExtendedTypes();
            if (!extendedTypes.isEmpty()) {
                definitionNode.addSpace().addTopToken(KEYWORD, "extends");

                if (extendedTypes.isNonEmpty()) {
                    for (int i = 0, max = extendedTypes.size(); i < max; i++) {
                        final ClassOrInterfaceType extendedType = extendedTypes.get(i);
                        visitType(extendedType, definitionNode);

                        if (i < max - 1) {
                            definitionNode.addTopToken(PUNCTUATION, ",");
                        }
                    }
                }
            }
        }

        // implements a class
        if (typeDeclaration instanceof NodeWithImplements) {
            final NodeList<ClassOrInterfaceType> implementedTypes = ((NodeWithImplements<?>) typeDeclaration).getImplementedTypes();
            if (!implementedTypes.isEmpty()) {
                definitionNode.addSpace().addTopToken(KEYWORD, "implements");

                for (int i = 0, max = implementedTypes.size(); i < max; i++) {
                    final ClassOrInterfaceType implementedType = implementedTypes.get(i);
                    visitType(implementedType, definitionNode);

                    if (i < max - 1) {
                        definitionNode.addTopToken(PUNCTUATION, ",").addSpace();
                    }
                }
            }
        }
    }

    private void visitDeclarationNameAndVariables(NodeList<VariableDeclarator> variables, TreeNode definitionNode) {
        if (variables.size() > 1) {
            for (int i = 0; i < variables.size(); i++) {
                final VariableDeclarator variableDeclarator = variables.get(i);
                final String name = variableDeclarator.getNameAsString();
                final String definitionId = makeId(variableDeclarator);
                definitionNode.addTopToken(MEMBER_NAME, name, definitionId);

                if (i < variables.size() - 1) {
                    definitionNode.addTopToken(PUNCTUATION, ",").addSpace();
                }
            }
        } else if (variables.size() == 1) {
            final VariableDeclarator variableDeclarator = variables.get(0);
            final String name = variableDeclarator.getNameAsString();
            final String definitionId = makeId(variableDeclarator);
            definitionNode.addTopToken(MEMBER_NAME, name, definitionId);

            final Optional<Expression> variableDeclaratorOption = variableDeclarator.getInitializer();
            if (variableDeclaratorOption.isPresent()) {
                Expression e = variableDeclaratorOption.get();
                if (e.isObjectCreationExpr() && e.asObjectCreationExpr()
                        .getAnonymousClassBody()
                        .isPresent()) {
                    // no-op because we don't want to include all of the anonymous inner class details
                } else {
                    definitionNode
                            .addSpace()
                            .addTopToken(PUNCTUATION, "=")
                            .addSpace()
                            .addTopToken(TEXT, variableDeclaratorOption.get().toString());
                }
            }
        }
    }

    private void visitAnnotations(final NodeWithAnnotations<?> nodeWithAnnotations,
                                  final boolean showAnnotationProperties,
                                  final boolean addNewline,
                                  final TreeNode methodNode) {

//        // don't show the annotations on an annotation declaration
//        if (nodeWithAnnotations instanceof AnnotationDeclaration) {
//            return;
//        }

        Consumer<AnnotationExpr> consumer = annotation -> {
            // Check the annotation rules map for any overrides
            final String annotationName = annotation.getName().toString();
            AnnotationRule annotationRule = ANNOTATION_RULE_MAP.get(annotationName);

            AnnotationRendererModel model = new AnnotationRendererModel(
                    annotation, nodeWithAnnotations, annotationRule, showAnnotationProperties, addNewline);

            if (model.isHidden()) {
                return;
            }

//                if (model.isAddNewline()) {
//                    addNewline(methodNode);
//                    addToken(makeWhitespace());
//                }

            renderAnnotation(model).forEach(methodNode::addTopToken);

            if (model.isAddNewline()) {
                methodNode.addNewline();
            } else {
                methodNode.addTopToken(WHITESPACE, " ");
            }
        };

        nodeWithAnnotations.getAnnotations()
            .stream()
            .filter(annotationExpr -> {
                String id = annotationExpr.getName().getIdentifier();
                return !id.startsWith("Json");
            })
            .sorted(Comparator.comparing(a -> a.getName().getIdentifier())) // we sort the annotations alphabetically
            .forEach(consumer);
    }

    private List<Token> renderAnnotation(AnnotationRendererModel m) {
        final AnnotationExpr a = m.getAnnotation();
        List<Token> tokens = new ArrayList<>();
        tokens.add(new Token(TYPE_NAME, "@" + a.getNameAsString()));
        if (m.isShowProperties()) {
            if (a instanceof NormalAnnotationExpr) {
                tokens.add(new Token(PUNCTUATION, "("));
                NodeList<MemberValuePair> pairs = ((NormalAnnotationExpr) a).getPairs();
                for (int i = 0; i < pairs.size(); i++) {
                    MemberValuePair pair = pairs.get(i);

                    // If the pair is a boolean expression, and we are condensed, we only take the name.
                    // If we are not a boolean expression, and we are condensed, we only take the value.
                    // If we are not condensed, we take both.
                    final Expression valueExpr = pair.getValue();
                    if (m.isCondensed()) {
                        if (valueExpr.isBooleanLiteralExpr()) {
                            tokens.add(new Token(MEMBER_NAME, upperCase(pair.getNameAsString())));
                        } else {
                            processAnnotationValueExpression(valueExpr, m.isCondensed(), tokens);
                        }
                    } else {
                        tokens.add(new Token(MEMBER_NAME, pair.getNameAsString()));
                        tokens.add(new Token(PUNCTUATION, " = "));

                        processAnnotationValueExpression(valueExpr, m.isCondensed(), tokens);
                    }

                    if (i < pairs.size() - 1) {
                        tokens.add(new Token(PUNCTUATION, ", "));
                    }
                }

                tokens.add(new Token(PUNCTUATION, ")"));
            } else if (a instanceof SingleMemberAnnotationExpr) {
                tokens.add(new Token(PUNCTUATION, "("));
                processAnnotationValueExpression(((SingleMemberAnnotationExpr) a).getMemberValue(), m.isCondensed(), tokens);
                tokens.add(new Token(PUNCTUATION, ")"));
            }
        }
        return tokens;
    }

    private void processAnnotationValueExpression(final Expression valueExpr, final boolean condensed, final List<Token> tokens) {
        if (valueExpr.isClassExpr()) {
            // lookup to see if the type is known about, if so, make it a link, otherwise leave it as text
            String typeName = valueExpr.getChildNodes().get(0).toString();
            if (apiListing.getKnownTypes().containsKey(typeName)) {
                final Token token = new Token(TYPE_NAME, typeName);
                tokens.add(token);
                return;
            }
        } else if (valueExpr.isArrayInitializerExpr()) {
            if (!condensed) {
                tokens.add(new Token(PUNCTUATION, "{ "));
            }
            for (int i = 0; i < valueExpr.getChildNodes().size(); i++) {
                Node n = valueExpr.getChildNodes().get(i);

                if (n instanceof Expression) {
                    processAnnotationValueExpression((Expression) n, condensed, tokens);
                } else {
                    tokens.add(new Token(TEXT, valueExpr.toString()));
                }

                if (i < valueExpr.getChildNodes().size() - 1) {
                    tokens.add(new Token(PUNCTUATION, ", "));
                }
            }
            if (!condensed) {
                tokens.add(new Token(PUNCTUATION, " }"));
            }
            return;
        }

        // if we fall through to here, just treat it as a string.
        // If we are in condensed mode, we strip off everything before the last period
        String value = valueExpr.toString();
        if (condensed) {
            int lastPeriod = value.lastIndexOf('.');
            if (lastPeriod != -1) {
                value = value.substring(lastPeriod + 1);
            }
            tokens.add(new Token(TEXT, upperCase(value)));
        } else {
            tokens.add(new Token(TEXT, value));
        }
    }

    private void visitDeclarationNameAndParameters(CallableDeclaration<?> callableDeclaration,
                                                   NodeList<Parameter> parameters,
                                                   TreeNode node) {

        boolean isDeprecated = callableDeclaration.isAnnotationPresent("Deprecated");

        // create an unique definition id
        String name = callableDeclaration.getNameAsString();
        final String definitionId = makeId(callableDeclaration);

//            if (isDeprecated) {
//                addToken(new Token(DEPRECATED_RANGE_START));
//            }

        Token nameToken = new Token(MEMBER_NAME, name, definitionId);
//            checkForCrossLanguageDefinitionId(nameToken, callableDeclaration);
        node.addTopToken(nameToken);

        if (isDeprecated) {
            nameToken.addRenderClass("Deprecated");
        }

        node.addTopToken(PUNCTUATION, "(");

        if (!parameters.isEmpty()) {
            for (int i = 0, max = parameters.size(); i < max; i++) {
                final Parameter parameter = parameters.get(i);
                visitType(parameter, node);
                node.addTopToken(WHITESPACE, " ");
                node.addTopToken(TEXT, parameter.getNameAsString());

                if (i < max - 1) {
                    node.addTopToken(PUNCTUATION, ",").addSpace();
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
            node.addSpace()
                .addTopToken(KEYWORD, "extends")
                .addSpace();
            for (ClassOrInterfaceType typeBound : typeBounds) {
                visitType(typeBound, node);
            }
        }
    }

    private void visitThrowException(NodeWithThrownExceptions<?> node, TreeNode methodNode) {
        final NodeList<ReferenceType> thrownExceptions = node.getThrownExceptions();
        if (thrownExceptions.isEmpty()) {
            return;
        }

        methodNode.addTopToken(KEYWORD, "throws").addSpace();

        for (int i = 0, max = thrownExceptions.size(); i < max; i++) {
            final String exceptionName = thrownExceptions.get(i).getElementType().toString();
            final Token throwsToken = new Token(TYPE_NAME, exceptionName);

            methodNode.addTopToken(throwsToken);
            if (i < max - 1) {
                methodNode.addTopToken(PUNCTUATION, ",").addSpace();
            }
        }
        methodNode.addSpace();
    }

    private void visitType(Object type, TreeNode parentNode) {
        if (type instanceof Parameter) {
            visitClassType(((NodeWithType) type).getType(), parentNode);
            if (((Parameter) type).isVarArgs()) {
                parentNode.addTopToken(PUNCTUATION, "...");
            }
        } else if (type instanceof MethodDeclaration) {
            visitClassType(((MethodDeclaration) type).getType(), parentNode);
            parentNode.addSpace();
        } else if (type instanceof FieldDeclaration) {
            visitClassType(((FieldDeclaration) type).getElementType(), parentNode);
            parentNode.addSpace();
        } else if (type instanceof ClassOrInterfaceType) {
            visitClassType(((Type) type), parentNode);
        } else if (type instanceof AnnotationDeclaration ||
                   type instanceof ConstructorDeclaration ||
                   type instanceof ClassOrInterfaceDeclaration ||
                   type instanceof EnumDeclaration) {
            // no-op
        } else {
            System.err.println("Unknown type " + type + " of type " + type.getClass());
        }
    }

    private void visitClassType(Type type, TreeNode parentNode) {
        if (type.isPrimitiveType()) {
            parentNode.addTopToken(TYPE_NAME, type.asPrimitiveType().toString());
        } else if (type.isVoidType()) {
            parentNode.addTopToken(TYPE_NAME, "void");
        } else if (type.isReferenceType()) {
            // Array Type
            type.ifArrayType(arrayType -> {
                visitClassType(arrayType.getComponentType(), parentNode);
                parentNode.addTopToken(PUNCTUATION, "[]");
            });
            // Class or Interface type
            type.ifClassOrInterfaceType(t -> visitTypeDFS(t, parentNode));

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

    private void visitTypeDFS(Node node, TreeNode parentNode) {
        final List<Node> nodes = node.getChildNodes();
        final int childrenSize = nodes.size();
        // Recursion's base case: leaf node
        if (childrenSize <= 1) {
            final String typeName = node.toString();
            final Token token = new Token(TYPE_NAME, typeName);
            parentNode.addTopToken(token);
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
                visitTypeDFS(childNodes.get(i), parentNode);
            }
            // Now, add "<" before adding the type arg nodes
            parentNode.addTopToken(PUNCTUATION, "<");
            for (int i = childNodes.size() - genericTypeArgsCount; i < childNodes.size(); i++) {
                if (i > childNodes.size() - genericTypeArgsCount) {
                    // Add a "," separator if there are more two or more type args
                    parentNode.addTopToken(PUNCTUATION, ",").addSpace();
                }
                visitTypeDFS(childNodes.get(i), parentNode);
            }
            // Close the type args with ">"
            parentNode.addTopToken(PUNCTUATION, ">");
        } else {
            // No type args, all child nodes are just part of the fully qualified class name
            for (int i = 0; i < childNodes.size(); i++) {
                if (i > 0) {
                    parentNode.addTopToken(PUNCTUATION, ".");
                }
                visitTypeDFS(childNodes.get(i), parentNode);
            }
        }
    }

    private void addDefaultConstructor(TypeDeclaration<?> typeDeclaration, TreeNode parentNode) {
        final String name = typeDeclaration.getNameAsString();
        final String definitionId = makeId(typeDeclaration.getNameAsString());

        parentNode.addChild(new TreeNode(null, makeId(typeDeclaration)+"_default_ctor", TreeNodeKind.METHOD)
            .hideFromNavigation()
            .addTopToken(KEYWORD, "public").addSpace()
            .addTopToken(MEMBER_NAME, name, definitionId)
            .addTopToken(PUNCTUATION, "(")
            .addTopToken(PUNCTUATION, ")"));
    }

    private void visitJavaDoc(BodyDeclaration<?> bodyDeclaration, TreeNode parentNode) {
        attemptToFindJavadocComment(bodyDeclaration).ifPresent(jd -> visitJavaDoc(jd, parentNode));
    }

    private void visitJavaDoc(JavadocComment javadoc, TreeNode parentNode) {
        if (!SHOW_JAVADOC) {
            return;
        }

        // The default toString() implementation changed after version 3.16.1. Previous implementation
        // always used a print configuration local to toString() method. The new implementation uses instance level
        // configuration that can be mutated by other calls like getDeclarationAsString() called from 'makeId()'
        // (ASTUtils).
        // The updated configuration from getDeclarationAsString removes the comment option and hence the toString
        // returns an empty string now. So, here we are using the toString overload that takes a PrintConfiguration
        // to get the old behavior.
        String str = javadoc.toString();
        if (str == null || str.isEmpty()) {
            return;
        }

        //        addToken(new Token(DOCUMENTATION_RANGE_START));

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
//        addToken(new Token(DOCUMENTATION_RANGE_END));
    }

    // Note: Returns the CHILD node that was added, not the parent node (which is what TreeNode.addChild does).
    private static TreeNode addChild(TreeNode parent, String name, String id, TreeNodeKind kind) {
        TreeNode child = new TreeNode(name, id, kind);
        parent.addChild(child);
        return child;
    }

    private static TreeNodeKind getTreeNodeKind(TypeDeclaration<?> typeDeclaration) {
        if (typeDeclaration.isClassOrInterfaceDeclaration()) {
            if (((ClassOrInterfaceDeclaration) typeDeclaration).isInterface()) {
                return TreeNodeKind.INTERFACE;
            } else {
                return TreeNodeKind.CLASS;
            }
        } else if (typeDeclaration.isEnumDeclaration()) {
            return TreeNodeKind.ENUM;
        } else if (typeDeclaration.isAnnotationDeclaration()) {
            return TreeNodeKind.ANNOTATION;
        } else {
            return TreeNodeKind.UNKNOWN;
        }
    }
}
