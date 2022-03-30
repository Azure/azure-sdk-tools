// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

package com.azure.tools.codesnippetplugin.implementation;

import com.azure.tools.codesnippetplugin.ExecutionMode;
import com.azure.tools.codesnippetplugin.RootAndGlob;
import org.apache.maven.plugin.MojoExecutionException;
import org.apache.maven.plugin.logging.Log;

import java.io.BufferedWriter;
import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.ArrayList;
import java.util.Collections;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.regex.Matcher;
import java.util.regex.Pattern;
import java.util.stream.Collectors;

public final class SnippetReplacer {
    private static final String SNIPPET_ID_CAPTURE = "\\s*([a-zA-Z0-9.#\\-_]+)\\s*";
    private static final Pattern SNIPPET_DEF_BEGIN = Pattern.compile(String.format("\\s*//\\s*BEGIN:%s", SNIPPET_ID_CAPTURE));
    private static final Pattern SNIPPET_DEF_END = Pattern.compile(String.format("\\s*//\\s*END:%s", SNIPPET_ID_CAPTURE));
    private static final Pattern SNIPPET_SRC_CALL_BEGIN = Pattern.compile(String.format("(\\s*)\\*?\\s*<!--\\s*src_embed%s-->", SNIPPET_ID_CAPTURE));
    private static final Pattern SNIPPET_SRC_CALL_END = Pattern.compile(String.format("(\\s*)\\*?\\s*<!--\\s*end%s-->", SNIPPET_ID_CAPTURE));
    private static final Pattern SNIPPET_README_CALL_BEGIN = Pattern.compile(String.format("(\\s*)?```(\\s*)?java%s", SNIPPET_ID_CAPTURE));
    private static final Pattern SNIPPET_README_CALL_END = Pattern.compile("(\\s*)?```\\s*");
    private static final Pattern WHITESPACE_EXTRACTION = Pattern.compile("(\\s*)(.*)");
    private static final Pattern END_OF_LINE_WHITESPACES = Pattern.compile("\\s+$");

    private static final String JAVADOC_PRE_FENCE = "<pre>";
    private static final String JAVADOC_POST_FENCE = "</pre>";

    // Ordering matters. If the ampersand (&) isn't done first it will double encode ampersands used in other
    // replacements.
    private static final List<CodesnippetReplacement> CODESNIPPET_REPLACEMENTS;

    static {
        CODESNIPPET_REPLACEMENTS = new ArrayList<>();
        CODESNIPPET_REPLACEMENTS.add(new CodesnippetReplacement("&", "&amp;"));
        CODESNIPPET_REPLACEMENTS.add(new CodesnippetReplacement("\"", "&quot;"));
        CODESNIPPET_REPLACEMENTS.add(new CodesnippetReplacement(">", "&gt;"));
        CODESNIPPET_REPLACEMENTS.add(new CodesnippetReplacement("<", "&lt;"));
        CODESNIPPET_REPLACEMENTS.add(new CodesnippetReplacement("@", "&#64;"));
        CODESNIPPET_REPLACEMENTS.add(new CodesnippetReplacement("\\{", "&#123;"));
        CODESNIPPET_REPLACEMENTS.add(new CodesnippetReplacement("}", "&#125;"));
        CODESNIPPET_REPLACEMENTS.add(new CodesnippetReplacement("\\(", "&#40;"));
        CODESNIPPET_REPLACEMENTS.add(new CodesnippetReplacement("\\)", "&#41;"));
        CODESNIPPET_REPLACEMENTS.add(new CodesnippetReplacement("/", "&#47;"));
        CODESNIPPET_REPLACEMENTS.add(new CodesnippetReplacement("\\\\", "&#92;"));
    }

