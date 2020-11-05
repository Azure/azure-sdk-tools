package com.azure.tools.apiview.processor;

import com.azure.tools.apiview.processor.analysers.Analyser;
import com.azure.tools.apiview.processor.model.Diagnostic;
import com.azure.tools.apiview.processor.model.DiagnosticKind;
import com.fasterxml.jackson.annotation.JsonInclude;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.azure.tools.apiview.processor.analysers.ASTAnalyser;
import com.azure.tools.apiview.processor.model.APIListing;
import com.azure.tools.apiview.processor.model.Token;
import org.w3c.dom.Document;
import org.w3c.dom.Node;
import org.xml.sax.SAXException;

import javax.xml.parsers.DocumentBuilder;
import javax.xml.parsers.DocumentBuilderFactory;
import javax.xml.parsers.ParserConfigurationException;
import javax.xml.xpath.XPath;
import javax.xml.xpath.XPathConstants;
import javax.xml.xpath.XPathExpressionException;
import javax.xml.xpath.XPathFactory;
import java.io.File;
import java.io.IOException;
import java.io.InputStream;
import java.nio.file.FileSystem;
import java.nio.file.FileSystems;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.ArrayList;
import java.util.Enumeration;
import java.util.List;
import java.util.jar.JarEntry;
import java.util.jar.JarFile;
import java.util.stream.Stream;

import static com.fasterxml.jackson.databind.MapperFeature.*;
import static com.azure.tools.apiview.processor.model.TokenKind.*;

public class Main {

    // expected argument order:
    // [inputFiles] <outputDirectory>
    public static void main(String[] args) throws IOException {
        if (args.length != 2) {
            System.out.println("Expected argument order: [comma-separated sources jarFiles] <outputFile>, e.g. /path/to/jarfile.jar ./temp/");
            System.exit(-1);
        }

        final String jarFiles = args[0];
        final String[] jarFilesArray = jarFiles.split(",");

        final File outputDir = new File(args[1]);
        if (!outputDir.exists()) {
            outputDir.mkdirs();
        }

        System.out.println("Running with following configuration:");
        System.out.println("  Output directory: '" + outputDir + "'");

        for (final String jarFile : jarFilesArray) {
            System.out.println("  Processing input .jar file: '" + jarFile + "'");

            final File file = new File(jarFile);
            if (!file.exists()) {
                System.out.println("Cannot find file '" + file + "'");
                System.exit(-1);
            }

            final String jsonFileName = file.getName().substring(0, file.getName().length() - 4) + ".json";
            final File outputFile = new File(outputDir, jsonFileName);
            processFile(file, outputFile);
        }
    }

    private static ReviewProperties getReviewProperties(File inputFile) {
        final ReviewProperties reviewProperties = new ReviewProperties();

        // we will firstly try to get the artifact ID from the maven file inside the jar file...if it exists
        try (final JarFile jarFile = new JarFile(inputFile)) {
            final Enumeration<JarEntry> enumOfJar = jarFile.entries();
            while (enumOfJar.hasMoreElements()) {
                final JarEntry entry = enumOfJar.nextElement();
                final String fullPath = entry.getName();

                if (fullPath.startsWith("META-INF/maven") && fullPath.endsWith("pom.xml")) {
                    final InputStream jarIS = jarFile.getInputStream(entry);

                    // use xpath to get the artifact ID
                    final DocumentBuilderFactory builderFactory = DocumentBuilderFactory.newInstance();
                    final DocumentBuilder builder = builderFactory.newDocumentBuilder();
                    final Document xmlDocument = builder.parse(jarIS);
                    final XPath xPath = XPathFactory.newInstance().newXPath();

                    final String groupIdExpression = "/project/groupId";
                    final Node groupIdNode = (Node) xPath.compile(groupIdExpression).evaluate(xmlDocument, XPathConstants.NODE);
                    reviewProperties.setMavenGroupId(groupIdNode.getTextContent());

                    final String artifactIdExpression = "/project/artifactId";
                    final Node artifactIdNode = (Node) xPath.compile(artifactIdExpression).evaluate(xmlDocument, XPathConstants.NODE);
                    reviewProperties.setMavenArtifactId(artifactIdNode.getTextContent());

                    final String versionExpression = "/project/version";
                    final Node versionNode = (Node) xPath.compile(versionExpression).evaluate(xmlDocument, XPathConstants.NODE);
                    reviewProperties.setPackageVersion(versionNode.getTextContent());
                }
            }
        } catch (IOException | ParserConfigurationException | SAXException | XPathExpressionException e) {
            e.printStackTrace();
        }

        // if we can't get the maven details out of the Jar file, we will just use the filename itself...
        if (reviewProperties.getMavenArtifactId() == null || reviewProperties.getMavenArtifactId().isEmpty()) {
            // we failed to read it from the maven pom file, we will just take the file name without any extension
            final String filename = inputFile.getName();
            int i = 0;
            while (i < filename.length() && !Character.isDigit(filename.charAt(i))) {
                i++;
            }

            reviewProperties.setMavenArtifactId(filename.substring(0, i - 1));
            reviewProperties.setPackageVersion(filename.substring(i, filename.indexOf("-sources.jar")));
        }

        return reviewProperties;
    }

