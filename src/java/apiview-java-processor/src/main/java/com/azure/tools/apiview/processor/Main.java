package com.azure.tools.apiview.processor;

import com.azure.tools.apiview.processor.analysers.Analyser;
import com.azure.tools.apiview.processor.model.ChildItem;
import com.azure.tools.apiview.processor.model.Diagnostic;
import com.azure.tools.apiview.processor.model.TokenKind;
import com.azure.tools.apiview.processor.model.TypeKind;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.azure.tools.apiview.processor.analysers.ASTAnalyser;
import com.azure.tools.apiview.processor.model.APIListing;
import com.azure.tools.apiview.processor.model.Token;

import java.io.File;
import java.io.IOException;
import java.nio.file.FileSystems;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.ArrayList;
import java.util.List;

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

    private static void processFile(File inputFile, File outputFile) {
        APIListing apiListing = new APIListing();
        apiListing.setName(inputFile.getName());

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
            apiListing.addDiagnostic(new Diagnostic("error", "Uploaded files should end with '-sources.jar', " +
                    "as the APIView tool only works with source jar files, not compiled jar files. The uploaded file " +
                    "that was submitted to APIView was named " + inputFile.getName()));
        }

        // Write out to the filesystem
        try {
            ObjectMapper objectMapper = new ObjectMapper();
            objectMapper.disable(AUTO_DETECT_CREATORS, AUTO_DETECT_FIELDS, AUTO_DETECT_GETTERS, AUTO_DETECT_IS_GETTERS);
            objectMapper.writerWithDefaultPrettyPrinter().writeValue(outputFile, apiListing);
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