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

    @Test
    public void srcParse()
            throws Exception
    {
        Path testFile = _getPathToResource("../../project-to-test/basic_src_snippet_parse.txt");

        List<String> lines = Files.readAllLines(testFile, StandardCharsets.UTF_8);
        HashMap<String, List<String>> foundSnippets = new SnippetReplacer().grepSnippets(lines);

        assertTrue(foundSnippets.size() == 2);
        assertTrue(foundSnippets.get("com.azure.data.applicationconfig.configurationclient.instantiation").size() == 3);
        assertTrue(foundSnippets.get("com.azure.data.appconfiguration.ConfigurationClient.addConfigurationSetting#String-String-String").size() == 3);
    }

    @Test
    public void srcInsertion()
            throws Exception
    {
        Path snippetSourceFile = _getPathToResource("../../project-to-test/basic_src_snippet_parse.txt");
        Path codeForReplacement = _getPathToResource("../../project-to-test/basic_src_snippet_insertion_before.txt");
        Path expectedOutCome = _getPathToResource("../../project-to-test/basic_src_snippet_insertion_after.txt");
        SnippetReplacer testReplacer =  new SnippetReplacer();
        List<String> sourceLines = Files.readAllLines(snippetSourceFile, StandardCharsets.UTF_8);
        List<String> testLines = Files.readAllLines(codeForReplacement, StandardCharsets.UTF_8);
        String expectedString = Files.readString(expectedOutCome, StandardCharsets.UTF_8);

        HashMap<String, List<String>> foundSnippets = testReplacer.grepSnippets(sourceLines);
        StringBuilder result = testReplacer.updateSnippets(testLines, foundSnippets, "<pre>", "</pre>", 1, "* ");

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
    public void snippetsEncodeHTML(){

    }

    @Test
    public void notFoundSnippetCrashes(){

    }

    @Test
    public void duplicateSnippetCrashes(){

    }
}
