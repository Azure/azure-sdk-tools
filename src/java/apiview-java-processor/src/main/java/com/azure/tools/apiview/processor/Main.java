package com.azure.tools.apiview.processor;

import com.azure.tools.apiview.processor.analysers.JavaASTAnalyser;
import com.azure.tools.apiview.processor.analysers.Analyser;
import com.azure.tools.apiview.processor.analysers.XMLASTAnalyser;
import com.azure.tools.apiview.processor.model.APIListing;
import com.azure.tools.apiview.processor.model.Diagnostic;
import com.azure.tools.apiview.processor.model.DiagnosticKind;
import com.azure.tools.apiview.processor.model.LanguageVariant;
import com.azure.tools.apiview.processor.model.Token;
import com.azure.tools.apiview.processor.model.maven.Pom;
import com.fasterxml.jackson.annotation.JsonInclude;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.databind.ObjectWriter;

import java.io.File;
import java.io.FileInputStream;
import java.io.IOException;
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

import static com.azure.tools.apiview.processor.model.TokenKind.LINE_ID_MARKER;
import static com.fasterxml.jackson.databind.MapperFeature.AUTO_DETECT_CREATORS;
import static com.fasterxml.jackson.databind.MapperFeature.AUTO_DETECT_FIELDS;
import static com.fasterxml.jackson.databind.MapperFeature.AUTO_DETECT_GETTERS;
import static com.fasterxml.jackson.databind.MapperFeature.AUTO_DETECT_IS_GETTERS;

