package azuresdk.plugin;
import java.io.FileWriter;
import java.lang.reflect.Array;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.ArrayList;
import java.util.List;
import java.util.regex.*;

import org.apache.maven.plugin.MojoExecutionException;

import java.io.File;
import java.util.HashMap;
import java.io.IOException;

public class SnippetReplacer {
    private static Pattern SNIPPET_DEF_BEGIN = Pattern.compile("\\s*\\/\\/\\s*BEGIN\\:\\s+([a-zA-Z0-9\\.\\#\\-\\_]*)\\s*");
    private static Pattern SNIPPET_DEF_END = Pattern.compile("\\s*\\/\\/\\s*END\\:\\s+([a-zA-Z0-9\\.\\#\\-\\_]*)\\s*");
    private static Pattern SNIPPET_CALL_BEGIN   = Pattern.compile("(\\s*)\\*?\\s*<!--\\s+(src_embed)\\s+([a-zA-Z0-9\\.\\#\\-\\_]*)\\s*-->");
    private static Pattern SNIPPET_CALL_END   = Pattern.compile("(\\s*)\\*?\\s*<!--\\s+(end)\\s+([a-zA-Z0-9\\.\\#\\-\\_]*)\\s*-->");
    private static Pattern WHITESPACE_EXTRACTION = Pattern.compile("(\\s*)(.*)");
    private static String SAMPLE_PATH_GLOB       = "**/src/samples/java/**";
    private static HashMap REPLACEMENT_SET = new HashMap(){{
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

    public SnippetReplacer(){}

    /*
        Constructor for use directly from the Mojo entrypoint.
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

    public List<VerifyResult> RunVerification(File folderToVerify){
        // get all files
        return new ArrayList<VerifyResult>();
    }

    public void RunUpdate(File folderToVerify){
        // find all files that we care about

        // for each of the found files, extract content, grep snippets
    }

    private List<String> _respaceLines(List<String> snippetText){
        // get List of all the the leading whitespace in the sample
        // toss out lines that are empty (as they shouldn't mess with the minimum)
        String minWhitespace = null;
        List<String> modifiedStrings = new ArrayList<String>();

        for(String snippetLine: snippetText){
            // only look at non-whitespace only strings for the min indent
            if(snippetLine.trim().length() != 0) {
                Matcher leadSpaceMatch = WHITESPACE_EXTRACTION.matcher(snippetLine);

                if(leadSpaceMatch.matches()){
                    String leadSpace = leadSpaceMatch.group(1);

                    if(leadSpace.length() < minWhitespace.length())
                        minWhitespace = leadSpace;
                }
            }
        }

        // respace the lines and pass them back.
        for(String snippetLine: snippetText) {
            modifiedStrings.add(snippetLine.replaceFirst(minWhitespace, ""));
        }

        return modifiedStrings;
    }

    public void UpdateReadmeSnippets(Path file, HashMap<String, List<String>> snippetMap) throws IOException {
        List<String> lines = Files.readAllLines(file, StandardCharsets.UTF_8); //TODO: stream this and save mem
        StringBuilder modifiedLines = this.UpdateSnippets(lines, snippetMap, "```Java", "```");

        if(modifiedLines != null) {
            try {
                FileWriter modificationWriter = new FileWriter(Files.readString(file), StandardCharsets.UTF_8);
                modificationWriter.write(modifiedLines.toString());
            } catch (IOException e) {
                e.printStackTrace();
            }
        }
    }


    /*
        Update a file on disk with matched codesnippet definitions.
     */
    public void UpdateSrcSnippets(Path file, HashMap<String, List<String>> snippetMap) throws IOException {
        List<String> lines = Files.readAllLines(file, StandardCharsets.UTF_8); //TODO: stream this and save mem
        StringBuilder modifiedLines = this.UpdateSnippets(lines, snippetMap, "<pre>", "</pre>");

        if(modifiedLines != null) {
            try {
                FileWriter modificationWriter = new FileWriter(Files.readString(file), StandardCharsets.UTF_8);
                modificationWriter.write(modifiedLines.toString());
            } catch (IOException e) {
                e.printStackTrace();
            }
        }
    }

    private String _prefixFunction(Matcher match, int groupNum, String additionalPrefix){
        // if we pass -1 as the matcher groupNum, we don't want any prefix at all
        if(match == null || groupNum < 1) {
            return "";
        }
        else{
            return match.group(groupNum) + additionalPrefix;
        }
    }

    public StringBuilder UpdateSnippets(List<String> lines, HashMap<String, List<String>> snippetMap, String preFence, String postFence, int prefixGroupNum, String additionalLinePrefix){
        StringBuilder modifiedLines = new StringBuilder();
        boolean inSnippet = false;
        boolean needsAmend = false;

        for(String line: lines){
            Matcher begin = SNIPPET_CALL_BEGIN.matcher(line);
            Matcher end = SNIPPET_CALL_END.matcher(line);
            String current_snippet_id = "";

            if(begin.matches()){
                modifiedLines.append(line);
                current_snippet_id = begin.group(2);
                inSnippet = true;
            }
            else if(end.matches()){
                List<String> newSnippets = snippetMap.getOrDefault(end.group(2), new ArrayList<String>());
                // We use this additional prefix because in src snippet cases we need to prespace
                // for readme snippet cases we DONT need the prespace at all.
                String linePrefix = this._prefixFunction(end, prefixGroupNum, additionalLinePrefix);
                List<String> modifiedSnippets = new ArrayList<String>();

                for(String snippet: this._respaceLines(newSnippets)){
                    // TODO: leverage the escape table here.
                    modifiedSnippets.add(linePrefix + snippet);
                }

                modifiedLines.append(linePrefix + preFence);
                modifiedLines.append(String.join("", modifiedSnippets));
                modifiedLines.append(linePrefix + postFence);
                modifiedLines.append(line);
                needsAmend = true;
            }
            else {
                if(inSnippet){
                    // do nothing. we'll write everything at the end,
                    // we'd do a comparison here if we were verifying
                }
                else {
                    modifiedLines.append(line);
                }
            }
        }

        if(needsAmend){
            return modifiedLines;
        }
        else{
            return null;
        }
    }

    /*
        Scans a readme file, ensures that contents of snippets match their sources
     */
    public List<VerifyResult> VerifySnippets(Path file, HashMap<String, List<String>> snippetMap){

        return new ArrayList<VerifyResult>();
    }

    /*
        Scans through a file content array, finds all code snippet definitions.
     */
    public HashMap<String, List<String>> GrepSnippets(List<String> fileContent){
        HashMap foundSnippets = new HashMap();
        SnippetDictionary snippetReader = new SnippetDictionary();
        int counter = 0;

        for(String line: fileContent){
            Matcher begin = SNIPPET_DEF_BEGIN.matcher(line);
            Matcher end = SNIPPET_DEF_END.matcher(line);

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

    public HashMap<String, List<String>> GrepSnippets(File fileWithContent) throws IOException{
        List<String> lines = Files.readAllLines(fileWithContent.toPath(), StandardCharsets.UTF_8);
        return this.GrepSnippets(lines);
    }

    private int GetEndIndex(List<String> lines, int startIndex){
        for(int i = startIndex; i < lines.size(); i++){
            Matcher end = SNIPPET_CALL_END.matcher(lines.get(i));
            if(end.matches())
                return i;
        }

        return -1;
    }
}
