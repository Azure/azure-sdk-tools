package azuresdk.plugin;

import static org.junit.Assert.*;
import org.junit.Test;

import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
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

    /**
     * @throws Exception if any
     */
    @Test
    public void testBasicSrcParse()
            throws Exception
    {
        Path testFile = _getPathToResource("../../project-to-test/basic_src_snippet_parse.txt");

        List<String> lines = Files.readAllLines(testFile, StandardCharsets.UTF_8);
        HashMap<String, List<String>> foundSnippets = new SnippetReplacer().GrepSnippets(lines);

        assertTrue(foundSnippets.size() == 2);
        assertTrue(foundSnippets.get("com.azure.data.applicationconfig.configurationclient.pipeline.instantiation").size() == 9);
        assertTrue(foundSnippets.get("com.azure.data.appconfiguration.ConfigurationClient.addConfigurationSetting#String-String-String").size() == 3);
    }

    @Test
    public void testBasicInsertion()
            throws Exception
    {
        Path snippetSourceFile = _getPathToResource("../../project-to-test/basic_src_snippet_parse.txt");
        Path codeForReplacement = _getPathToResource("../../project-to-test/basic_src_snippet_insertion_before.txt");
        Path expectedOutCome = _getPathToResource("../../project-to-test/basic_src_snippet_insertion_after.txt");

        SnippetReplacer testReplacer =  new SnippetReplacer();

        List<String> sourceLines = Files.readAllLines(snippetSourceFile, StandardCharsets.UTF_8);
        HashMap<String, List<String>> foundSnippets = testReplacer.GrepSnippets(sourceLines);
        List<String> testLines = Files.readAllLines(codeForReplacement, StandardCharsets.UTF_8);


        StringBuilder result = testReplacer.UpdateSnippets(testLines, foundSnippets, "<pre>", "</pre>", 1, "* ");

        assertTrue(result != null);

    }
}