    /**
     * The "verification" operation encapsulated by this function is as follows.
     *
     * 1. Scan under the target direction for all discovered code snippet DEFINITIONS 2. Examine all snippet CALLS,
     * finding where updates are needed. 3. Report all discovered snippets in need of update as well as all bad snippet
     * calls
     *
     * A "bad snippet call" is simply calling for a snippet whose ID has no definition.
     *
     * See {@link #updateCodesnippets(RootAndGlob, List, RootAndGlob, boolean, RootAndGlob, List, boolean, int, boolean, Log)}
     * for details on actually defining and calling snippets.
     */
    public static void verifyCodesnippets(RootAndGlob codesnippetRootAndGlob, List<RootAndGlob> additionalCodesnippets,
        RootAndGlob sourcesRootAndGlob, boolean includeSources, RootAndGlob readmeRootAndGlob,
        List<RootAndGlob> additionalReadmes, boolean includeReadme, int maxLineLength, boolean failOnError, Log logger)
        throws IOException, MojoExecutionException {
        runCodesnippets(codesnippetRootAndGlob, additionalCodesnippets, sourcesRootAndGlob, includeSources,
            readmeRootAndGlob, additionalReadmes, includeReadme, maxLineLength, failOnError, ExecutionMode.VERIFY,
            logger);
    }

    static List<CodesnippetError> verifyReadmeCodesnippets(Path file, Map<String, Codesnippet> snippetMap)
        throws IOException {
        return verifySnippets(file, SNIPPET_README_CALL_BEGIN, 3, SNIPPET_README_CALL_END, snippetMap, "", "", 0, "",
            Collections.emptyList(), Integer.MAX_VALUE, true);
    }

