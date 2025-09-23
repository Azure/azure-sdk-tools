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

    public static void main(String[] args) {
        if (args.length == 0) {
            printUsageAndExit();
        }

        // Detect --diff mode
        boolean isDiff = false;
        List<String> oldInputs = new ArrayList<>();
        List<String> newInputs = new ArrayList<>();
        String outputDirArg = null;
        for (int i = 0; i < args.length; i++) {
            String a = args[i];
            if ("--diff".equals(a)) {
                isDiff = true;
            } else if ("--old".equals(a) && i + 1 < args.length) {
                oldInputs.addAll(Arrays.asList(args[++i].split(",")));
            } else if ("--new".equals(a) && i + 1 < args.length) {
                newInputs.addAll(Arrays.asList(args[++i].split(",")));
            } else if ("--out".equals(a) && i + 1 < args.length) {
                outputDirArg = args[++i];
            } else if (a.startsWith("--")) {
                System.out.println("Unknown option: " + a);
                printUsageAndExit();
            } else {
                // fallback classic mode collection
                if (outputDirArg == null && args.length == 2) {
                    // classic mode: [inputFiles] <outputDir>
                    classicMode(args);
                    return;
                }
            }
        }

        if (isDiff) {
            runDiffMode(oldInputs, newInputs, outputDirArg);
            return;
        }

        // If not diff mode, fallback to classic
        if (args.length == 2) {
            classicMode(args);
        } else {
            printUsageAndExit();
        }
    }

    private static void classicMode(String[] args) {
        if (args.length != 2) {
            printUsageAndExit();
        }
        long startMillis = System.currentTimeMillis();
        final String jarFiles = args[0];
        final String[] jarFilesArray = jarFiles.split(",");
        final File outputDir = new File(args[1]);
        System.out.println("Running with following configuration:");
        System.out.printf("  Output directory: '%s'%n", outputDir);
        Arrays.stream(jarFilesArray).forEach(jarFile -> run(new File(jarFile), outputDir));
        System.out.println("Finished processing in " + (System.currentTimeMillis() - startMillis) + "ms");
    }

    private static void runDiffMode(List<String> oldInputs, List<String> newInputs, String outputDirArg) {
        if (oldInputs.isEmpty() || newInputs.isEmpty() || outputDirArg == null) {
            System.out.println("Missing required --diff arguments");
            printUsageAndExit();
        }
        long startMillis = System.currentTimeMillis();
        System.out.println("Running diff mode");
        File outDir = new File(outputDirArg);
        if (!outDir.exists()) outDir.mkdirs();

        // Collect source file paths (.java) from each input directory
        List<Path> oldSourceFiles = collectJavaSources(oldInputs);
        List<Path> newSourceFiles = collectJavaSources(newInputs);

        com.azure.tools.apiview.processor.diff.model.ApiSymbolTable oldTable = new com.azure.tools.apiview.processor.diff.model.ApiSymbolTable();
        com.azure.tools.apiview.processor.diff.model.ApiSymbolTable newTable = new com.azure.tools.apiview.processor.diff.model.ApiSymbolTable();

        com.azure.tools.apiview.processor.diff.collector.DiffSymbolCollector oldCollector = new com.azure.tools.apiview.processor.diff.collector.DiffSymbolCollector(oldTable);
        com.azure.tools.apiview.processor.diff.collector.DiffSymbolCollector newCollector = new com.azure.tools.apiview.processor.diff.collector.DiffSymbolCollector(newTable);
        new com.azure.tools.apiview.processor.analysers.JavaASTAnalyser(new com.azure.tools.apiview.processor.model.APIListing(), oldCollector).analyse(oldSourceFiles);
        new com.azure.tools.apiview.processor.analysers.JavaASTAnalyser(new com.azure.tools.apiview.processor.model.APIListing(), newCollector).analyse(newSourceFiles);

        com.azure.tools.apiview.processor.diff.DiffEngine engine = new com.azure.tools.apiview.processor.diff.DiffEngine();
        java.util.List<com.azure.tools.apiview.processor.diff.dto.ApiChangeDto> changes = engine.diff(oldTable, newTable);
        com.azure.tools.apiview.processor.diff.dto.ApiDiffResult result = new com.azure.tools.apiview.processor.diff.dto.ApiDiffResult();
        result.changes = changes;

        File outputFile = new File(outDir, "apiview-diff.json");
        try (java.io.OutputStream os = new java.io.FileOutputStream(outputFile);
             com.azure.json.JsonWriter writer = com.azure.json.JsonProviders.createWriter(os)) {
            result.write(writer);
            System.out.println("Diff output written: " + outputFile.getAbsolutePath());
        } catch (Exception e) {
            e.printStackTrace();
            System.exit(-1);
        }
        System.out.println("Diff finished in " + (System.currentTimeMillis() - startMillis) + "ms with " + changes.size() + " changes");
    }

    private static List<Path> collectJavaSources(List<String> inputs) {
        List<Path> out = new ArrayList<>();
        for (String in : inputs) {
            File f = new File(in);
            if (!f.exists()) continue;
            if (f.isDirectory()) {
                try (Stream<Path> paths = Files.walk(f.toPath())) {
                    paths.filter(p -> p.toString().endsWith(".java")).forEach(out::add);
                } catch (IOException e) { e.printStackTrace(); }
            }
        }
        return out;
    }

    private static void printUsageAndExit() {
        System.out.println("Usage:\n" +
                "  Listing mode: <sourcesOrJarsCommaSeparated> <outputDir>\n" +
                "  Diff mode: --diff --old <oldPathsCommaSeparated> --new <newPathsCommaSeparated> --out <outputDir>\n");
        System.exit(-1);
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

        if (inputFile.isDirectory()) {
            processSourceDirectory(inputFile, apiListing);
            return Optional.of(apiListing);
        } else if (inputFile.getName().endsWith("-sources.jar")) {
            processJavaSourcesJar(inputFile, apiListing);
            return Optional.of(apiListing);
        }

        System.out.println("  Skipping unsupported input: " + inputFile);
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

    private static void processSourceDirectory(File dir, APIListing apiListing) {
        System.out.println("  Processing source directory: '" + dir + "'");
        final Pom mavenPom = Pom.fromDirectory(dir);
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

        final List<Path> allFiles = new ArrayList<>();
        try (Stream<Path> paths = Files.walk(dir.toPath())) {
            paths.forEach(allFiles::add);
            final Analyser analyser = new JavaASTAnalyser(apiListing);
            analyser.analyse(allFiles);
        } catch (IOException e) {
            e.printStackTrace();
        }
    }
}
