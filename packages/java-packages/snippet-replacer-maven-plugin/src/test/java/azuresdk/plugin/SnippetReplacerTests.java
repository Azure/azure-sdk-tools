package azuresdk.plugin;

import static org.junit.Assert.*;

import org.apache.maven.plugin.MojoExecutionException;
import org.junit.Test;

import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.Paths;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.HashMap;
import java.util.List;

public class SnippetReplacerTests {

    private Path _getPathToResource(String relativeLocation){
        String pathToTestFile = SnippetReplacerTests.class.getResource(relativeLocation).getPath();

        if(pathToTestFile.startsWith("/")){
            pathToTestFile = pathToTestFile.substring(1);
        }

        return Paths.get(pathToTestFile);
    }

    @Test
    public void testSrcParse()
            throws Exception
    {
        Path testFile = _getPathToResource("../../project-to-test/basic_src_snippet_parse.txt");
        HashMap<String, List<String>> foundSnippets = new SnippetReplacer().getAllSnippets(
                new ArrayList<Path>(Arrays.asList(testFile.toAbsolutePath())));

        assertTrue(foundSnippets.size() == 3);
        assertTrue(
                foundSnippets.get("com.azure.data.applicationconfig.configurationclient.instantiation").size() == 3);
        assertTrue(
                foundSnippets.get("com.azure.data.appconfiguration.ConfigurationClient.addConfigurationSetting#String-String-String").size() == 3);
        assertTrue(foundSnippets.get("com.azure.core.http.rest.pagedflux.instantiation").size() == 9);
    }

    @Test
    public void testSrcInsertion()
            throws Exception
    {
        /*
            Ensures html encoding, empty and populated snippets replacement
         */
        Path snippetSourceFile = _getPathToResource("../../project-to-test/basic_src_snippet_parse.txt");
        Path codeForReplacement = _getPathToResource("../../project-to-test/basic_src_snippet_insertion_before.txt");
        Path expectedOutCome = _getPathToResource("../../project-to-test/basic_src_snippet_insertion_after.txt");
        SnippetReplacer testReplacer =  new SnippetReplacer();
        List<String> testLines = Files.readAllLines(codeForReplacement, StandardCharsets.UTF_8);
        byte[] rawBytes = Files.readAllBytes(expectedOutCome);
        String expectedString = new String(rawBytes, StandardCharsets.UTF_8);

        HashMap<String, List<String>> foundSnippets = testReplacer.getAllSnippets(
                new ArrayList<Path>(Arrays.asList(snippetSourceFile.toAbsolutePath())));
        SnippetOperationResult<StringBuilder> opResult = testReplacer.updateSnippets(codeForReplacement, testLines,
                testReplacer.SNIPPET_SRC_CALL_BEGIN, testReplacer.SNIPPET_SRC_CALL_END, foundSnippets,
                "<pre>", "</pre>", 1, "* ", false);

        assertTrue(opResult.result != null);
        assertEquals(opResult.result.toString(),expectedString);
    }

    @Test
    public void testReadmeInsertion()
            throws Exception {
        /*
            Ensures html encoding, empty and populated snippets replacement
         */
        Path snippetSourceFile = _getPathToResource("../../project-to-test/basic_src_snippet_parse.txt");
        Path codeForReplacement = _getPathToResource("../../project-to-test/basic_readme_insertion_before.txt");
        Path expectedOutCome = _getPathToResource("../../project-to-test/basic_readme_insertion_after.txt");
        SnippetReplacer testReplacer =  new SnippetReplacer();
        List<String> testLines = Files.readAllLines(codeForReplacement, StandardCharsets.UTF_8);
        byte[] rawBytes = Files.readAllBytes(expectedOutCome);
        String expectedString = new String(rawBytes, StandardCharsets.UTF_8);

        HashMap<String, List<String>> foundSnippets = testReplacer.getAllSnippets(
                new ArrayList<Path>(Arrays.asList(snippetSourceFile.toAbsolutePath())));
        SnippetOperationResult<StringBuilder> opResult = testReplacer.updateSnippets(codeForReplacement, testLines,
                testReplacer.SNIPPET_README_CALL_BEGIN, testReplacer.SNIPPET_README_CALL_END, foundSnippets,
                "", "", 0, "", true);

        assertTrue(opResult.result != null);
        assertTrue(opResult.result.toString().equals(expectedString));
    }

