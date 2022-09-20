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
import java.util.BitSet;
import java.util.Collections;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.function.BiFunction;
import java.util.stream.Collectors;

public final class SnippetReplacer {
    private static final String JAVADOC_PRE_FENCE = "<pre>";
    private static final String JAVADOC_POST_FENCE = "</pre>";

    static final BitSet VALID_SNIPPET_ID_CHARACTER;
    static final String[] JAVADOC_CODESNIPPET_REPLACEMENTS;

    static {
        JAVADOC_CODESNIPPET_REPLACEMENTS = new String[256];
        JAVADOC_CODESNIPPET_REPLACEMENTS['&'] = "&amp;";
        JAVADOC_CODESNIPPET_REPLACEMENTS['\"'] = "&quot;";
        JAVADOC_CODESNIPPET_REPLACEMENTS['>'] = "&gt;";
        JAVADOC_CODESNIPPET_REPLACEMENTS['<'] = "&lt;";
        JAVADOC_CODESNIPPET_REPLACEMENTS['@'] = "&#64;";
        JAVADOC_CODESNIPPET_REPLACEMENTS['{'] = "&#123;";
        JAVADOC_CODESNIPPET_REPLACEMENTS['}'] = "&#125;";
        JAVADOC_CODESNIPPET_REPLACEMENTS['('] = "&#40;";
        JAVADOC_CODESNIPPET_REPLACEMENTS[')'] = "&#41;";
        JAVADOC_CODESNIPPET_REPLACEMENTS['/'] = "&#47;";
        JAVADOC_CODESNIPPET_REPLACEMENTS['\\'] = "&#92;";

        VALID_SNIPPET_ID_CHARACTER = new BitSet(256);
        VALID_SNIPPET_ID_CHARACTER.set('a', 'z' + 1);
        VALID_SNIPPET_ID_CHARACTER.set('A', 'Z' + 1);
        VALID_SNIPPET_ID_CHARACTER.set('0', '9' + 1);
        VALID_SNIPPET_ID_CHARACTER.set('.');
        VALID_SNIPPET_ID_CHARACTER.set('#');
        VALID_SNIPPET_ID_CHARACTER.set('\\');
        VALID_SNIPPET_ID_CHARACTER.set('-');
        VALID_SNIPPET_ID_CHARACTER.set('_');
    }

