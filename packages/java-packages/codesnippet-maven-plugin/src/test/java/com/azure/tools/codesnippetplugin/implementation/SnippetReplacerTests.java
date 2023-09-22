// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

package com.azure.tools.codesnippetplugin.implementation;

import org.junit.Test;

import java.io.File;
import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.Collections;
import java.util.List;
import java.util.Map;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertNotNull;
import static org.junit.Assert.assertThrows;
import static org.junit.Assert.assertTrue;

public class SnippetReplacerTests {
    private Path getPathToResource(String fileName) {
        File resourcesFile = new File(SnippetReplacerTests.class.getClassLoader().getResource(".").getPath(),
            "project-to-test/");

        return new File(resourcesFile, fileName).toPath();
    }

    @Test
    public void testSrcParse() throws Exception {
        Path testFile = getPathToResource("basic_src_snippet_parse.txt");
        Map<String, Codesnippet> foundSnippets = SnippetReplacer.getAllSnippets(
            Collections.singletonList(testFile.toAbsolutePath()));

        assertEquals(3, foundSnippets.size());
        assertEquals(3, foundSnippets.get("com.azure.data.applicationconfig.configurationclient.instantiation")
            .getContent().size());
        assertEquals(3, foundSnippets.get("com.azure.data.appconfiguration.ConfigurationClient.addConfigurationSetting#String-String-String")
            .getContent().size());
        assertEquals(9, foundSnippets.get("com.azure.core.http.rest.pagedflux.instantiation")
            .getContent().size());
    }

    @Test
    public void testSrcInsertion() throws Exception {
        /*
         * Ensures html encoding, empty and populated snippets replacement
         */
        Path snippetSourceFile = getPathToResource("basic_src_snippet_parse.txt");
        Path codeForReplacement = getPathToResource("basic_src_snippet_insertion_before.txt");
        Path expectedOutCome = getPathToResource("basic_src_snippet_insertion_after.txt");
        String expectedString = new String(Files.readAllBytes(expectedOutCome), StandardCharsets.UTF_8);

        Map<String, Codesnippet> foundSnippets = SnippetReplacer.getAllSnippets(
            new ArrayList<>(Collections.singletonList(snippetSourceFile.toAbsolutePath())));
        List<CodesnippetError> errors = SnippetReplacer.updateSourceCodeSnippets(codeForReplacement, foundSnippets, 120);

        assertTrue(errors.isEmpty());
        assertEquals(expectedString, new String(Files.readAllBytes(codeForReplacement), StandardCharsets.UTF_8));
    }

    @Test
    public void testReadmeInsertion() throws Exception {
        /*
            Ensures html encoding, empty and populated snippets replacement
         */
        Path snippetSourceFile = getPathToResource("basic_src_snippet_parse.txt");
        Path codeForReplacement = getPathToResource("basic_readme_insertion_before.txt");
        Path expectedOutCome = getPathToResource("basic_readme_insertion_after.txt");
        String expectedString = new String(Files.readAllBytes(expectedOutCome), StandardCharsets.UTF_8);

        Map<String, Codesnippet> foundSnippets = SnippetReplacer.getAllSnippets(
            new ArrayList<>(Collections.singletonList(snippetSourceFile.toAbsolutePath())));
        List<CodesnippetError> errors = SnippetReplacer.updateReadmeCodesnippets(codeForReplacement, foundSnippets);

        assertTrue(errors.isEmpty());
        assertEquals(expectedString, new String(Files.readAllBytes(codeForReplacement), StandardCharsets.UTF_8));
    }

    @Test
    public void testReadmeVerification() throws Exception {
        Path snippetSourceFile = getPathToResource("basic_src_snippet_parse.txt");
        Path verification = getPathToResource("readme_insertion_verification_failure.txt");

        Map<String, Codesnippet> foundSnippets = SnippetReplacer.getAllSnippets(
            Collections.singletonList(snippetSourceFile.toAbsolutePath()));
        List<CodesnippetError> errors = SnippetReplacer.verifyReadmeCodesnippets(verification, foundSnippets);

        assertNotNull(errors);
        assertEquals(1, errors.size());
        assertEquals("com.azure.core.http.rest.pagedflux.instantiation", errors.get(0).getSnippetId());
    }