public class Main {
    private static final ObjectWriter WRITER = new ObjectMapper()
        .disable(AUTO_DETECT_CREATORS, AUTO_DETECT_FIELDS, AUTO_DETECT_GETTERS, AUTO_DETECT_IS_GETTERS)
        .setSerializationInclusion(JsonInclude.Include.NON_NULL)
        .writerWithDefaultPrettyPrinter();

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
            if (!outputDir.mkdirs()) {
                System.out.printf("Failed to create output directory %s%n", outputDir);
            }
        }

        System.out.println("Running with following configuration:");
        System.out.printf("  Output directory: '%s'%n", outputDir);

        for (final String jarFile : jarFilesArray) {
            System.out.printf("  Processing input .jar file: '%s'%n", jarFile);

            final File file = new File(jarFile);
            if (!file.exists()) {
                System.out.printf("Cannot find file '%s'%n", file);
                System.exit(-1);
            }

            final String jsonFileName = file.getName().substring(0, file.getName().length() - 4) + ".json";
            final File outputFile = new File(outputDir, jsonFileName);
            processFile(file, outputFile);
        }
    }

    private static ReviewProperties getReviewProperties(File inputFile) {
        final ReviewProperties reviewProperties = new ReviewProperties();
        final String filename = inputFile.getName();
        int i = 0;
        while (i < filename.length() && !Character.isDigit(filename.charAt(i))) {
            i++;
        }

        String artifactId = filename.substring(0, i - 1);
        String packageVersion = filename.substring(i, filename.indexOf("-sources.jar"));

        // we will firstly try to get the artifact ID from the maven file inside the jar file...if it exists
        try (final JarFile jarFile = new JarFile(inputFile)) {
            final Enumeration<JarEntry> enumOfJar = jarFile.entries();
            while (enumOfJar.hasMoreElements()) {
                final JarEntry entry = enumOfJar.nextElement();
                final String fullPath = entry.getName();

                // use the pom.xml of this artifact only
                // shaded jars can contain a pom.xml file each for every shaded dependencies
                if (fullPath.startsWith("META-INF/maven") && fullPath.endsWith(artifactId + "/pom.xml")) {
                    reviewProperties.setMavenPom(new Pom(jarFile.getInputStream(entry)));
                }
            }
        } catch (IOException e) {
            e.printStackTrace();
        }

        // if we can't get the maven details out of the Jar file, we will just use the filename itself...
        if (reviewProperties.getMavenPom() == null) {
            // we failed to read it from the maven pom file, we will just take the file name without any extension
            reviewProperties.setMavenPom(new Pom("", artifactId, packageVersion, false));
        }

        return reviewProperties;
    }

    private static void processFile(final File inputFile, final File outputFile) throws IOException {
        final APIListing apiListing = new APIListing();

        // empty tokens list that we will fill as we process each class file
        final List<Token> tokens = new ArrayList<>();
        apiListing.setTokens(tokens);

        if (inputFile.getName().endsWith("-sources.jar")) {
            processJavaSourcesJar(inputFile, apiListing);
        } else if (inputFile.getName().endsWith(".xml")) {
            // We are *probably* looking at a Maven BOM / POM file, let's analyse it!
            processXmlFile(inputFile, apiListing);
        } else {
            apiListing.getTokens().add(new Token(LINE_ID_MARKER, "Error!", "error"));
            apiListing.addDiagnostic(new Diagnostic(
                DiagnosticKind.ERROR,
                "error",
                "Uploaded files should end with '-sources.jar' or '.xml', " +
                    "as the APIView tool only works with source jar files, not compiled jar files. The uploaded file " +
                    "that was submitted to APIView was named " + inputFile.getName()));
        }

        // Write out to the filesystem
        WRITER.writeValue(outputFile, apiListing);
    }

    private static void processJavaSourcesJar(File inputFile, APIListing apiListing) throws IOException {
        final ReviewProperties reviewProperties = getReviewProperties(inputFile);

        final String groupId = reviewProperties.getMavenPom().getGroupId();

        final String reviewName = reviewProperties.getMavenPom().getArtifactId()
                                      + " (version " + reviewProperties.getMavenPom().getVersion() + ")";
        System.out.println("  Using '" + reviewName + "' for the review name");

        final String packageName = (groupId.isEmpty() ? "" : groupId + ":") + reviewProperties.getMavenPom().getArtifactId();
        System.out.println("  Using '" + packageName + "' for the package name");

        System.out.println("  Using '" + reviewProperties.getMavenPom().getVersion() + "' for the package version");

        apiListing.setReviewName(reviewName);
        apiListing.setPackageName(packageName);
        apiListing.setPackageVersion(reviewProperties.getMavenPom().getVersion());
        apiListing.setLanguage("Java");
        apiListing.setMavenPom(reviewProperties.getMavenPom());

        if(groupId.contains("spring")) {
            apiListing.setLanguageVariant(LanguageVariant.SPRING);
        } else if(groupId.contains("android")) {
            apiListing.setLanguageVariant(LanguageVariant.ANDROID);
        } else {
            apiListing.setLanguageVariant(LanguageVariant.DEFAULT);
        }
        System.out.println("  Using '" + apiListing.getLanguageVariant() + "' for the language variant");

        final Analyser analyser = new JavaASTAnalyser(inputFile, apiListing);

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
    }

    private static void processXmlFile(File inputFile, APIListing apiListing) {
        final ReviewProperties reviewProperties = new ReviewProperties();

        try (FileInputStream fis = new FileInputStream(inputFile)) {
            String reviewName;
            String packageName;
            String packageVersion;
            String reviewLanguage;

            try {
                reviewProperties.setMavenPom(new Pom(fis));

                final String groupId = reviewProperties.getMavenPom().getGroupId();
                packageName = (groupId.isEmpty() ? "" : groupId + ":") + reviewProperties.getMavenPom().getArtifactId();
                packageVersion = reviewProperties.getMavenPom().getVersion();
                reviewName = reviewProperties.getMavenPom().getArtifactId() + " (version " + packageVersion + ")";
                reviewLanguage = "Java";

                apiListing.setMavenPom(reviewProperties.getMavenPom());
            } catch (IOException e) {
                // this is likely to not be a maven pom file
                reviewName = inputFile.getName();
                packageName = reviewName;
                packageVersion = "1.0.0";
                reviewLanguage = "Xml";
            }

            System.out.println("  Using '" + reviewName + "' for the review name");
            System.out.println("  Using '" + packageName + "' for the package name");
            System.out.println("  Using '" + packageVersion + "' for the package version");

            apiListing.setReviewName(reviewName);
            apiListing.setPackageName(packageName);
            apiListing.setPackageVersion(packageVersion);
            apiListing.setLanguage(reviewLanguage);

            final Analyser analyser = new XMLASTAnalyser(apiListing);
            analyser.analyse(inputFile.toPath());
        } catch (IOException e) {
            e.printStackTrace();
        }


    }

    private static class ReviewProperties {
        private Pom mavenPom;

        public void setMavenPom(Pom pom) {
            this.mavenPom = pom;
        }

        public Pom getMavenPom() {
            return mavenPom;
        }
    }
}
