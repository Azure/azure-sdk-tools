// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

package com.azure.tools.codesnippetplugin;

import org.apache.maven.plugin.MojoExecutionException;
import org.apache.maven.plugin.logging.Log;

import java.io.BufferedWriter;
import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.FileSystems;
import java.nio.file.FileVisitResult;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.PathMatcher;
import java.nio.file.SimpleFileVisitor;
import java.nio.file.attribute.BasicFileAttributes;
import java.util.ArrayList;
import java.util.Collections;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

final class SnippetReplacer {
    static final Pattern SNIPPET_DEF_BEGIN =
        Pattern.compile("\\s*\\/\\/\\s*BEGIN\\:\\s+([a-zA-Z0-9\\.\\#\\-\\_]*)\\s*");
    static final Pattern SNIPPET_DEF_END = Pattern.compile("\\s*\\/\\/\\s*END\\:\\s+([a-zA-Z0-9\\.\\#\\-\\_]*)\\s*");
    static final Pattern SNIPPET_SRC_CALL_BEGIN =
        Pattern.compile("(\\s*)\\*?\\s*<!--\\s+src_embed\\s+([a-zA-Z0-9\\.\\#\\-\\_]*)\\s*-->");
    static final Pattern SNIPPET_SRC_CALL_END =
        Pattern.compile("(\\s*)\\*?\\s*<!--\\s+end\\s+([a-zA-Z0-9\\.\\#\\-\\_]*)\\s*-->");
    static final Pattern SNIPPET_README_CALL_BEGIN =
        Pattern.compile("```(\\s*)?Java\\s+([a-zA-Z0-9\\.\\#\\-\\_]*)\\s*");
    static final Pattern SNIPPET_README_CALL_END = Pattern.compile("```");
    static final Pattern WHITESPACE_EXTRACTION = Pattern.compile("(\\s*)(.*)");
    static final Pattern END_OF_LINE_WHITESPACES = Pattern.compile("[\\s]+$");

    static final String DEFAULT_CODESNIPPET_GLOB = "**/src/samples/java/**/*.java";
    static final String DEFAULT_SOURCE_GLOB = "**/*.java";

    private static final HashMap<String, String> REPLACEMENT_SET = new HashMap<String, String>() {{
        put("\"", "&quot;");
        put(">", "&gt;");
        put("<", "&lt;");
        put("@", "{@literal @}");
        put("{", "&#123;");
        put("}", "&#125;");
        put("(", "&#40;");
        put(")", "&#41;");
        put("/", "&#47;");
        put("\\", "&#92;");
    }};

