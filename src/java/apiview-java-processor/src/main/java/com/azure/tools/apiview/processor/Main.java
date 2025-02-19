package com.azure.tools.apiview.processor;

import com.azure.tools.apiview.processor.analysers.Analyser;
import com.azure.tools.apiview.processor.analysers.JavaASTAnalyser;
import com.azure.tools.apiview.processor.analysers.models.Constants;
import com.azure.tools.apiview.processor.model.APIListing;
import com.azure.tools.apiview.processor.model.ApiViewProperties;
import com.azure.tools.apiview.processor.model.Language;
import com.azure.tools.apiview.processor.model.LanguageVariant;
import com.azure.tools.apiview.processor.model.maven.Pom;
import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.networknt.schema.*;

import java.io.*;
import java.net.URISyntaxException;
import java.net.URL;
import java.nio.file.FileSystem;
import java.nio.file.FileSystems;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.*;
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
     * Runs APIView parser and returns the output file path as an array. The first value in the array is the
     * JSON file. If there are multiple outputs (i.e. gzipping is enabled), the second value in the array is the
     * gzipped file.
     */
    public static File[] run(File jarFile, File outputDir) {
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

        final String outputFileName = jarFile.getName().substring(0, jarFile.getName().length() - 4) + ".json";
        final Optional<APIListing> apiListing = processFile(jarFile);

        if (apiListing.isPresent()) {
            File[] files = new File[Constants.GZIP_OUTPUT ? 2 : 1];
            files[0] = new File(outputDir, outputFileName);
            apiListing.get().toFile(files[0], false);
            System.out.println("  Output written to file: " + files[0]);

            if (Constants.PRETTY_PRINT_JSON) {
                try {
                    String json = new String(Files.readAllBytes(files[0].toPath()));
                    ObjectMapper mapper = new ObjectMapper();
                    Object jsonObject = mapper.readValue(json, Object.class);
                    mapper.writerWithDefaultPrettyPrinter().writeValue(files[0], jsonObject);
                } catch (IOException e) {
                    throw new RuntimeException(e);
                }
            }

            if (Constants.VALIDATE_JSON_SCHEMA) {
                System.out.println("  Validating the generated JSON file against the schema...");
                try {
                    JsonSchemaFactory factory = JsonSchemaFactory.getInstance(SpecVersion.VersionFlag.V7);
                    JsonSchema schema = factory.getSchema(new URL(Constants.APIVIEW_JSON_SCHEMA).toURI());

                    JsonNode jsonNode = new ObjectMapper().readTree(files[0]);
                    schema.initializeValidators();
                    Set<ValidationMessage> validationMessages = schema.validate(jsonNode);
                    if (validationMessages.isEmpty()) {
                        System.out.println("    Validation passed.");
                    } else {
                        System.out.println("    Validation failed. Errors:");
                        validationMessages.forEach(msg -> System.out.println("      " + msg.getMessage()));
                    }
                } catch (IOException e) {
                    throw new RuntimeException(e);
                } catch (URISyntaxException e) {
                    throw new RuntimeException(e);
                }
            }

            if (Constants.GZIP_OUTPUT) {
                files[1] = new File(outputDir, outputFileName + ".tgz");
                apiListing.get().toFile(files[1], true);
                System.out.println("  Output written to file: " + files[1]);
            }

            return files;
        }

        return new File[] { };
    }

    private static Optional<APIListing> processFile(final File inputFile) {
        final APIListing apiListing = new APIListing();

        if (inputFile.getName().endsWith("-sources.jar")) {
            processJavaSourcesJar(inputFile, apiListing);
            return Optional.of(apiListing);
        }

        return Optional.empty();
    }

    private static void processJavaSourcesJar(File inputFile, APIListing apiListing) {
        final Pom mavenPom = Pom.fromSourcesJarFile(inputFile);
        final String groupId = mavenPom.getGroupId();
        final String artifactId = mavenPom.getArtifactId();

        final String packageName = (groupId.isEmpty() ? "" : groupId + ":") + artifactId;
        System.out.println("  Using '" + packageName + "' for the package name");

        System.out.println("  Using '" + mavenPom.getVersion() + "' for the package version");

        apiListing.setPackageName(packageName);
        apiListing.setPackageVersion(mavenPom.getVersion());
        apiListing.setLanguage(Language.JAVA);
        apiListing.setMavenPom(mavenPom);

        if (groupId.contains("spring")) {
            apiListing.setLanguageVariant(LanguageVariant.SPRING);
        } else if (groupId.contains("android")) {
            apiListing.setLanguageVariant(LanguageVariant.ANDROID);
        } else {
            apiListing.setLanguageVariant(LanguageVariant.DEFAULT);
        }
        System.out.println("  Using '" + apiListing.getLanguageVariant() + "' for the language variant");

        // Read all files within the jar file so that we can create a list of files to analyse
        final List<Path> allFiles = new ArrayList<>();
        try (FileSystem fs = FileSystems.newFileSystem(inputFile.toPath(), Main.class.getClassLoader())) {
            ApiViewProperties.fromSourcesJarFile(fs, mavenPom).ifPresent(apiListing::setApiViewProperties);

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
}