    private static void processFile(final File inputFile, final File outputFile) throws IOException {
        final ReviewProperties reviewProperties = getReviewProperties(inputFile);

        final String reviewName = reviewProperties.getMavenArtifactId() + " (version " + reviewProperties.getPackageVersion() + ")";
        System.out.println("  Using '" + reviewName + "' for the review name");

        final String packageName = reviewProperties.getMavenGroupId() + ":" + reviewProperties.getMavenArtifactId();
        System.out.println("  Using '" + packageName + "' for the package name");

        System.out.println("  Using '" + reviewProperties.getPackageVersion() + "' for the package version");

        final APIListing apiListing = new APIListing(reviewName);
        apiListing.setPackageName(packageName);
        apiListing.setPackageVersion(reviewProperties.getPackageVersion());
        apiListing.setLanguage("Java");

        // empty tokens list that we will fill as we process each class file
        final List<Token> tokens = new ArrayList<>();
        apiListing.setTokens(tokens);

        if (inputFile.getName().endsWith("-sources.jar")) {
            final Analyser analyser = new ASTAnalyser(inputFile, apiListing);

            // Read all files within the jar file so that we can create a list of files to analyse
            final List<Path> allFiles = new ArrayList<>();
            try (FileSystem fs = FileSystems.newFileSystem(inputFile.toPath(), Main.class.getClassLoader())) {
                fs.getRootDirectories().forEach(root -> {
                    try (Stream<Path> paths = Files.walk(root)) {
                        paths.forEach(allFiles::add);
                    } catch (IOException e) {
                        e.printStackTrace();
                        System.exit(-1);
                    }
                });

                // Do the analysis while the filesystem is still represented in memory
                analyser.analyse(allFiles);
            }


        } else {
            apiListing.getTokens().add(new Token(LINE_ID_MARKER, "Error!", "error"));
            apiListing.addDiagnostic(new Diagnostic(
                    DiagnosticKind.ERROR,
                    "error",
                    "Uploaded files should end with '-sources.jar', " +
                    "as the APIView tool only works with source jar files, not compiled jar files. The uploaded file " +
                    "that was submitted to APIView was named " + inputFile.getName()));
        }

        // Write out to the filesystem
        new ObjectMapper()
            .disable(AUTO_DETECT_CREATORS, AUTO_DETECT_FIELDS, AUTO_DETECT_GETTERS, AUTO_DETECT_IS_GETTERS)
            .setSerializationInclusion(JsonInclude.Include.NON_NULL)
            .writerWithDefaultPrettyPrinter()
            .writeValue(outputFile, apiListing);
    }

    private static class ReviewProperties {
        private String name;
        private String mavenGroupId;
        private String mavenArtifactId;
        private String packageVersion;

        public String getName() {
            return name;
        }

        public void setName(final String name) {
            this.name = name;
        }

        public String getMavenGroupId() {
            return mavenGroupId;
        }

        public void setMavenGroupId(final String mavenGroupId) {
            this.mavenGroupId = mavenGroupId;
        }

        public String getMavenArtifactId() {
            return mavenArtifactId;
        }

        public void setMavenArtifactId(final String mavenArtifactId) {
            this.mavenArtifactId = mavenArtifactId;
        }

        public String getPackageVersion() {
            return packageVersion;
        }

        public void setPackageVersion(final String packageVersion) {
            this.packageVersion = packageVersion;
        }
    }
}