    /**
     * The "verification" operation encapsulated by this function is as follows.
     *
     * 1. Scan under the target direction for all discovered code snippet DEFINITIONS 2. Examine all snippet CALLS,
     * finding where updates are needed. 3. Report all discovered snippets in need of update as well as all bad snippet
     * calls
     *
     * A "bad snippet call" is simply calling for a snippet whose Id has no definition.
     *
     * See {@link #updateCodesnippets(Path, String, Path, String, boolean, Path, boolean, int, Log)} for details on
     * actually defining and calling snippets.
     */
    static void verifyCodesnippets(Path codesnippetRootDirectory, String codesnippetGlob, Path sourcesRootDirectory,
        String sourcesGlob, boolean includeSources, Path readmePath, boolean includeReadme, int maxLineLength,
        Log logger) throws IOException, MojoExecutionException {
        // Neither sources nor README is included in the update, there is no work to be done.
        if (!includeSources && !includeReadme) {
            logger.debug("Neither sources or README were included. No codesnippet updating will be done.");
            return;
        }

        // Codesnippet root directory isn't a directory, throw an exception.
        if (!codesnippetRootDirectory.toFile().isDirectory()) {
            throw new MojoExecutionException(String.format("Codesnippet root directory isn't a directory: %s",
                codesnippetRootDirectory));
        }

        // Get the files that match the codesnippet glob and are contained in the codesnippet root directory.
        List<Path> codesnippetFiles = globFiles(codesnippetRootDirectory, FileSystems.getDefault()
            .getPathMatcher("glob:" + codesnippetGlob));

        // Only get the source files if sources are included in the update.
        List<Path> sourceFiles = Collections.emptyList();
        if (includeSources) {
            // Sources root directory isn't a directory, throw an exception.
            if (!sourcesRootDirectory.toFile().isDirectory()) {
                throw new MojoExecutionException(String.format("Sources root directory isn't a directory: %s",
                    sourcesRootDirectory));
            }

            // Get the files that match the sources glob and are contained in the sources root directory.
            sourceFiles = globFiles(sourcesRootDirectory, FileSystems.getDefault().getPathMatcher(sourcesGlob));
        }

        List<VerifyResult> snippetsNeedingUpdate = new ArrayList<>();
        List<VerifyResult> badSnippetCalls = new ArrayList<>();

        // scan the sample files for all the snippet files
        Map<String, List<String>> foundSnippets = getAllSnippets(codesnippetFiles);

        // walk across all the java files, run UpdateSrcSnippets
        if (includeSources) {
            for (Path sourcePath : sourceFiles) {
                SnippetOperationResult<List<VerifyResult>> sourcesResult = verifySrcSnippets(sourcePath, foundSnippets);
                snippetsNeedingUpdate.addAll(sourcesResult.result);
                badSnippetCalls.addAll(sourcesResult.errorList);
            }
        }

        // now find folderToVerify/README.md
        // run Update ReadmeSnippets on that
        if (includeReadme) {
            SnippetOperationResult<List<VerifyResult>> readmeResult = verifyReadmeSnippets(readmePath, foundSnippets);
            snippetsNeedingUpdate.addAll(readmeResult.result);
            badSnippetCalls.addAll(readmeResult.errorList);
        }

        if (snippetsNeedingUpdate.size() > 0 || badSnippetCalls.size() > 0) {
            for (VerifyResult result : snippetsNeedingUpdate) {
                logger.error(String.format("SnippetId %s needs update in file %s.", result.snippetWithIssues,
                    result.readmeLocation));
            }

            for (VerifyResult result : badSnippetCalls) {
                logger.error(String.format("Unable to locate snippet with Id of %s. Reference in %s",
                    result.snippetWithIssues, result.readmeLocation));
            }

            throw new MojoExecutionException("Snippet-Replacer has encountered errors, check above output for details.");
        }
    }

