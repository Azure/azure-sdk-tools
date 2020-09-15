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
import java.nio.file.FileSystems;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.ArrayList;
import java.util.Enumeration;
import java.util.List;
import java.util.jar.JarEntry;
import java.util.jar.JarFile;

import static com.fasterxml.jackson.databind.MapperFeature.*;
import static com.azure.tools.apiview.processor.model.TokenKind.*;

public class Main {

    // expected argument order:
    // [inputFiles] <outputDirectory>
    public static void main(String[] args) {
        if (args.length != 2) {
            System.out.println("Expected argument order: [comma-separated sources jarFiles] <outputFile>, e.g. /path/to/jarfile.jar ./temp/");
            System.exit(-1);
        }

        final String jarFiles = args[0];
        String[] jarFilesArray = jarFiles.split(",");

        final File outputDir = new File(args[1]);
        if (!outputDir.exists()) {
            outputDir.mkdirs();
        }

        System.out.println("Running with following configuration:");
        System.out.println("  Output directory: '" + outputDir + "'");

        for (String jarFile : jarFilesArray) {
            System.out.println("  Processing input .jar file: '" + jarFile + "'");

            final File file = new File(jarFile);
            if (!file.exists()) {
                System.out.println("Cannot find file '" + file + "'");
                System.exit(-1);
            }

            String jsonFileName = file.getName().substring(0, file.getName().length() - 4) + ".json";
            File outputFile = new File(outputDir, jsonFileName);
            processFile(file, outputFile);
        }
    }

    private static String getReviewName(File inputFile) {
        String artifactId = "";
        String version = "";

        // we will firstly try to get the artifact ID from the maven file inside the jar file...if it exists
        try (final JarFile jarFile = new JarFile(inputFile)) {
            final Enumeration<JarEntry> enumOfJar = jarFile.entries();
            while (enumOfJar.hasMoreElements()) {
                final JarEntry entry = enumOfJar.nextElement();
                final String fullPath = entry.getName();

                if (fullPath.startsWith("META-INF/maven") && fullPath.endsWith("pom.xml")) {
                    final InputStream jarIS = jarFile.getInputStream(entry);

                    // use xpath to get the artifact ID
                    DocumentBuilderFactory builderFactory = DocumentBuilderFactory.newInstance();
                    DocumentBuilder builder = builderFactory.newDocumentBuilder();
                    Document xmlDocument = builder.parse(jarIS);
                    XPath xPath = XPathFactory.newInstance().newXPath();

                    String artifactIdExpression = "/project/artifactId";
                    Node artifactIdNode = (Node) xPath.compile(artifactIdExpression).evaluate(xmlDocument, XPathConstants.NODE);
                    artifactId = artifactIdNode.getTextContent();

                    String versionExpression = "/project/version";
                    Node versionNode = (Node) xPath.compile(versionExpression).evaluate(xmlDocument, XPathConstants.NODE);
                    version = versionNode.getTextContent();
                }
            }
        } catch (IOException | ParserConfigurationException | SAXException | XPathExpressionException e) {
            e.printStackTrace();
        }

        if (artifactId == null || artifactId.isEmpty()) {
            // we failed to read it from the maven pom file, we will just take the file name without any extension
            final String filename = inputFile.getName();
            int i = 0;
            while (i < filename.length() && !Character.isDigit(filename.charAt(i))) {
                i++;
            }

            artifactId = filename.substring(0, i - 1);
            version = filename.substring(i, filename.indexOf("-sources.jar"));
        }

        final String reviewName = artifactId + " (version " + version + ")";
        System.out.println("  Using '" + reviewName + "' for the review name");

        return reviewName;
    }

    private static void processFile(File inputFile, File outputFile) {
        APIListing apiListing = new APIListing(getReviewName(inputFile));
        apiListing.setLanguage("Java");

        // empty tokens list that we will fill as we process each class file
        List<Token> tokens = new ArrayList<>();
        apiListing.setTokens(tokens);

        if (inputFile.getName().endsWith("-sources.jar")) {
            Analyser analyser = new ASTAnalyser(inputFile, apiListing);

            // Read all files within the jar file so that we can create a list of files to analyse
            List<Path> allFiles = new ArrayList<>();
            try {
                FileSystems.newFileSystem(inputFile.toPath(), Main.class.getClassLoader())
                        .getRootDirectories()
                        .forEach(root -> {
                            try {
                                Files.walk(root).forEach(allFiles::add);
                            } catch (IOException e) {
                                e.printStackTrace();
                                System.exit(-1);
                            }
                        });
            } catch (IOException e) {
                e.printStackTrace();
                System.exit(-1);
            }

            // Do the analysis
            analyser.analyse(allFiles);
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
        try {
            ObjectMapper objectMapper = new ObjectMapper();
            objectMapper
                    .disable(AUTO_DETECT_CREATORS, AUTO_DETECT_FIELDS, AUTO_DETECT_GETTERS, AUTO_DETECT_IS_GETTERS)
                    .setSerializationInclusion(JsonInclude.Include.NON_NULL)
                    .writerWithDefaultPrettyPrinter()
                    .writeValue(outputFile, apiListing);
        } catch (IOException e) {
            e.printStackTrace();
        }
    }

    // Debug method to easily print to console
    private void printTokensToConsole(APIListing apiListing) {
        apiListing.getTokens().stream().forEach(token -> {
            if (token.getKind() == NEW_LINE) {
                System.out.println();
            } else {
                System.out.print(token.getValue());
            }
        });
    }
}