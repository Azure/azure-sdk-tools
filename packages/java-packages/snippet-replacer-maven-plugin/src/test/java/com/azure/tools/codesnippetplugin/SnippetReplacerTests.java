package com.azure.tools.codesnippetplugin;

import org.junit.Test;

import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.Paths;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.Collections;
import java.util.HashMap;
import java.util.List;
import java.util.Map;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertNotNull;
import static org.junit.Assert.assertThrows;
import static org.junit.Assert.assertTrue;

public class SnippetReplacerTests {
    private static final String TEST_RESOURCES_DIRECTORY = "../../../../project-to-test/";

    private Path getPathToResource(String fileName) {
        String relativeLocation = TEST_RESOURCES_DIRECTORY + fileName;
        String pathToTestFile = SnippetReplacerTests.class.getResource(relativeLocation).getPath();

        if (pathToTestFile.startsWith("/")) {
            pathToTestFile = pathToTestFile.substring(1);
        }

        return Paths.get(pathToTestFile);
    }

    @Test
    public void testSrcParse() throws Exception {
        Path testFile = getPathToResource("basic_src_snippet_parse.txt");
        Map<String, List<String>> foundSnippets = SnippetReplacer.getAllSnippets(
            new ArrayList<>(Collections.singletonList(testFile.toAbsolutePath())));

        assertEquals(3, foundSnippets.size());
        assertEquals(3, foundSnippets.get("com.azure.data.applicationconfig.configurationclient.instantiation").size());
        assertEquals(3, foundSnippets.get("com.azure.data.appconfiguration.ConfigurationClient.addConfigurationSetting#String-String-String").size());
        assertEquals(9, foundSnippets.get("com.azure.core.http.rest.pagedflux.instantiation").size());
    }

    @Test
    public void testSrcInsertion() throws Exception {
        /*
         * Ensures html encoding, empty and populated snippets replacement
         */
        Path snippetSourceFile = getPathToResource("basic_src_snippet_parse.txt");
        Path codeForReplacement = getPathToResource("basic_src_snippet_insertion_before.txt");
        Path expectedOutCome = getPathToResource("basic_src_snippet_insertion_after.txt");
        byte[] rawBytes = Files.readAllBytes(expectedOutCome);
        String expectedString = new String(rawBytes, StandardCharsets.UTF_8);

        Map<String, List<String>> foundSnippets = SnippetReplacer.getAllSnippets(
            new ArrayList<>(Collections.singletonList(snippetSourceFile.toAbsolutePath())));
        SnippetOperationResult<StringBuilder> opResult = SnippetReplacer.updateSnippets(codeForReplacement,
            SnippetReplacer.SNIPPET_SRC_CALL_BEGIN, SnippetReplacer.SNIPPET_SRC_CALL_END, foundSnippets, "<pre>",
            "</pre>", 1, "* ", false);

        assertNotNull(opResult);
        assertNotNull(opResult.result);
        assertEquals(opResult.result.toString(), expectedString);
    }

    @Test
    public void testReadmeInsertion()
        throws Exception {
        /*
            Ensures html encoding, empty and populated snippets replacement
         */
        Path snippetSourceFile = getPathToResource("basic_src_snippet_parse.txt");
        Path codeForReplacement = getPathToResource("basic_readme_insertion_before.txt");
        Path expectedOutCome = getPathToResource("basic_readme_insertion_after.txt");
        byte[] rawBytes = Files.readAllBytes(expectedOutCome);
        String expectedString = new String(rawBytes, StandardCharsets.UTF_8);

        Map<String, List<String>> foundSnippets = SnippetReplacer.getAllSnippets(
            new ArrayList<>(Collections.singletonList(snippetSourceFile.toAbsolutePath())));
        SnippetOperationResult<StringBuilder> opResult = SnippetReplacer.updateSnippets(codeForReplacement,
            SnippetReplacer.SNIPPET_README_CALL_BEGIN, SnippetReplacer.SNIPPET_README_CALL_END, foundSnippets, "", "",
            0, "", true);

        assertNotNull(opResult);
        assertNotNull(opResult.result);
        assertEquals(opResult.result.toString(), expectedString);
    }

    @Test
    public void testReadmeVerification() throws Exception {
        Path snippetSourceFile = getPathToResource("basic_src_snippet_parse.txt");
        Path verification = getPathToResource("readme_insertion_verification_failure.txt");

        Map<String, List<String>> foundSnippets = SnippetReplacer.getAllSnippets(
            new ArrayList<>(Collections.singletonList(snippetSourceFile.toAbsolutePath())));
        SnippetOperationResult<List<VerifyResult>> opResult = SnippetReplacer.verifySnippets(verification,
            SnippetReplacer.SNIPPET_README_CALL_BEGIN, SnippetReplacer.SNIPPET_README_CALL_END, foundSnippets,
            "", "", 0, "", true);

        assertNotNull(opResult.result);
        assertEquals(1, opResult.result.size());
        assertEquals("com.azure.core.http.rest.pagedflux.instantiation", opResult.result.get(0).snippetWithIssues);
    }

    @Test
    public void testSrcVerification() throws Exception {
        Path snippetSourceFile = getPathToResource("basic_src_snippet_parse.txt");
        Path verification = getPathToResource("src_insertion_verification_failure.txt");

        Map<String, List<String>> foundSnippets = SnippetReplacer.getAllSnippets(
            new ArrayList<>(Collections.singletonList(snippetSourceFile.toAbsolutePath())));
        SnippetOperationResult<List<VerifyResult>> opResult = SnippetReplacer.verifySnippets(verification,
            SnippetReplacer.SNIPPET_SRC_CALL_BEGIN, SnippetReplacer.SNIPPET_SRC_CALL_END, foundSnippets,
            "<pre>", "</pre>", 1, "* ", false);

        assertNotNull(opResult.result);
        assertEquals(2, opResult.result.size());
        assertEquals("com.azure.data.applicationconfig.configurationclient.instantiation", opResult.result.get(0).snippetWithIssues);
        assertEquals("com.azure.core.http.rest.pagedflux.instantiation", opResult.result.get(1).snippetWithIssues);
    }

    @Test
    public void emptySnippetWorks() throws Exception {
        Path single = getPathToResource("empty_snippet_def.txt");

        List<Path> srcs = new ArrayList<>(Collections.singletonList(single.toAbsolutePath()));
        Map<String, List<String>> foundSnippets = SnippetReplacer.getAllSnippets(srcs);

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
        assertTrue(e.toString().contains("com.azure.data.applicationconfig.configurationclient.instantiation"));
        // should be one duplicate message from each file
        assertEquals(2, (e.toString().split("Duplicate snippetId", -1).length - 1));
    }

    @Test
    public void notFoundSnippetCrashes() throws IOException {
        HashMap<String, List<String>> emptyMap = new HashMap<>();

        Path codeForReplacement = getPathToResource("basic_src_snippet_insertion_before.txt");

        SnippetOperationResult<StringBuilder> opResult = SnippetReplacer.updateSnippets(codeForReplacement,
            SnippetReplacer.SNIPPET_SRC_CALL_BEGIN, SnippetReplacer.SNIPPET_SRC_CALL_END, emptyMap, "<pre>", "</pre>",
            1, "* ", false);

        assertNotNull(opResult);
        assertEquals(3, opResult.errorList.size());
    }
}
