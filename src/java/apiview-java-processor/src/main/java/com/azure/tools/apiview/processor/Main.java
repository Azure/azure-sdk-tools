package com.azure.tools.apiview.processor;

import com.azure.json.JsonProviders;
import com.azure.json.JsonReader;
import com.azure.json.JsonWriter;
import com.azure.tools.apiview.processor.analysers.Analyser;
import com.azure.tools.apiview.processor.analysers.JavaASTAnalyser;
import com.azure.tools.apiview.processor.model.APIListing;
import com.azure.tools.apiview.processor.model.ApiViewProperties;
import com.azure.tools.apiview.processor.model.Language;
import com.azure.tools.apiview.processor.model.LanguageVariant;
import com.azure.tools.apiview.processor.model.maven.Pom;

import java.io.File;
import java.io.IOException;
import java.nio.file.FileSystem;
import java.nio.file.FileSystems;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.Enumeration;
import java.util.List;
import java.util.jar.JarEntry;
import java.util.jar.JarFile;
import java.util.stream.Stream;

public class Main {
    // expected argument order:
    // [inputFiles] <outputDirectory>
    public static void main(String[] args) {
        if (args.length != 2) {
            System.out.println("Expected argument order: [comma-separated sources jarFiles] <outputFile>, e.g. /path/to/jarfile.jar ./temp/");
            System.exit(-1);
        }

        final String jarFiles = args[0];
        final String[] jarFilesArray = jarFiles.split(",");

        final File outputDir = new File(args[1]);

        System.out.println("Running with following configuration:");
        System.out.printf("  Output directory: '%s'%n", outputDir);

        Arrays.stream(jarFilesArray).forEach(jarFile -> run(new File(jarFile), outputDir));
    }

    /**
     * Runs APIView parser and returns the output file path.
     */
    public static File run(File jarFile, File outputDir) {
        System.out.printf("  Processing input .jar file: '%s'%n", jarFile);

        if (!jarFile.exists()) {
            System.out.printf("Cannot find file '%s'%n", jarFile);
            System.exit(-1);
        }

        if (!outputDir.exists()) {
            if (!outputDir.mkdirs()) {
                System.out.printf("Failed to create output directory %s%n", outputDir);
                System.exit(-1);
            }
        }

        final String jsonFileName = jarFile.getName().substring(0, jarFile.getName().length() - 4) + ".json";
        final File outputFile = new File(outputDir, jsonFileName);
        processFile(jarFile, outputFile);
        return outputFile;
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

    private static void processFile(final File inputFile, final File outputFile) {
        final APIListing apiListing = new APIListing();

        if (inputFile.getName().endsWith("-sources.jar")) {
            processJavaSourcesJar(inputFile, apiListing);
        } else {
//            apiListing.getTokens().add(new Token(LINE_ID_MARKER, "Error!", "error"));
//            apiListing.addDiagnostic(new Diagnostic(
//                DiagnosticKind.ERROR,
//                "error",
//                "Uploaded files should end with '-sources.jar' or '.xml', " +
//                    "as the APIView tool only works with source jar files, not compiled jar files. The uploaded file " +
//                    "that was submitted to APIView was named " + inputFile.getName()));
        }

        try {
            // Write out to the filesystem, make the file if it doesn't exist
            if (!outputFile.exists()) {
                if (!outputFile.createNewFile()) {
                    System.out.printf("Failed to create output file %s%n", outputFile);
                }
            }
            try (JsonWriter jsonWriter = JsonProviders.createWriter(Files.newBufferedWriter(outputFile.toPath()))) {
                apiListing.toJson(jsonWriter);
            }
        } catch (IOException e) {
            e.printStackTrace();
        }
    }

    private static void processJavaSourcesJar(File inputFile, APIListing apiListing) {
        final ReviewProperties reviewProperties = getReviewProperties(inputFile);

        final String groupId = reviewProperties.getMavenPom().getGroupId();
        final String artifactId = reviewProperties.getMavenPom().getArtifactId();

        final String reviewName = artifactId + " (version " + reviewProperties.getMavenPom().getVersion() + ")";
        System.out.println("  Using '" + reviewName + "' for the review name");

        final String packageName = (groupId.isEmpty() ? "" : groupId + ":") + artifactId;
        System.out.println("  Using '" + packageName + "' for the package name");

        System.out.println("  Using '" + reviewProperties.getMavenPom().getVersion() + "' for the package version");

        apiListing.setReviewName(reviewName);
        apiListing.setPackageName(packageName);
        apiListing.setPackageVersion(reviewProperties.getMavenPom().getVersion());
        apiListing.setLanguage(Language.JAVA);
        apiListing.setMavenPom(reviewProperties.getMavenPom());

        if(groupId.contains("spring")) {
            apiListing.setLanguageVariant(LanguageVariant.SPRING);
        } else if(groupId.contains("android")) {
            apiListing.setLanguageVariant(LanguageVariant.ANDROID);
        } else {
            apiListing.setLanguageVariant(LanguageVariant.DEFAULT);
        }
        System.out.println("  Using '" + apiListing.getLanguageVariant() + "' for the language variant");

        // Read all files within the jar file so that we can create a list of files to analyse
        final List<Path> allFiles = new ArrayList<>();
        try (FileSystem fs = FileSystems.newFileSystem(inputFile.toPath(), Main.class.getClassLoader())) {
            tryParseApiViewProperties(fs, apiListing, artifactId);

            fs.getRootDirectories().forEach(root -> {
                try (Stream<Path> paths = Files.walk(root)) {
                    paths.forEach(allFiles::add);
                } catch (IOException e) {
                    e.printStackTrace();
                    System.exit(-1);
                }
            });

            // Do the analysis while the filesystem is still represented in memory
            final Analyser analyser = new JavaASTAnalyser(apiListing);
            analyser.analyse(allFiles);
        } catch (Exception e) {
            e.printStackTrace();
        }
    }

    /**
     * Attempts to process the {@code apiview_properties.json} file in the jar file, if it exists.
     * <p>
     * If the file was found and successfully parsed as {@link ApiViewProperties}, it is set on the {@link APIListing}
     * object.
     *
     * @param fs the {@link FileSystem} representing the jar file
     * @param apiListing the {@link APIListing} object to set the {@link ApiViewProperties} on
     * @param artifactId the artifact ID of the jar file
     */
    private static void tryParseApiViewProperties(FileSystem fs, APIListing apiListing, String artifactId) {
        // the filename is [<artifactid>_]apiview_properties.json
        String artifactName = (artifactId != null && !artifactId.isEmpty()) ? (artifactId + "_") : "";
        String filePath = "/META-INF/" + artifactName + "apiview_properties.json";
        Path apiviewPropertiesPath = fs.getPath(filePath);
        if (!Files.exists(apiviewPropertiesPath)) {
            System.out.println("  No apiview_properties.json file found in jar file - continuing...");
            return;
        }

        try {
            // we eagerly load the apiview_properties.json file into an ApiViewProperties object, so that it can
            // be used throughout the analysis process, as required
            try (JsonReader reader = JsonProviders.createReader(Files.readAllBytes(apiviewPropertiesPath))) {
                ApiViewProperties properties = ApiViewProperties.fromJson(reader);
                apiListing.setApiViewProperties(properties);
                System.out.println("  Found apiview_properties.json file in jar file");
                System.out.println("    - Found " + properties.getCrossLanguageDefinitionIds().size()
                    + " cross-language definition IDs");
            }
        } catch (IOException e) {
            System.out.println("  ERROR: Unable to parse apiview_properties.json file in jar file - continuing...");
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