    /**
     * This method encapsulates the "update" lifecycle of the snippet-replacer plugin.
     * <p>
     * Given a root folder, the plugin will scan for snippet DEFINITIONS or snippet CALLS. Once a snippet definition
     * index has been formulated, all java files located under the target directory will have snippet CALLS updated with
     * the source from the DEFINITIONS.
     *
     * <p><strong>Snippet Definition</strong></p>
     *
     * A snippet definition is delineated by BEGIN and END comments directly in your java source. Example:
     * <pre>
     * // BEGIN: com.azure.data.applicationconfig.configurationclient.instantiation
     * ConfigurationClient configurationClient = new ConfigurationClientBuilder&#40;&#41;
     *     .connectionString&#40;connectionString&#41;
     *     .buildClient&#40;&#41;;
     * // END: com.azure.data.applicationconfig.configurationclient.instantiation
     * </pre>
     *
     * <p><strong>Calling a Snippet</strong></p>
     *
     * From within a javadoc comment, embed an html comment &#47;* &lt;!-- src_embed
     * com.azure.data.applicationconfig.configurationclient.instantiation --&gt; ConfigurationClient configurationClient
     * = new ConfigurationClientBuilder&#40;&#41; .connectionString&#40;connectionString&#41; .buildClient&#40;&#41;;
     * &lt;!-- end com.azure.data.applicationconfig.configurationclient.instantiation --&gt; Other javadoc details
     * perhaps. *&#47; public void myfunction()
     * </pre>
     *
     * After finishing update operations, this function will throw a MojoExecutionException after reporting all snippet
     * CALLS that have no DEFINITION.
     */
    static void updateCodesnippets(Path codesnippetRootDirectory, String codesnippetGlob, Path sourcesRootDirectory,
        String sourcesGlob, boolean includeSources, Path readmePath, boolean includeReadme, int maxLineLength,
        Log logger) throws IOException, MojoExecutionException {
        // Neither sources nor README is included in the update, there is no work to be done.
        if (!includeSources && !includeReadme) {
            logger.debug("Neither sources or README were included. No codesnippet updating will be done.");
            return;
        }

        // Codesnippet root directory isn't a directory, throw an exception.
        if (!codesnippetRootDirectory.toFile().isDirectory()) {
            throw new MojoExecutionException(String.format("Codesnippet root directory isn't a directory: %s",
                codesnippetRootDirectory));
        }

        // Get the files that match the codesnippet glob and are contained in the codesnippet root directory.
        List<Path> codesnippetFiles = globFiles(codesnippetRootDirectory, FileSystems.getDefault()
            .getPathMatcher("glob:" + codesnippetGlob));

        // Only get the source files if sources are included in the update.
        List<Path> sourceFiles = Collections.emptyList();
        if (includeSources) {
            // Sources root directory isn't a directory, throw an exception.
            if (!sourcesRootDirectory.toFile().isDirectory()) {
                throw new MojoExecutionException(String.format("Sources root directory isn't a directory: %s",
                    sourcesRootDirectory));
            }

            // Get the files that match the sources glob and are contained in the sources root directory.
            sourceFiles = globFiles(sourcesRootDirectory, FileSystems.getDefault().getPathMatcher(sourcesGlob));
        }

        List<VerifyResult> badSnippetCalls = new ArrayList<>();

        // scan the sample files for all the snippet files
        Map<String, List<String>> foundSnippets = getAllSnippets(codesnippetFiles);

        // walk across all the java files, run UpdateSrcSnippets
        if (includeSources) {
            for (Path sourcePath : sourceFiles) {
                badSnippetCalls.addAll(updateSourceCodeSnippets(sourcePath, foundSnippets));
            }
        }

        // now find folderToVerify/README.md
        // run Update ReadmeSnippets on that
        if (includeReadme) {
            badSnippetCalls.addAll(updateReadmeCodesnippets(readmePath, foundSnippets));
        }

        if (badSnippetCalls.size() > 0) {
            for (VerifyResult result : badSnippetCalls) {
                logger.error(String.format("Unable to locate snippet with Id of %s. Reference in %s",
                    result.snippetWithIssues, result.readmeLocation.toString()));
            }
            throw new MojoExecutionException("Discovered snippets in need of updating. "
                + "Please run this plugin in update mode and commit the changes.");
        }
    }

    static List<VerifyResult> updateReadmeCodesnippets(Path file, Map<String, List<String>> snippetMap) throws IOException {
        List<String> lines = Files.readAllLines(file, StandardCharsets.UTF_8);
        SnippetOperationResult<StringBuilder> opResult = updateSnippets(file,
            lines,
            SNIPPET_README_CALL_BEGIN,
            SNIPPET_README_CALL_END,
            snippetMap,
            "",
            "",
            0,
            "",
            true);

        if (opResult.result != null) {
            try (BufferedWriter writer = Files.newBufferedWriter(file, StandardCharsets.UTF_8)) {
                writer.write(opResult.result.toString());
            }
        }

        return opResult.errorList;
    }