    @Test
    public void testSrcVerification() throws Exception {
        Path snippetSourceFile = getPathToResource("basic_src_snippet_parse.txt");
        Path verification = getPathToResource("src_insertion_verification_failure.txt");

        Map<String, Codesnippet> foundSnippets = SnippetReplacer.getAllSnippets(
            new ArrayList<>(Collections.singletonList(snippetSourceFile.toAbsolutePath())));
        List<CodesnippetError> errors = SnippetReplacer.verifySourceCodeSnippets(verification, foundSnippets, 120);

        assertNotNull(errors);
        assertEquals(2, errors.size());
        assertEquals("com.azure.data.applicationconfig.configurationclient.instantiation",
            errors.get(0).getSnippetId());
        assertEquals("com.azure.core.http.rest.pagedflux.instantiation", errors.get(1).getSnippetId());
    }

    @Test
    public void emptySnippetWorks() throws Exception {
        Path single = getPathToResource("empty_snippet_def.txt");

        List<Path> srcs = new ArrayList<>(Collections.singletonList(single.toAbsolutePath()));
        Map<String, Codesnippet> foundSnippets = SnippetReplacer.getAllSnippets(srcs);

        assertEquals(1, foundSnippets.keySet().size());
        assertTrue(foundSnippets.containsKey("com.azure.data.applicationconfig.configurationclient.testEmpty"));
    }

    @Test
    public void duplicateSnippetsCrashWithSingleFile() {
        Path single = getPathToResource("duplicate_snippet_src.txt");

        List<Path> srcs = new ArrayList<>(Collections.singletonList(single.toAbsolutePath()));

        Exception e = assertThrows(Exception.class, () -> SnippetReplacer.getAllSnippets(srcs));

        // check for snippet id in string
        assertTrue(e.toString().contains("com.azure.data.applicationconfig.configurationclient.instantiation"));
    }

    @Test
    public void duplicateSnippetsCrashWithMultipleFiles() {
        Path single = getPathToResource("duplicate_snippet_src.txt");
        Path multiple = getPathToResource("duplicate_snippet_src_multiple.txt");

        List<Path> srcs = new ArrayList<>(Arrays.asList(single.toAbsolutePath(), multiple.toAbsolutePath()));

        Exception e = assertThrows(Exception.class, () -> SnippetReplacer.getAllSnippets(srcs));

        // check for snippet id in string
        assertTrue(e.getMessage().contains("com.azure.data.applicationconfig.configurationclient.instantiation"));
    }

    @Test
    public void notFoundSnippetCrashes() throws IOException {
        Path codeForReplacement = getPathToResource("basic_src_snippet_insertion_before.txt");

        List<CodesnippetError> errors = SnippetReplacer.updateSourceCodeSnippets(codeForReplacement,
            Collections.emptyMap(), 120);

        assertNotNull(errors);
        assertEquals(3, errors.size());
    }

    /**
     * Tests that when a README has a code fence without a snippet declaration the code fence isn't mutated in any way.
     */
    @Test
    public void readmeCodeFenceNoSnippet() throws Exception {
        Path snippetSourceFile = getPathToResource("basic_src_snippet_parse.txt");
        Path codeForReplacement = getPathToResource("readme_code_fence_no_snippet_before.txt");
        Path expectedOutCome = getPathToResource("readme_code_fence_no_snippet_after.txt");
        String expectedString = new String(Files.readAllBytes(expectedOutCome), StandardCharsets.UTF_8);

        Map<String, Codesnippet> foundSnippets = SnippetReplacer.getAllSnippets(
            new ArrayList<>(Collections.singletonList(snippetSourceFile.toAbsolutePath())));
        List<CodesnippetError> errors = SnippetReplacer.updateReadmeCodesnippets(codeForReplacement, foundSnippets);

        assertTrue(errors.isEmpty());
        assertEquals(expectedString, new String(Files.readAllBytes(codeForReplacement), StandardCharsets.UTF_8));
    }

    @Test
    public void fullClassCodesnippet() throws Exception {
        Path snippetSourceFile = getPathToResource("full_class_parse.txt");
        Path codeForReplacement = getPathToResource("full_class_readme_insertion_before.txt");
        Path expectedOutCome = getPathToResource("full_class_readme_insertion_after.txt");
        String expectedString = new String(Files.readAllBytes(expectedOutCome), StandardCharsets.UTF_8);

        Map<String, Codesnippet> foundSnippets = SnippetReplacer.getAllSnippets(
            new ArrayList<>(Collections.singletonList(snippetSourceFile.toAbsolutePath())));
        List<CodesnippetError> errors = SnippetReplacer.updateReadmeCodesnippets(codeForReplacement, foundSnippets);

        assertTrue(errors.isEmpty());
        assertEquals(expectedString, new String(Files.readAllBytes(codeForReplacement), StandardCharsets.UTF_8));
    }
}