    @Test
    public void testReadmeVerification() throws Exception {
        Path snippetSourceFile = _getPathToResource("../../project-to-test/basic_src_snippet_parse.txt");
        Path verification = _getPathToResource("../../project-to-test/readme_insertion_verification_failure.txt");
        SnippetReplacer testReplacer =  new SnippetReplacer();
        List<String> testLines = Files.readAllLines(verification, StandardCharsets.UTF_8);

        HashMap<String, List<String>> foundSnippets = testReplacer.getAllSnippets(
                new ArrayList<Path>(Arrays.asList(snippetSourceFile.toAbsolutePath())));
        SnippetOperationResult<List<VerifyResult>> opResult = testReplacer.verifySnippets(verification, testLines,
                testReplacer.SNIPPET_README_CALL_BEGIN, testReplacer.SNIPPET_README_CALL_END, foundSnippets,
                "", "", 0, "", true);

        assertTrue(opResult.result != null);
        assertTrue(opResult.result.size() == 1);
        assertTrue(opResult.result.get(0).SnippetWithIssues.equals("com.azure.core.http.rest.pagedflux.instantiation"));
    }

    @Test
    public void testSrcVerification() throws Exception {
        Path snippetSourceFile = _getPathToResource("../../project-to-test/basic_src_snippet_parse.txt");
        Path verification = _getPathToResource("../../project-to-test/src_insertion_verification_failure.txt");
        SnippetReplacer testReplacer =  new SnippetReplacer();
        List<String> testLines = Files.readAllLines(verification, StandardCharsets.UTF_8);

        HashMap<String, List<String>> foundSnippets = testReplacer.getAllSnippets(
                new ArrayList<Path>(Arrays.asList(snippetSourceFile.toAbsolutePath())));
        SnippetOperationResult<List<VerifyResult>> opResult = testReplacer.verifySnippets(verification, testLines,
                testReplacer.SNIPPET_SRC_CALL_BEGIN, testReplacer.SNIPPET_SRC_CALL_END, foundSnippets,
                "<pre>", "</pre>", 1, "* ", false);

        assertTrue(opResult.result != null);
        assertTrue(opResult.result.size() == 2);
        assertTrue(opResult.result.get(0).SnippetWithIssues.equals("com.azure.data.applicationconfig.configurationclient.instantiation"));
        assertTrue(opResult.result.get(1).SnippetWithIssues.equals("com.azure.core.http.rest.pagedflux.instantiation"));
    }

    @Test
    public void emptySnippetWorks() throws Exception {
        Path single = _getPathToResource("../../project-to-test/empty_snippet_def.txt");

        List<Path> srcs = new ArrayList<Path>(Arrays.asList(single.toAbsolutePath()));
        HashMap<String, List<String>> foundSnippets = new SnippetReplacer().getAllSnippets(srcs);

        assertTrue(foundSnippets.keySet().size() == 1);
        assertTrue(foundSnippets.containsKey("com.azure.data.applicationconfig.configurationclient.testEmpty"));
    }

    @Test
    public void duplicateSnippetsCrashWithSingleFile() {
        try {
            Path single = _getPathToResource("../../project-to-test/duplicate_snippet_src.txt");

            List<Path> srcs = new ArrayList<Path>(Arrays.asList(single.toAbsolutePath()));
            HashMap<String, List<String>> foundSnippets = new SnippetReplacer().getAllSnippets(srcs);
        } catch (Exception e){
            // check for snippet id in string
            assertTrue(e.toString().contains("com.azure.data.applicationconfig.configurationclient.instantiation"));
        }
    }

    @Test
    public void duplicateSnippetsCrashWithMultipleFiles() {
        try {
            Path single = _getPathToResource("../../project-to-test/duplicate_snippet_src.txt");
            Path multiple = _getPathToResource("../../project-to-test/duplicate_snippet_src_multiple.txt");

            List<Path> srcs = new ArrayList<Path>(Arrays.asList(single.toAbsolutePath(), multiple.toAbsolutePath()));
            HashMap<String, List<String>> foundSnippets = new SnippetReplacer().getAllSnippets(srcs);
        } catch (Exception e){
            // check for snippet id in string
            assertTrue(e.toString().contains("com.azure.data.applicationconfig.configurationclient.instantiation"));
            // should be one duplicate message from each file
            assertTrue((e.toString().split("Duplicate snippetId", -1).length - 1) == 2);
        }
    }

    @Test
    public void notFoundSnippetCrashes() throws IOException{
        HashMap<String, List<String>> emptyMap = new HashMap<String, List<String>>();

        Path codeForReplacement = _getPathToResource("../../project-to-test/basic_src_snippet_insertion_before.txt");
        List<String> testLines = Files.readAllLines(codeForReplacement, StandardCharsets.UTF_8);
        SnippetReplacer testReplacer = new SnippetReplacer();

        SnippetOperationResult<StringBuilder> opResult = testReplacer.updateSnippets(codeForReplacement, testLines,
                testReplacer.SNIPPET_SRC_CALL_BEGIN, testReplacer.SNIPPET_SRC_CALL_END, emptyMap, "<pre>",
                "</pre>", 1, "* ", false);

        assertTrue(opResult.errorList.size() == 3);
    }
}
