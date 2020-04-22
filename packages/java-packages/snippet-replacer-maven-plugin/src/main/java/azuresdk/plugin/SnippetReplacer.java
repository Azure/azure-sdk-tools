package azuresdk.plugin;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.util.ArrayList;
import java.util.List;
import java.util.Map;
import java.util.regex.*;
import org.apache.maven.plugin.MojoExecutionException;

import java.io.File;
import java.util.HashMap;
import java.io.FileWriter;
import java.io.IOException;

public class SnippetReplacer {
    private static Pattern SNIPPET_BEGIN         = Pattern.compile("\\s*\\/\\/\\s*BEGIN\\:\\s+([a-zA-Z0-9\\.\\#\\-\\_]*)\\s*");
    private static Pattern SNIPPET_END           = Pattern.compile("\\s*\\/\\/\\s*END\\:\\s+([a-zA-Z0-9\\.\\#\\-\\_]*)\\s*");
    private static Pattern SNIPPET_CALL          = Pattern.compile("(.*\\*).*\\{\\@codesnippet(.*)\\}");
    private static Pattern WHITESPACE_EXTRACTION = Pattern.compile("(\\s*).*");
    private static String SAMPLE_PATH_GLOB       = "**/src/samples/java/**";

    HashMap REPLACEMENT_SET = new HashMap(){{
        put('"', "&quot;");
        put(">", "&#62;");
        put("<", "&#60;");
        put("@", "{@literal @}");
        put("{", "&#123;");
        put("}", "&#125;");
        put("(", "&#40;");
        put(")", "&#41;");
        put("/", "&#47;");
        put("\\", "&#92;");
    }};

    public SnippetReplacer(){
    }

    /*
     Matcher matcher = pattern.matcher(textData);

        while (matcher.find()) {
            if (matcher.groupCount() == 3) {
                areaCodeList.add(
                        new PhoneAreaCode(matcher.group("city"), matcher.group("state"),
                                matcher.group("areaCode")));
            }
        }
     */

    public SnippetReplacer(String mode, File folderToVerify) throws MojoExecutionException {
        switch (mode) {
            case "update":
                this.RunUpdate(folderToVerify);
                break;
            case "verify":
                this.RunVerification(folderToVerify);
                break;
            default:
                throw new MojoExecutionException(String.format("Unrecognized snippet-replacer mode: %s.", mode));
        }
    }

    public boolean RunVerification(File folderToVerify){

        return false;
    }

    public boolean RunUpdate(File folderToVerify){
        // find all files that we care about

        // for each of the found files, extract content, grep snippets
        return false;
    }

    public HashMap<String, List<String>> GrepSnippets(File fileWithContent) throws IOException{
        List<String> lines = Files.readAllLines(fileWithContent.toPath(), StandardCharsets.UTF_8);
        return this.GrepSnippets(lines);
    }

    /*
        Scans through a file content array, finds all code snippet definitions.

        The hashmap key:value pair is (CodeSnippetId: CodeSnippetLines)
     */
    public HashMap<String, List<String>> GrepSnippets(List<String> fileContent){
        HashMap foundSnippets = new HashMap();
        SnippetDictionary snippetReader = new SnippetDictionary();
        int counter = 0;

        for(String line: fileContent){
            Matcher begin = SNIPPET_BEGIN.matcher(line);
            Matcher end = SNIPPET_END.matcher(line);

            if(begin.matches())
            {

                String id_beginning = begin.group(1);
                snippetReader.BeginSnippet((id_beginning));
            }
            else if(end.matches())
            {
                String id_ending = end.group(1);
                List<String> snippetContent = snippetReader.FinalizeSnippet((id_ending));
                foundSnippets.put(id_ending, snippetContent);
            }
            else if(snippetReader.IsActive())
            {
                snippetReader.ProcessLine(line);
            }

            counter++;
        }

        return foundSnippets;
    }
}