    static List<VerifyResult> updateSourceCodeSnippets(Path file, Map<String, List<String>> snippetMap)
        throws IOException {
        List<String> lines = Files.readAllLines(file, StandardCharsets.UTF_8);
        SnippetOperationResult<StringBuilder> opResult = updateSnippets(file,
            lines,
            SNIPPET_SRC_CALL_BEGIN,
            SNIPPET_SRC_CALL_END,
            snippetMap,
            "<pre>",
            "</pre>",
            1,
            "* ",
            false);

        if (opResult.result != null) {
            try (BufferedWriter writer = Files.newBufferedWriter(file, StandardCharsets.UTF_8)) {
                writer.write(opResult.result.toString());
            }
        }

        return opResult.errorList;
    }

    static SnippetOperationResult<StringBuilder> updateSnippets(Path file, List<String> lines, Pattern beginRegex,
        Pattern endRegex, Map<String, List<String>> snippetMap, String preFence, String postFence, int prefixGroupNum,
        String additionalLinePrefix, boolean disableEscape) {

        List<VerifyResult> badSnippetCalls = new ArrayList<>();
        StringBuilder modifiedLines = new StringBuilder();
        boolean inSnippet = false;
        boolean needsAmend = false;
        String lineSep = System.lineSeparator();
        String currentSnippetId = "";

        for (String line : lines) {
            Matcher begin = beginRegex.matcher(line);
            Matcher end = endRegex.matcher(line);

            if (begin.matches()) {
                modifiedLines.append(line).append(lineSep);
                currentSnippetId = begin.group(2);
                inSnippet = true;
            } else if (end.matches()) {
                if (inSnippet) {
                    List<String> newSnippets;
                    if (snippetMap.containsKey(currentSnippetId)) {
                        newSnippets = snippetMap.get(currentSnippetId);
                    } else {
                        badSnippetCalls.add(new VerifyResult(file, currentSnippetId));
                        needsAmend = true;
                        inSnippet = false;
                        continue;
                    }

                    List<String> modifiedSnippets = new ArrayList<>();

                    // We use this additional prefix because in src snippet cases we need to prespace
                    // for readme snippet cases we DONT need the prespace at all.
                    String linePrefix = prefixFunction(end, prefixGroupNum, additionalLinePrefix);

                    for (String snippet : respaceLines(newSnippets)) {
                        String moddedSnippet = disableEscape ? snippet : escapeString(snippet);
                        modifiedSnippets.add(moddedSnippet.length() == 0
                            ? END_OF_LINE_WHITESPACES.matcher(linePrefix).replaceAll("") + lineSep
                            : linePrefix + moddedSnippet + lineSep);
                    }

                    if (preFence != null && preFence.length() > 0) {
                        modifiedLines.append(linePrefix).append(preFence).append(lineSep);
                    }

                    modifiedSnippets.forEach(modifiedLines::append);

                    if (postFence != null && postFence.length() > 0) {
                        modifiedLines.append(linePrefix).append(postFence).append(lineSep);
                    }

                    modifiedLines.append(line).append(lineSep);
                    needsAmend = true;
                    inSnippet = false;
                }
            } else {
                if (inSnippet) {
                    // do nothing. we'll write everything at the end,
                    // we'd do a comparison here if we were verifying
                } else {
                    modifiedLines.append(line).append(lineSep);
                }
            }
        }

        if (needsAmend) {
            return new SnippetOperationResult<>(modifiedLines, badSnippetCalls);
        } else {
            return null;
        }
    }

    static SnippetOperationResult<List<VerifyResult>> verifyReadmeSnippets(Path file,
        Map<String, List<String>> snippetMap) throws IOException {
        List<String> lines = Files.readAllLines(file, StandardCharsets.UTF_8);
        return verifySnippets(file, lines, SNIPPET_README_CALL_BEGIN, SNIPPET_README_CALL_END, snippetMap, "", "", 0,
            "", true);
    }

    static SnippetOperationResult<List<VerifyResult>> verifySrcSnippets(Path file,
        Map<String, List<String>> snippetMap) throws IOException {
        List<String> lines = Files.readAllLines(file, StandardCharsets.UTF_8);
        return verifySnippets(file, lines, SNIPPET_SRC_CALL_BEGIN, SNIPPET_SRC_CALL_END, snippetMap, "<pre>", "</pre>",
            1, "* ", false);
    }