    static List<CodesnippetError> verifySourceCodeSnippets(Path file, Map<String, Codesnippet> snippetMap,
        int maxLineLength) throws IOException {
        return verifySnippets(file, SNIPPET_SRC_CALL_BEGIN, 2, SNIPPET_SRC_CALL_END, snippetMap, JAVADOC_PRE_FENCE,
            JAVADOC_POST_FENCE, 1, "* ", CODESNIPPET_REPLACEMENTS, maxLineLength, false);
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
     * A snippet definition is delineated by BEGIN and END comments directly in your java source. Example: <!--
     * src_embed: com.azure.data.applicationconfig.configurationclient.instantiation -->
     * <pre><code>
     * ConfigurationClient configurationClient = new ConfigurationClientBuilder&#40;&#41;
     *     .connectionString&#40;connectionString&#41;
     *     .buildClient&#40;&#41;;
     * </code></pre>
     * <!-- end com.azure.data.applicationconfig.configurationclient.instantiation -->
     *
     * <p><strong>Calling a Snippet</strong></p>
     *
     * From within a javadoc comment, embed an HTML comment &#47;* &lt;!-- src_embed
     * com.azure.data.applicationconfig.configurationclient.instantiation --&gt; ConfigurationClient configurationClient
     * = new ConfigurationClientBuilder&#40;&#41; .connectionString&#40;connectionString&#41; .buildClient&#40;&#41;;
     * &lt;!-- end com.azure.data.applicationconfig.configurationclient.instantiation --&gt; Other javadoc details
     * perhaps. *&#47; public void myfunction()
     * </pre>
     *
     * After finishing update operations, this function will throw a MojoExecutionException after reporting all snippet
     * CALLS that have no DEFINITION.
     */
    public static void updateCodesnippets(RootAndGlob codesnippetRootAndGlob, List<RootAndGlob> additionalCodesnippets,
        RootAndGlob sourcesRootAndGlob, boolean includeSources, RootAndGlob readmeRootAndGlob,
        List<RootAndGlob> additionalReadmes, boolean includeReadme, int maxLineLength, boolean failOnError, Log logger)
        throws IOException, MojoExecutionException {
        runCodesnippets(codesnippetRootAndGlob, additionalCodesnippets, sourcesRootAndGlob, includeSources,
            readmeRootAndGlob, additionalReadmes, includeReadme, maxLineLength, failOnError, ExecutionMode.UPDATE,
            logger);
    }

    private static void runCodesnippets(RootAndGlob codesnippetRootAndGlob, List<RootAndGlob> additionalCodesnippets,
        RootAndGlob sourcesRootAndGlob, boolean includeSources, RootAndGlob readmeRootAndGlob,
        List<RootAndGlob> additionalReadmes, boolean includeReadme, int maxLineLength, boolean failOnError,
        ExecutionMode mode, Log logger) throws IOException, MojoExecutionException {
        // Neither sources nor README is included in the update, there is no work to be done.
        if (!includeSources && !includeReadme) {
            logger.debug("Neither sources or README were included. No codesnippet updating will be done.");
            return;
        }

        // Only get the source files if sources are included in the update.
        List<Path> sourceFiles = Collections.emptyList();
        if (includeSources && sourcesRootAndGlob.rootExists()) {
            // Get the files that match the sources glob and are contained in the sources root directory.
            sourceFiles = sourcesRootAndGlob.globFiles();
        }

        // Only get the README files if READMEs are included in the update.
        List<Path> readmeFiles = new ArrayList<>();
        if (includeReadme) {
            if (readmeRootAndGlob.rootExists()) {
                readmeFiles = readmeRootAndGlob.globFiles();
            }

            for (RootAndGlob additionalReadme : additionalReadmes) {
                readmeFiles.addAll(additionalReadme.globFiles());
            }
        }

        if (sourceFiles.isEmpty() && readmeFiles.isEmpty()) {
            logger.info("No files to update.");
            return;
        }

        // Get the files that match the codesnippet glob and are contained in the codesnippet root directory.
        List<Path> codesnippetFiles = codesnippetRootAndGlob.globFiles();
        for (RootAndGlob additionalCodesnippet : additionalCodesnippets) {
            codesnippetFiles.addAll(additionalCodesnippet.globFiles());
        }

        // scan the sample files for all the snippet files
        Map<String, Codesnippet> foundSnippets = getAllSnippets(codesnippetFiles);

        List<CodesnippetError> errors = new ArrayList<>();

        // Updates all source files.
        for (Path sourcePath : sourceFiles) {
            if (mode == ExecutionMode.UPDATE) {
                errors.addAll(updateSourceCodeSnippets(sourcePath, foundSnippets, maxLineLength));
            } else {
                errors.addAll(verifySourceCodeSnippets(sourcePath, foundSnippets, maxLineLength));
            }
        }

        // Update all README files.
        for (Path readmeFile : readmeFiles) {
            if (mode == ExecutionMode.UPDATE) {
                errors.addAll(updateReadmeCodesnippets(readmeFile, foundSnippets));
            } else {
                errors.addAll(verifyReadmeCodesnippets(readmeFile, foundSnippets));
            }
        }

        if (!errors.isEmpty()) {
            String errorMessage = createErrorMessage((mode == ExecutionMode.UPDATE) ? "updating" : "verifying",
                maxLineLength, errors);
            logger.error(errorMessage);

            if (failOnError) {
                throw new MojoExecutionException(errorMessage);
            }
        }
    }

    static List<CodesnippetError> updateReadmeCodesnippets(Path file, Map<String, Codesnippet> snippetMap)
        throws IOException {
        return updateSnippets(file, SNIPPET_README_CALL_BEGIN, SNIPPET_README_CALL_END, 3, snippetMap, "", "", 0, "",
            Collections.emptyList(), Integer.MAX_VALUE, true);
    }

    static List<CodesnippetError> updateSourceCodeSnippets(Path file, Map<String, Codesnippet> snippetMap,
        int maxLineLength) throws IOException {
        return updateSnippets(file, SNIPPET_SRC_CALL_BEGIN, SNIPPET_SRC_CALL_END, 2, snippetMap, JAVADOC_PRE_FENCE,
            JAVADOC_POST_FENCE, 1, "* ", CODESNIPPET_REPLACEMENTS, maxLineLength, false);
    }

    private static List<CodesnippetError> updateSnippets(Path file, Pattern beginRegex, Pattern endRegex,
        int snippetIdGroup, Map<String, Codesnippet> snippetMap, String preFence, String postFence, int prefixGroupNum,
        String additionalLinePrefix, List<CodesnippetReplacement> replacements, int maxLineLength,
        boolean prependSnippetTagIndentation) throws IOException {
        List<String> lines = Files.readAllLines(file, StandardCharsets.UTF_8);

        List<CodesnippetError> updateErrors = new ArrayList<>();
        List<String> modifiedLines = new ArrayList<>();
        boolean inSnippet = false;
        boolean needsAmend = false;
        String lineSep = System.lineSeparator();
        String currentSnippetId = "";

        int snippetTagIndentation = 0;
        for (String line : lines) {
            Matcher begin = beginRegex.matcher(line);
            Matcher end = endRegex.matcher(line);

            if (begin.matches()) {
                modifiedLines.add(line);
                modifiedLines.add(lineSep);
                currentSnippetId = begin.group(snippetIdGroup);
                if (prependSnippetTagIndentation) {
                    snippetTagIndentation = begin.group(1).length();
                }
                inSnippet = true;
            } else if (end.matches()) {
                if (inSnippet) {
                    Codesnippet newSnippets;
                    if (snippetMap.containsKey(currentSnippetId)) {
                        newSnippets = snippetMap.get(currentSnippetId);
                    } else {
                        updateErrors.add(new CodesnippetMissingError(currentSnippetId, file));
                        needsAmend = true;
                        inSnippet = false;
                        // Even though the snippet is missing, don't break the file.
                        modifiedLines.add(line);
                        modifiedLines.add(lineSep);
                        continue;
                    }

                    List<String> modifiedSnippets = new ArrayList<>();

                    // We use this additional prefix because in src snippet cases we need to pre-space
                    // for readme snippet cases we DON'T need the pre-space at all.
                    String linePrefix = prefixFunction(end, prefixGroupNum, additionalLinePrefix);

                    int longestSnippetLine = 0;
                    StringBuilder snippetIndentationBuilder = new StringBuilder();
                    for (int i = 0; i < snippetTagIndentation; i++) {
                        snippetIndentationBuilder.append(" ");
                    }
                    String snippetIndentation = snippetIndentationBuilder.toString();
                    for (String snippet : respaceLines(newSnippets.getContent())) {
                        longestSnippetLine = Math.max(longestSnippetLine, snippet.length());
                        String modifiedSnippet = applyReplacements(snippet, replacements);
                        modifiedSnippets.add(modifiedSnippet.length() == 0
                            ? END_OF_LINE_WHITESPACES.matcher(linePrefix).replaceAll("") + lineSep
                            : snippetIndentation + linePrefix + modifiedSnippet + lineSep);
                    }

                    if (longestSnippetLine > maxLineLength) {
                        updateErrors.add(new CodesnippetLengthError(currentSnippetId, file, longestSnippetLine));
                    }

                    if (preFence != null && preFence.length() > 0) {
                        modifiedLines.add(linePrefix);
                        modifiedLines.add(preFence);
                        modifiedLines.add(lineSep);
                    }

                    modifiedLines.addAll(modifiedSnippets);

                    if (postFence != null && postFence.length() > 0) {
                        modifiedLines.add(linePrefix);
                        modifiedLines.add(postFence);
                        modifiedLines.add(lineSep);
                    }

                    modifiedLines.add(line);
                    modifiedLines.add(lineSep);
                    needsAmend = true;
                    inSnippet = false;
                } else {
                    // Hit an end code fence without being in a snippet, just append the line.
                    // This can happen in README files with non-Java code fences.
                    modifiedLines.add(line);
                    modifiedLines.add(lineSep);
                }
            } else if (!inSnippet) {
                // Only modify the lines if not in the codesnippet.
                modifiedLines.add(line);
                modifiedLines.add(lineSep);
            }
        }

        if (needsAmend) {
            try (BufferedWriter writer = Files.newBufferedWriter(file, StandardCharsets.UTF_8)) {
                for (String line : modifiedLines) {
                    writer.write(line);
                }
            }
        }

        return updateErrors;
    }

    private static List<CodesnippetError> verifySnippets(Path file, Pattern beginRegex, int snippetIdGroup,
        Pattern endRegex, Map<String, Codesnippet> snippetMap, String preFence, String postFence, int prefixGroupNum,
        String additionalLinePrefix, List<CodesnippetReplacement> replacements, int maxLineLength,
        boolean prependSnippetTagIndentation) throws IOException {
        List<String> lines = Files.readAllLines(file, StandardCharsets.UTF_8);

        boolean inSnippet = false;
        String lineSep = System.lineSeparator();
        List<String> currentSnippetSet = null;
        List<CodesnippetError> verificationErrors = new ArrayList<>();
        String currentSnippetId = "";

        int snippetTagIndentation = 0;
        for (String line : lines) {
            Matcher begin = beginRegex.matcher(line);
            Matcher end = endRegex.matcher(line);

            if (begin.matches()) {
                currentSnippetId = begin.group(snippetIdGroup);
                inSnippet = true;
                if (prependSnippetTagIndentation) {
                    snippetTagIndentation = begin.group(1).length();
                }
                currentSnippetSet = new ArrayList<>();
            } else if (end.matches()) {
                if (inSnippet) {
                    Codesnippet newSnippets;
                    if (snippetMap.containsKey(currentSnippetId)) {
                        newSnippets = snippetMap.get(currentSnippetId);
                    } else {
                        verificationErrors.add(new CodesnippetMissingError(currentSnippetId, file));
                        inSnippet = false;
                        currentSnippetSet = null;
                        continue;
                    }
                    List<String> modifiedSnippets = new ArrayList<>();

                    // We use this additional prefix because in src snippet cases we need to pre-space
                    // for readme snippet cases we DON'T need the pre-space at all.
                    String linePrefix = prefixFunction(end, prefixGroupNum, additionalLinePrefix);

                    int longestSnippetLine = 0;
                    StringBuilder snippetIndentationBuilder = new StringBuilder();
                    for (int i = 0; i < snippetTagIndentation; i++) {
                        snippetIndentationBuilder.append(" ");
                    }
                    String snippetIndentation = snippetIndentationBuilder.toString();
                    for (String snippet : respaceLines(newSnippets.getContent())) {
                        longestSnippetLine = Math.max(longestSnippetLine, snippet.length());
                        String modifiedSnippet = applyReplacements(snippet, replacements);
                        modifiedSnippets.add(modifiedSnippet.length() == 0
                            ? END_OF_LINE_WHITESPACES.matcher(linePrefix).replaceAll("") + lineSep
                            : snippetIndentation + linePrefix + modifiedSnippet + lineSep);
                    }

                    if (longestSnippetLine > maxLineLength) {
                        verificationErrors.add(new CodesnippetLengthError(currentSnippetId, file, longestSnippetLine));
                    }

                    if (!modifiedSnippets.equals(currentSnippetSet)) {
                        verificationErrors.add(new CodesnippetMismatchError(currentSnippetId, file));
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

        return verificationErrors;
    }

    static Map<String, Codesnippet> getAllSnippets(List<Path> snippetSources)
        throws IOException, MojoExecutionException {
        Map<String, List<Codesnippet>> codesnippets = new HashMap<>();

        for (Path samplePath : snippetSources) {
            List<String> fileContent = Files.readAllLines(samplePath, StandardCharsets.UTF_8);
            SnippetDictionary snippetReader = new SnippetDictionary();

            for (String line : fileContent) {
                Matcher begin = SNIPPET_DEF_BEGIN.matcher(line);
                Matcher end = SNIPPET_DEF_END.matcher(line);

                if (begin.matches()) {
                    String id_beginning = begin.group(1);
                    snippetReader.beginSnippet(id_beginning);
                } else if (end.matches()) {
                    String id_ending = end.group(1);
                    List<String> snippetContent = snippetReader.finalizeSnippet(id_ending);
                    codesnippets.compute(id_ending, (key, value) -> {
                        if (value == null) {
                            value = new ArrayList<>();
                        }

                        value.add(new Codesnippet(key, samplePath, snippetContent));
                        return value;
                    });
                } else if (snippetReader.isActive()) {
                    snippetReader.processLine(line);
                }
            }
        }

        String potentialErrorMessage = createDuplicateCodesnippetErrorMessage(codesnippets);
        if (!potentialErrorMessage.isEmpty()) {
            throw new MojoExecutionException(potentialErrorMessage);
        }

        return codesnippets.entrySet().stream()
            .collect(Collectors.toMap(Map.Entry::getKey, entry -> entry.getValue().get(0)));
    }

    private static String createDuplicateCodesnippetErrorMessage(Map<String, List<Codesnippet>> codesnippets) {
        StringBuilder errorMessage = new StringBuilder();

        for (Map.Entry<String, List<Codesnippet>> codesnippetsById : codesnippets.entrySet()) {
            if (codesnippetsById.getValue().size() == 1) {
                continue;
            }

            if (errorMessage.length() == 0) {
                errorMessage.append("Multiple codesnippets used the same identifier:")
                    .append(System.lineSeparator())
                    .append(System.lineSeparator());
            }

            errorMessage.append("Codesnippet ID '")
                .append(codesnippetsById.getKey())
                .append("' was used multiple times. Found in files:")
                .append(System.lineSeparator());

            for (Codesnippet codesnippet : codesnippetsById.getValue()) {
                errorMessage.append("--> ").append(codesnippet.getDefinitionLocation())
                    .append(System.lineSeparator());
            }

            errorMessage.append(System.lineSeparator());
        }

        return errorMessage.toString();
    }

    private static List<String> respaceLines(List<String> snippetText) {
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
            Pattern minWhitespacePattern = Pattern.compile(minWhitespace);
            for (String snippetLine : snippetText) {
                modifiedStrings.add(minWhitespacePattern.matcher(snippetLine).replaceFirst(""));
            }
        }

        return modifiedStrings;
    }

    private static String prefixFunction(Matcher match, int groupNum, String additionalPrefix) {
        // if we pass -1 as the matcher groupNum, we don't want any prefix at all
        if (match == null || groupNum < 1) {
            return "";
        } else {
            return match.group(groupNum) + additionalPrefix;
        }
    }

    private static String applyReplacements(String snippet, List<CodesnippetReplacement> replacements) {
        if (replacements.isEmpty()) {
            return snippet;
        }

        for (CodesnippetReplacement replacement : replacements) {
            snippet = replacement.replaceCodesnippet(snippet);
        }

        return snippet;
    }

    private static String createErrorMessage(String operationKind, int allowedLength, List<CodesnippetError> errors) {
        StringBuilder errorMessageBuilder = new StringBuilder("codesnippet-maven-plugin has encountered errors while ")
            .append(operationKind)
            .append(" codesnippets.")
            .append(System.lineSeparator())
            .append(System.lineSeparator());

        List<String> mismatchErrorMessages = errors.stream()
            .filter(error -> error instanceof CodesnippetMismatchError)
            .map(CodesnippetError::getErrorMessage)
            .collect(Collectors.toList());
        if (!mismatchErrorMessages.isEmpty()) {
            errorMessageBuilder.append("The following codesnippets need updates:").append(System.lineSeparator());
            for (String errorMessage : mismatchErrorMessages) {
                errorMessageBuilder.append(errorMessage).append(System.lineSeparator());
            }
        }

        List<String> missingErrorMessages = errors.stream()
            .filter(error -> error instanceof CodesnippetMissingError)
            .map(CodesnippetError::getErrorMessage)
            .collect(Collectors.toList());
        if (!missingErrorMessages.isEmpty()) {
            errorMessageBuilder.append(System.lineSeparator())
                .append("The following codesnippets were missing:")
                .append(System.lineSeparator());
            for (String errorMessage : missingErrorMessages) {
                errorMessageBuilder.append(errorMessage).append(System.lineSeparator());
            }
        }

        List<String> lengthErrorMessages = errors.stream()
            .filter(error -> error instanceof CodesnippetLengthError)
            .map(CodesnippetError::getErrorMessage)
            .collect(Collectors.toList());
        if (!lengthErrorMessages.isEmpty()) {
            errorMessageBuilder.append(System.lineSeparator())
                .append("The following codesnippets exceeded the allowed length(")
                .append(allowedLength)
                .append("):")
                .append(System.lineSeparator());
            for (String errorMessage : lengthErrorMessages) {
                errorMessageBuilder.append(errorMessage).append(System.lineSeparator());
            }
        }

        return errorMessageBuilder.toString();
    }

    private SnippetReplacer() {
    }
}
