package com.azure.tools.apiview.processor;

import com.azure.tools.apiview.processor.analysers.Analyser;
import com.azure.tools.apiview.processor.analysers.JavaASTAnalyser;
import com.azure.tools.apiview.processor.analysers.models.Constants;
import com.azure.tools.apiview.processor.model.APIListing;
import com.azure.tools.apiview.processor.model.ApiViewProperties;
import com.azure.tools.apiview.processor.model.Language;
import com.azure.tools.apiview.processor.model.LanguageVariant;
import com.azure.tools.apiview.processor.model.MarkdownRenderer;
import com.azure.tools.apiview.processor.model.maven.Pom;
import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.networknt.schema.JsonSchema;
import com.networknt.schema.JsonSchemaFactory;
import com.networknt.schema.SpecVersion;
import com.networknt.schema.ValidationMessage;

import java.io.File;
import java.io.IOException;
import java.net.URI;
import java.net.URISyntaxException;
import java.net.URL;
import java.nio.file.FileSystem;
import java.nio.file.FileSystems;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.List;
import java.util.Optional;
import java.util.Set;
import java.util.stream.Stream;

public class Main {
    private static final String RENDERER_OPTION_PREFIX = "--renderer=";

    // expected argument order:
    // [--renderer=json|markdown] [inputFiles] <outputDirectory>
    public static void main(String[] args) {
        if (args.length != 2 && args.length != 3) {
            printUsage();
            System.exit(-1);
        }

        final boolean hasRendererOption = args.length == 3 && args[0].startsWith(RENDERER_OPTION_PREFIX);
        if (args.length == 3 && !hasRendererOption) {
            printUsage();
            System.exit(-1);
        }

        long startMillis = System.currentTimeMillis();
        Renderer selectedRenderer = Renderer.JSON;
        if (hasRendererOption) {
            try {
                selectedRenderer = Renderer.fromValue(args[0].substring(RENDERER_OPTION_PREFIX.length()));
            } catch (IllegalArgumentException e) {
                System.out.println(e.getMessage());
                printUsage();
                System.exit(-1);
            }
        }
        final Renderer renderer = selectedRenderer;
        final int positionalArgumentOffset = hasRendererOption ? 1 : 0;
        final String jarFiles = args[positionalArgumentOffset];
        final String[] jarFilesArray = jarFiles.split(",");

        final File outputDir = new File(args[positionalArgumentOffset + 1]);

        System.out.println("Running with following configuration:");
        System.out.printf("  Output directory: '%s'%n", outputDir);
        System.out.printf("  Renderer: %s%n", renderer.value);

        Arrays.stream(jarFilesArray).forEach(jarFile -> run(new File(jarFile), outputDir, renderer));
        System.out.println("Finished processing in " + (System.currentTimeMillis() - startMillis) + "ms");
    }

    private static void printUsage() {
        System.out.println("Expected argument order: [--renderer=json|markdown] "
            + "[comma-separated sources jarFiles] <outputDirectory>, e.g. /path/to/jarfile.jar ./temp/");
    }

    /**
     * Runs APIView parser and returns the output file path as an array. The first value in the array is the
     * JSON file. If there are multiple outputs (i.e. gzipping is enabled), the second value in the array is the
     * gzipped file.
     */
    public static File[] run(File jarFile, File outputDir) {
        return run(jarFile, outputDir, Renderer.JSON);
    }

    private static File[] run(File jarFile, File outputDir, Renderer renderer) {
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

        final String outputFileName = jarFile.getName().substring(0, jarFile.getName().length() - 4);
        final Optional<APIListing> apiListing = processFile(jarFile);

        if (apiListing.isPresent()) {
            switch (renderer) {
                case JSON:
                    return renderJson(apiListing.get(), outputDir, outputFileName);
                case MARKDOWN:
                    return renderMarkdown(apiListing.get(), outputDir, outputFileName);
                default:
                    throw new IllegalStateException("Unsupported renderer: " + renderer);
            }
        }

        return new File[] { };
    }

    private static File[] renderJson(APIListing apiListing, File outputDir, String outputFileName) {
        File[] files = new File[Constants.GZIP_OUTPUT ? 2 : 1];
        files[0] = new File(outputDir, outputFileName + ".json");
        apiListing.toFile(files[0], false);
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
                // Load the schema from the classpath resource
                URL resource = Main.class.getResource(Constants.APIVIEW_JSON_SCHEMA_RESOURCE);
                if (resource == null) {
                    throw new IllegalStateException("Resource not found: " + Constants.APIVIEW_JSON_SCHEMA_RESOURCE);
                }
                URI localResourceUri = resource.toURI();
                JsonSchema schema = factory.getSchema(localResourceUri);

                JsonNode jsonNode = new ObjectMapper().readTree(files[0]);
                schema.initializeValidators();
                Set<ValidationMessage> validationMessages = schema.validate(jsonNode);
                if (validationMessages.isEmpty()) {
                    System.out.println("    Validation passed.");
                } else {
                    System.out.println("    Validation failed. Errors:");
                    validationMessages.forEach(msg -> System.out.println("      " + msg.getMessage()));
                }
            } catch (IOException | URISyntaxException e) {
                throw new RuntimeException(e);
            }
        }

        if (Constants.GZIP_OUTPUT) {
            files[1] = new File(outputDir, outputFileName + ".json.tgz");
            apiListing.toFile(files[1], true);
            System.out.println("  Output written to file: " + files[1]);
        }

        return files;
    }

    private static File[] renderMarkdown(APIListing apiListing, File outputDir, String outputFileName) {
        File outputFile = new File(outputDir, outputFileName + ".md");
        MarkdownRenderer.write(apiListing, outputFile);
        System.out.println("  Output written to file: " + outputFile);
        return new File[] { outputFile };
    }

    private enum Renderer {
        JSON("json"),
        MARKDOWN("markdown");

        private final String value;

        Renderer(String value) {
            this.value = value;
        }

        private static Renderer fromValue(String value) {
            switch (value) {
                case "json":
                    return JSON;
                case "markdown":
                    return MARKDOWN;
                default:
                    throw new IllegalArgumentException("Unsupported renderer '" + value + "'. Expected json or markdown.");
            }
        }
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