    static SnippetOperationResult<List<VerifyResult>> verifySnippets(Path file, List<String> lines, Pattern beginRegex,
        Pattern endRegex, Map<String, List<String>> snippetMap, String preFence, String postFence, int prefixGroupNum,
        String additionalLinePrefix, boolean disableEscape) {

        boolean inSnippet = false;
        String lineSep = System.lineSeparator();
        List<String> currentSnippetSet = null;
        List<VerifyResult> foundIssues = new ArrayList<>();
        List<VerifyResult> badSnippetCalls = new ArrayList<>();
        String currentSnippetId = "";

        for (String line : lines) {
            Matcher begin = beginRegex.matcher(line);
            Matcher end = endRegex.matcher(line);

            if (begin.matches()) {
                currentSnippetId = begin.group(2);
                inSnippet = true;
                currentSnippetSet = new ArrayList<>();
            } else if (end.matches()) {
                if (inSnippet) {
                    List<String> newSnippets;
                    if (snippetMap.containsKey(currentSnippetId)) {
                        newSnippets = snippetMap.get(currentSnippetId);
                    } else {
                        badSnippetCalls.add(new VerifyResult(file, currentSnippetId));
                        inSnippet = false;
                        currentSnippetSet = null;
                        continue;
                    }
                    List<String> modifiedSnippets = new ArrayList<>();

                    // We use this additional prefix because in src snippet cases we need to prespace
                    // for readme snippet cases we DONT need the prespace at all.
                    String linePrefix = prefixFunction(end, prefixGroupNum, additionalLinePrefix);

                    for (String snippet : respaceLines(newSnippets)) {
                        String moddedSnippet = disableEscape ? snippet : escapeString(snippet);
                        modifiedSnippets.add(moddedSnippet.length() == 0
                            ? END_OF_LINE_WHITESPACES.matcher(linePrefix).replaceAll("") + lineSep
                            : linePrefix + moddedSnippet + lineSep);
                    }

                    Collections.sort(modifiedSnippets);
                    Collections.sort(currentSnippetSet);

                    if (!modifiedSnippets.equals(currentSnippetSet)) {
                        foundIssues.add(new VerifyResult(file, currentSnippetId));
                    }

                    inSnippet = false;
                    currentSnippetSet = null;
                }
            } else {
                if (inSnippet) {
                    if (preFence.length() > 0 && postFence.length() > 0) {
                        if (!line.contains(preFence) && !line.contains(postFence)) {
                            currentSnippetSet.add(line + lineSep);
                        }
                    } else {
                        currentSnippetSet.add(line + lineSep);
                    }
                }
            }
        }

        return new SnippetOperationResult<>(foundIssues, badSnippetCalls);
    }