    /**
     * The "verification" operation encapsulated by this function is as follows.
     * <p>
     * 1. Scan under the target direction for all discovered code snippet DEFINITIONS 2. Examine all snippet CALLS,
     * finding where updates are needed. 3. Report all discovered snippets in need of update as well as all bad snippet
     * calls
     * <p>
     * A "bad snippet call" is simply calling for a snippet whose ID has no definition.
     * <p>
     * See
     * {@link #updateCodesnippets(RootAndGlob, List, RootAndGlob, boolean, RootAndGlob, List, boolean, int, boolean,
     * Log)} for details on actually defining and calling snippets.
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
        return verifySnippets(file, SnippetReplacer::getReadmeCall, snippetMap, "", "", "", null, 2048, true);
    }

    static List<CodesnippetError> verifySourceCodeSnippets(Path file, Map<String, Codesnippet> snippetMap,
        int maxLineLength) throws IOException {
        return verifySnippets(file, SnippetReplacer::getSourceCall, snippetMap, JAVADOC_PRE_FENCE, JAVADOC_POST_FENCE,
            "* ", JAVADOC_CODESNIPPET_REPLACEMENTS, maxLineLength, false);
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
        return updateSnippets(file, SnippetReplacer::getReadmeCall, snippetMap, "", "", "", null, 2048, true);
    }

    static List<CodesnippetError> updateSourceCodeSnippets(Path file, Map<String, Codesnippet> snippetMap,
        int maxLineLength) throws IOException {
        return updateSnippets(file, SnippetReplacer::getSourceCall, snippetMap, JAVADOC_PRE_FENCE,
            JAVADOC_POST_FENCE, "* ", JAVADOC_CODESNIPPET_REPLACEMENTS, maxLineLength, false);
    }

    private static List<CodesnippetError> updateSnippets(Path file,
        BiFunction<String, Boolean, SnippetInfo> snippetTagMatcher, Map<String, Codesnippet> snippetMap,
        String preFence, String postFence, String additionalLinePrefix, String[] replacements, int maxLineLength,
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
            SnippetInfo begin = snippetTagMatcher.apply(line, true);
            if (begin != null) {
                modifiedLines.add(line);
                modifiedLines.add(lineSep);
                currentSnippetId = begin.snippetId;
                if (prependSnippetTagIndentation) {
                    snippetTagIndentation = begin.leadingWhitespace;
                }
                inSnippet = true;

                continue;
            }

            SnippetInfo end = snippetTagMatcher.apply(line, false);
            if (end != null) {
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
                    String linePrefix = prefixFunction(end, additionalLinePrefix);

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
                            ? stripTrailingWhitespace(linePrefix) + lineSep
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

    private static List<CodesnippetError> verifySnippets(Path file,
        BiFunction<String, Boolean, SnippetInfo> snippetTagMatcher, Map<String, Codesnippet> snippetMap,
        String preFence, String postFence, String additionalLinePrefix, String[] replacements, int maxLineLength,
        boolean prependSnippetTagIndentation) throws IOException {
        List<String> lines = Files.readAllLines(file, StandardCharsets.UTF_8);

        boolean inSnippet = false;
        String lineSep = System.lineSeparator();
        List<String> currentSnippetSet = null;
        List<CodesnippetError> verificationErrors = new ArrayList<>();
        String currentSnippetId = "";

        int snippetTagIndentation = 0;
        for (String line : lines) {
            SnippetInfo begin = snippetTagMatcher.apply(line, true);
            if (begin != null) {
                currentSnippetId = begin.snippetId;
                inSnippet = true;
                if (prependSnippetTagIndentation) {
                    snippetTagIndentation = begin.leadingWhitespace;
                }
                currentSnippetSet = new ArrayList<>();
                continue;
            }

            SnippetInfo end = snippetTagMatcher.apply(line, false);
            if (end != null) {
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
                    String linePrefix = prefixFunction(end, additionalLinePrefix);

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
                            ? stripTrailingWhitespace(linePrefix) + lineSep
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
        Map<String, List<String>> missingBeginTag = new HashMap<>();
        Map<String, List<String>> missingEndTag = new HashMap<>();

        for (Path samplePath : snippetSources) {
            List<String> fileContent = Files.readAllLines(samplePath, StandardCharsets.UTF_8);
            SnippetDictionary snippetReader = new SnippetDictionary();

            for (String line : fileContent) {
                SnippetInfo begin = getSnippetDefinition(line, true);
                if (begin != null) {
                    String id_beginning = begin.snippetId;
                    snippetReader.beginSnippet(id_beginning);
                    continue;
                }

                SnippetInfo end = getSnippetDefinition(line, false);
                if (end != null) {
                    String id_ending = end.snippetId;
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

            if (!snippetReader.getMissingEndTags().isEmpty()) {
                missingEndTag.put(samplePath.toString(), snippetReader.getMissingEndTags());
            }

            if (!snippetReader.getMissingBeginTags().isEmpty()) {
                missingBeginTag.put(samplePath.toString(), snippetReader.getMissingBeginTags());
            }
        }

        String potentialErrorMessage = createInvalidSnippetsErrorMessage(codesnippets, missingEndTag, missingBeginTag);
        if (!potentialErrorMessage.isEmpty()) {
            throw new MojoExecutionException(potentialErrorMessage);
        }

        return codesnippets.entrySet().stream()
            .collect(Collectors.toMap(Map.Entry::getKey, entry -> entry.getValue().get(0)));
    }

    private static String createInvalidSnippetsErrorMessage(Map<String, List<Codesnippet>> codesnippets,
        Map<String, List<String>> missingEndTags, Map<String, List<String>> missingBeginTags) {
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

        for (Map.Entry<String, List<String>> missingEndTag : missingEndTags.entrySet()) {
            errorMessage.append("The following codesnippet aliases in file' ")
                .append(missingEndTag.getKey())
                .append("' didn't have a matching END alias:")
                .append(System.lineSeparator());

            for (String alias : missingEndTag.getValue()) {
                errorMessage.append(" - ").append(alias).append(System.lineSeparator());
            }

            errorMessage.append(System.lineSeparator());
        }

        for (Map.Entry<String, List<String>> missingBeginTag : missingBeginTags.entrySet()) {
            errorMessage.append("The following codesnippet aliases in file '")
                .append(missingBeginTag.getKey())
                .append("' didn't have a matching BEGIN alias:")
                .append(System.lineSeparator());

            for (String alias : missingBeginTag.getValue()) {
                errorMessage.append(" - ").append(alias).append(System.lineSeparator());
            }

            errorMessage.append(System.lineSeparator());
        }

        return errorMessage.toString();
    }

    private static List<String> respaceLines(List<String> snippetText) {
        // get List of all the leading whitespace in the sample
        // toss out lines that are empty (as they shouldn't mess with the minimum)
        int minWhitespace = Integer.MAX_VALUE;
        List<String> modifiedStrings = new ArrayList<>();

        for (String snippetLine : snippetText) {
            // only look at non-whitespace only strings for the min indent
            if (snippetLine.trim().length() == 0) {
                continue;
            }

            minWhitespace = Math.min(minWhitespace, leadingWhitespaceCount(snippetLine));
            if (minWhitespace == 0) {
                break;
            }
        }

        if (minWhitespace > 0) {
            for (String snippetLine : snippetText) {
                if (snippetLine.length() >= minWhitespace) {
                    modifiedStrings.add(snippetLine.substring(minWhitespace));
                } else {
                    modifiedStrings.add(snippetLine);
                }
            }
        } else {
            return snippetText;
        }

        return modifiedStrings;
    }

    private static String prefixFunction(SnippetInfo snippetInfo, String additionalPrefix) {
        // if we pass -1 as the matcher groupNum, we don't want any prefix at all
        if (snippetInfo == null || !snippetInfo.additionalPrefix) {
            return "";
        } else {
            return snippetInfo.additionalPrefixString + additionalPrefix;
        }
    }

    static String applyReplacements(String snippet, String[] replacements) {
        if (replacements == null || replacements.length == 0) {
            return snippet;
        }

        int snippetLength = snippet.length();
        StringBuilder replacer = null;
        int prevStart = 0;

        for (int i = 0; i < snippetLength; i++) {
            char c = snippet.charAt(i);
            if (c >= 256) {
                continue;
            }

            String replacement = replacements[c];
            if (replacement != null) {
                if (replacer == null) {
                    // 500 is used as the largest replacement is 6 characters so this is expecting 100 replacements
                    // (6 - 1) * 100 = 500
                    replacer = new StringBuilder(snippet.length() + 500);
                }

                if (prevStart != i) {
                    replacer.append(snippet, prevStart, i);
                }
                replacer.append(replacement);

                prevStart = i + 1;
            }
        }

        if (replacer == null) {
            return snippet;
        }

        replacer.append(snippet, prevStart, snippet.length());

        return replacer.toString();
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

    private static int leadingWhitespaceCount(String str) {
        int count = nextNonWhitespace(str, 0);
        return (count == -1) ? 0 : count;
    }

    private static int nextNonWhitespace(String str, int offset) {
        if (str == null || str.length() == 0 || str.length() - 1 == offset) {
            return -1;
        }

        int length = str.length();
        while (offset < length) {
            if (!Character.isWhitespace(str.charAt(offset))) {
                return offset;
            }

            offset++;
        }

        return -1;
    }

    private static String stripTrailingWhitespace(String str) {
        if (str == null || str.length() == 0) {
            return str;
        }

        int end = str.length() - 1;
        while (end > 0) {
            if (!Character.isWhitespace(str.charAt(end))) {
                break;
            }

            end--;
        }

        return str.substring(0, end + 1);
    }

    private static String getSnippetId(String str, int offset) {
        if (str == null || str.isEmpty() || str.length() - 1 == offset) {
            return null;
        }

        int strLength = str.length();
        int start = nextNonWhitespace(str, offset);
        if (start == -1) {
            return null;
        }

        int end = start;
        for (; end < strLength; end++) {
            char c = str.charAt(end);
            if (!VALID_SNIPPET_ID_CHARACTER.get(c)) {
                if (!Character.isWhitespace(c)) {
                    return null;
                } else {
                    return str.substring(start, end);
                }
            }
        }

        return str.substring(start);
    }

    private static SnippetInfo getSnippetDefinition(String str, boolean beginDefinition) {
        if (str == null || str.length() == 0) {
            return null;
        }

        int leadingWhitespace = leadingWhitespaceCount(str);
        int offset = leadingWhitespace;

        if (!str.regionMatches(offset, "//", 0, 2)) {
            return null;
        }

        offset = nextNonWhitespace(str, offset + 2);

        if (offset == -1) {
            return null;
        } else if (beginDefinition) {
            if (!str.regionMatches(offset, "BEGIN:", 0, 6)) {
                return null;
            }
        } else if (!str.regionMatches(offset, "END:", 0, 4)) {
            return null;
        }

        String snippetId = getSnippetId(str, beginDefinition ? offset + 6 : offset + 4);
        return snippetId == null ? null : new SnippetInfo(leadingWhitespace, snippetId, false);
    }

    private static SnippetInfo getSourceCall(String str, boolean beginSourceCall) {
        if (str == null || str.length() == 0) {
            return null;
        }

        int leadingWhitespace = leadingWhitespaceCount(str);
        int offset = leadingWhitespace;

        if (str.charAt(offset) != '*') {
            return null;
        }

        offset = nextNonWhitespace(str, offset + 1);

        if (offset == -1) {
            return null;
        } else if (!str.regionMatches(offset, "<!--", 0, 4)) {
            return null;
        }

        offset = nextNonWhitespace(str, offset + 4);

        if (offset == -1) {
            return null;
        } else if (beginSourceCall) {
            if (!str.regionMatches(offset, "src_embed", 0, 9)) {
                return null;
            }
        } else if (!str.regionMatches(offset, "end", 0, 3)) {
            return null;
        }

        offset = beginSourceCall ? offset + 9 : offset + 3;
        String snippetId = getSnippetId(str, offset);
        if (snippetId == null) {
            return null;
        }

        offset = nextNonWhitespace(str, offset + snippetId.length() + 1);
        return !str.regionMatches(offset, "-->", 0, 3) ? null : new SnippetInfo(leadingWhitespace, snippetId, true);
    }

    private static SnippetInfo getReadmeCall(String str, boolean beginReadmeCall) {
        if (str == null || str.length() == 0) {
            return null;
        }

        int leadingWhitespace = leadingWhitespaceCount(str);
        int offset = leadingWhitespace;

        if (!str.regionMatches(offset, "```", 0, 3)) {
            return null;
        }

        if (!beginReadmeCall) {
            // README ends are special where they don't have the snippet ID.
            return new SnippetInfo(leadingWhitespace, null, false);
        }

        offset = nextNonWhitespace(str, offset + 3);

        if (offset == -1) {
            return null;
        } else if (!str.regionMatches(offset, "java", 0, 4)) {
            return null;
        }

        String snippetId = getSnippetId(str, offset + 4);
        return snippetId == null ? null : new SnippetInfo(leadingWhitespace, snippetId, false);
    }

    private static final class SnippetInfo {
        private final int leadingWhitespace;
        private final String snippetId;
        private final boolean additionalPrefix;
        private final String additionalPrefixString;

        SnippetInfo(int leadingWhitespace, String snippetId, boolean additionalPrefix) {
            this.leadingWhitespace = leadingWhitespace;
            this.snippetId = snippetId;
            this.additionalPrefix = additionalPrefix;
            if (additionalPrefix) {
                StringBuilder prefixBuilder = new StringBuilder(leadingWhitespace);
                for (int i = 0; i < leadingWhitespace; i++) {
                    prefixBuilder.append(" ");
                }
                additionalPrefixString = prefixBuilder.toString();
            } else {
                additionalPrefixString = null;
            }
        }
    }

    private SnippetReplacer() {
    }
}
