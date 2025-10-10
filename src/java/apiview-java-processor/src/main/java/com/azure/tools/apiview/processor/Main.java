package com.azure.tools.apiview.processor;

import com.azure.json.JsonProviders;
import com.azure.json.JsonWriter;
import com.azure.tools.apiview.processor.analysers.Analyser;
import com.azure.tools.apiview.processor.analysers.JavaASTAnalyser;
import com.azure.tools.apiview.processor.analysers.models.Constants;
import com.azure.tools.apiview.processor.diff.DiffEngine;
import com.azure.tools.apiview.processor.diff.collector.DiffSymbolCollector;
import com.azure.tools.apiview.processor.diff.dto.ApiDiffResult;
import com.azure.tools.apiview.processor.diff.model.ClassSymbol;
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
import java.io.FileOutputStream;
import java.io.IOException;
import java.io.OutputStream;
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
import java.util.HashMap;
import java.util.Collections;
import java.util.Map;
import java.util.stream.Stream;

public class Main {

    public static void main(String[] args) {
        ParsedArgs parsed = parseArgs(args);
        switch (parsed.mode) {
            case DIFF:
                createDiffListing(parsed);
                break;
            case LISTING:
                createApiListing(parsed.rawArgs);
                break;
            default:
                printUsageAndExit();
        }
    }

    private static void createApiListing(String[] args) {
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

    private static void createDiffListing(ParsedArgs parsed) {
        if (parsed.oldInputs.isEmpty() || parsed.newInputs.isEmpty() || parsed.outputDir == null) {
            System.out.println("Missing required --diff arguments");
            printUsageAndExit();
        }
        long startMillis = System.currentTimeMillis();
        System.out.println("Running diff mode");
        File outDir = null;
        if (parsed.outputDir != null) {
            outDir = new File(parsed.outputDir);
        }
        if (outDir != null && !outDir.exists()) outDir.mkdirs();

        // Collect source file paths (.java) from each input directory
        List<Path> oldSourceFiles = collectJavaSources(parsed.oldInputs);
        List<Path> newSourceFiles = collectJavaSources(parsed.newInputs);

        Map<String, ClassSymbol> oldClasses = new HashMap<>();
        Map<String, ClassSymbol> newClasses = new HashMap<>();

        DiffSymbolCollector oldCollector = new DiffSymbolCollector(oldClasses);
        DiffSymbolCollector newCollector = new DiffSymbolCollector(newClasses);
        new JavaASTAnalyser(new APIListing(), oldCollector, false).analyse(oldSourceFiles);
        new JavaASTAnalyser(new APIListing(), newCollector, false).analyse(newSourceFiles);

        DiffEngine engine = new DiffEngine();
        ApiDiffResult result = engine.diff(oldClasses, newClasses);

        File outputFile = new File(outDir, "apiview-diff.json");
        try (OutputStream os = Files.newOutputStream(outputFile.toPath());
             JsonWriter writer = JsonProviders.createWriter(os)) {
            result.write(writer);
            System.out.println("Diff output written: " + outputFile.getAbsolutePath());
        } catch (Exception e) {
            e.printStackTrace();
            System.exit(-1);
        }
        System.out.println("Diff finished in " + (System.currentTimeMillis() - startMillis) + "ms with " + result.getChanges().size() + " changes");
    }

    private static List<Path> collectJavaSources(List<String> inputs) {
        List<Path> out = new ArrayList<>();
        for (String in : inputs) {
            File f = new File(in);
            if (!f.exists()) continue;
            if (f.isDirectory()) {
                try (Stream<Path> paths = Files.walk(f.toPath())) {
                    paths.filter(p -> p.toString().endsWith(".java")).forEach(out::add);
                } catch (IOException e) {
                    e.printStackTrace();
                }
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
     * Lightweight representation of parsed command line.
     */
    private static final class ParsedArgs {
        enum Mode {LISTING, DIFF, INVALID}

        final Mode mode;
        final List<String> oldInputs;
        final List<String> newInputs;
        final String outputDir;
        final String[] rawArgs; // original for listing mode

        ParsedArgs(Mode mode, List<String> oldInputs, List<String> newInputs, String outputDir, String[] rawArgs) {
            this.mode = mode;
            this.oldInputs = oldInputs;
            this.newInputs = newInputs;
            this.outputDir = outputDir;
            this.rawArgs = rawArgs;
        }
    }

    private static ParsedArgs parseArgs(String[] args) {
        if (args == null || args.length == 0) {
            return new ParsedArgs(ParsedArgs.Mode.INVALID, Collections.emptyList(), Collections.emptyList(), null, args);
        }
        if (!args[0].startsWith("--") && args.length == 2) {
            // classic listing mode: <input(s)> <outputDir>
            return new ParsedArgs(ParsedArgs.Mode.LISTING, Collections.emptyList(), Collections.emptyList(), args[1], args);
        }
        // Diff / option mode parsing
        List<String> oldInputs = new ArrayList<>();
        List<String> newInputs = new ArrayList<>();
        String out = null;
        boolean diff = false;
        for (int i = 0; i < args.length; i++) {
            String a = args[i];
            switch (a) {
                case "--diff":
                    diff = true;
                    break;
                case "--old":
                    if (i + 1 < args.length) oldInputs.addAll(Arrays.asList(args[++i].split(",")));
                    else
                        return new ParsedArgs(ParsedArgs.Mode.INVALID, Collections.emptyList(), Collections.emptyList(), null, args);
                    break;
                case "--new":
                    if (i + 1 < args.length) newInputs.addAll(Arrays.asList(args[++i].split(",")));
                    else
                        return new ParsedArgs(ParsedArgs.Mode.INVALID, Collections.emptyList(), Collections.emptyList(), null, args);
                    break;
                case "--out":
                    if (i + 1 < args.length) out = args[++i];
                    else
                        return new ParsedArgs(ParsedArgs.Mode.INVALID, Collections.emptyList(), Collections.emptyList(), null, args);
                    break;
                default:
                    if (a.startsWith("--")) {
                        System.out.println("Unknown option: " + a);
                        return new ParsedArgs(ParsedArgs.Mode.INVALID, Collections.emptyList(), Collections.emptyList(), null, args);
                    }
            }
        }
        if (diff) {
            return new ParsedArgs(ParsedArgs.Mode.DIFF, oldInputs, newInputs, out, args);
        }
        return new ParsedArgs(ParsedArgs.Mode.INVALID, Collections.emptyList(), Collections.emptyList(), null, args);
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

        return new File[]{};
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