    static Map<String, List<String>> getAllSnippets(List<Path> snippetSources)
        throws IOException, MojoExecutionException {
        Map<String, List<String>> locatedSnippets = new HashMap<>();
        List<VerifyResult> detectedIssues = new ArrayList<>();

        for (Path samplePath : snippetSources) {
            List<String> fileContent = Files.readAllLines(samplePath, StandardCharsets.UTF_8);
            Map<String, List<String>> tempSnippetMap = new HashMap<>();
            SnippetDictionary snippetReader = new SnippetDictionary();

            for (String line : fileContent) {
                Matcher begin = SNIPPET_DEF_BEGIN.matcher(line);
                Matcher end = SNIPPET_DEF_END.matcher(line);

                if (begin.matches()) {
                    String id_beginning = begin.group(1);
                    snippetReader.beginSnippet((id_beginning));
                } else if (end.matches()) {
                    String id_ending = end.group(1);
                    List<String> snippetContent = snippetReader.finalizeSnippet((id_ending));
                    if (!tempSnippetMap.containsKey((id_ending))) {
                        tempSnippetMap.put(id_ending, snippetContent);
                    } else {
                        // detect duplicate in file
                        detectedIssues.add(new VerifyResult(samplePath, id_ending));
                    }
                } else if (snippetReader.isActive()) {
                    snippetReader.processLine(line);
                }
            }

            // we need to examine them individually, as we want to get a complete list of all the duplicates in a run
            for (String snippetId : tempSnippetMap.keySet()) {
                if (!locatedSnippets.containsKey(snippetId)) {
                    locatedSnippets.put(snippetId, tempSnippetMap.get(snippetId));
                } else {
                    // detect duplicate across multiple files
                    detectedIssues.add(new VerifyResult(samplePath, snippetId));
                }
            }
        }
        ;

        if (detectedIssues.size() > 0) {
            throw new MojoExecutionException("Duplicate Snippet Definitions Detected. " + System.lineSeparator() +
                getErrorString(detectedIssues));
        }

        return locatedSnippets;
    }

    static String getErrorString(List<VerifyResult> errors) {
        StringBuilder results = new StringBuilder();

        for (VerifyResult result : errors) {
            results.append(String.format("Duplicate snippetId %s detected in %s.", result.snippetWithIssues, result.readmeLocation))
                .append(System.lineSeparator());
        }

        return results.toString();
    }

    static List<String> respaceLines(List<String> snippetText) {
        // get List of all the leading whitespace in the sample
        // toss out lines that are empty (as they shouldn't mess with the minimum)
        String minWhitespace = null;
        List<String> modifiedStrings = new ArrayList<>();

        for (String snippetLine : snippetText) {
            // only look at non-whitespace only strings for the min indent
            if (snippetLine.trim().length() != 0) {
                Matcher leadSpaceMatch = WHITESPACE_EXTRACTION.matcher(snippetLine);

                if (leadSpaceMatch.matches()) {
                    String leadSpace = leadSpaceMatch.group(1);

                    if (minWhitespace == null || leadSpace.length() < minWhitespace.length())
                        minWhitespace = leadSpace;
                }
            }
        }

        if (minWhitespace != null) {
            for (String snippetLine : snippetText) {
                modifiedStrings.add(snippetLine.replaceFirst(minWhitespace, ""));
            }
        }

        return modifiedStrings;
    }

    static int getEndIndex(List<String> lines, int startIndex) {
        for (int i = startIndex; i < lines.size(); i++) {
            Matcher end = SNIPPET_SRC_CALL_END.matcher(lines.get(i));
            if (end.matches())
                return i;
        }

        return -1;
    }

    static String prefixFunction(Matcher match, int groupNum, String additionalPrefix) {
        // if we pass -1 as the matcher groupNum, we don't want any prefix at all
        if (match == null || groupNum < 1) {
            return "";
        } else {
            return match.group(groupNum) + additionalPrefix;
        }
    }

    static String escapeString(String target) {
        if (target != null && target.trim().length() > 0) {
            for (String key : REPLACEMENT_SET.keySet()) {
                target = target.replace(key, REPLACEMENT_SET.get(key));
            }
        }

        return target;
    }

    static List<Path> globFiles(Path rootFolder, PathMatcher pathMatcher) throws IOException {
        List<Path> locatedPaths = new ArrayList<>();

        Files.walkFileTree(rootFolder, new SimpleFileVisitor<Path>() {
            @Override
            public FileVisitResult visitFile(Path file, BasicFileAttributes attrs) {
                if (pathMatcher.matches(file)) {
                    locatedPaths.add(file);
                }
                return FileVisitResult.CONTINUE;
            }

            @Override
            public FileVisitResult visitFileFailed(Path file, IOException exc) {
                return FileVisitResult.CONTINUE;
            }
        });

        return locatedPaths;
    }

    private SnippetReplacer() {
    }
}
