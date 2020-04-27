package azuresdk.plugin;

import static org.junit.Assert.*;

import org.apache.maven.plugin.MojoExecutionException;
import org.junit.Test;

import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
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

        return Path.of(pathToTestFile);
    }

    @Test
    public void srcParse()
            throws Exception
    {
        Path testFile = _getPathToResource("../../project-to-test/basic_src_snippet_parse.txt");
        HashMap<String, List<String>> foundSnippets = new SnippetReplacer().getAllSnippets(new ArrayList<Path>(Arrays.asList(testFile.toAbsolutePath())));

        assertTrue(foundSnippets.size() == 2);
        assertTrue(foundSnippets.get("com.azure.data.applicationconfig.configurationclient.instantiation").size() == 3);
        assertTrue(foundSnippets.get("com.azure.data.appconfiguration.ConfigurationClient.addConfigurationSetting#String-String-String").size() == 3);
    }

    @Test
    public void srcInsertion()
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
        String expectedString = Files.readString(expectedOutCome, StandardCharsets.UTF_8);

        HashMap<String, List<String>> foundSnippets = testReplacer.getAllSnippets(new ArrayList<Path>(Arrays.asList(snippetSourceFile.toAbsolutePath())));
        StringBuilder result = testReplacer.updateSnippets(codeForReplacement, testLines, foundSnippets, "<pre>", "</pre>", 1, "* ");

        assertTrue(result != null);
        assertTrue(result.toString().equals(expectedString));
    }

    @Test
    public void readmeParse(){

    }

    @Test
    public void readmeInsertion(){

    }

    @Test
    public void emptySnippetWorks(){

    }

    @Test
    public void duplicateSnippetsCrashWithSingleFile(){
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
    public void duplicateSnippetsCrashWithMultipleFiles(){
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
    public void notFoundSnippetCrashes(){
        try {
            HashMap<String, List<String>> emptyMap = new HashMap<String, List<String>>();

            Path codeForReplacement = _getPathToResource("../../project-to-test/basic_src_snippet_insertion_before.txt");
            List<String> testLines = Files.readAllLines(codeForReplacement, StandardCharsets.UTF_8);

            StringBuilder result = new SnippetReplacer().updateSnippets(codeForReplacement, testLines, emptyMap, "<pre>", "</pre>", 1, "* ");
        } catch (Exception e){
            assertTrue(e instanceof MojoExecutionException);
        }
    }
}
