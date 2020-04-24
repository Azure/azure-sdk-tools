package azuresdk.plugin;
import java.io.FileWriter;
import java.lang.reflect.Array;
import java.nio.charset.StandardCharsets;
import java.nio.file.*;
import java.nio.file.attribute.BasicFileAttributes;
import java.util.ArrayList;
import java.util.List;
import java.util.regex.*;

import org.apache.maven.plugin.MojoExecutionException;

import java.io.File;
import java.util.HashMap;
import java.io.IOException;

public class SnippetReplacer {
    private final static Pattern SNIPPET_DEF_BEGIN = Pattern.compile("\\s*\\/\\/\\s*BEGIN\\:\\s+([a-zA-Z0-9\\.\\#\\-\\_]*)\\s*");
    private final static Pattern SNIPPET_DEF_END = Pattern.compile("\\s*\\/\\/\\s*END\\:\\s+([a-zA-Z0-9\\.\\#\\-\\_]*)\\s*");
    private final static Pattern SNIPPET_CALL_BEGIN = Pattern.compile("(\\s*)\\*?\\s*<!--\\s+src_embed\\s+([a-zA-Z0-9\\.\\#\\-\\_]*)\\s*-->");
    private final static Pattern SNIPPET_CALL_END = Pattern.compile("(\\s*)\\*?\\s*<!--\\s+end\\s+([a-zA-Z0-9\\.\\#\\-\\_]*)\\s*-->");
    private final static Pattern WHITESPACE_EXTRACTION = Pattern.compile("(\\s*)(.*)");

    private static PathMatcher SAMPLE_PATH_GLOB = FileSystems.getDefault().getPathMatcher("glob:**/src/samples/java/**/*.java");
    private static PathMatcher JAVA_GLOB = FileSystems.getDefault().getPathMatcher("glob:**/*.java");

    private static HashMap<String, String> REPLACEMENT_SET = new HashMap<String, String>(){{
        put("\"", "&quot;");
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

    public SnippetReplacer(String mode, File folderToVerify) throws MojoExecutionException, IOException {
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



    public List<VerifyResult> RunVerification(File folderToVerify) throws IOException{
        List<Path> allLocatedJavaFiles = _glob(folderToVerify.toPath(), JAVA_GLOB);
        List<Path> snippetSources = _globFiles(allLocatedJavaFiles, SAMPLE_PATH_GLOB);




        return new ArrayList<VerifyResult>();
    }

    public void RunUpdate(File folderToVerify) throws IOException{
        List<Path> allLocatedJavaFiles = _glob(folderToVerify.toPath(), JAVA_GLOB);
        List<Path> snippetSources = _globFiles(allLocatedJavaFiles, SAMPLE_PATH_GLOB);
        HashMap<String, List<String>> foundSnippets = new HashMap<String, List<String>>();

        // scan the sample files for all the snippet files
        for(Path samplePath: snippetSources){
            List<String> sourceLines = Files.readAllLines(samplePath, StandardCharsets.UTF_8);
            foundSnippets.putAll(this.GrepSnippets(sourceLines));
        };

        // walk across all the java files, run UpdateSrcSnippets
        for(Path sourcePath: allLocatedJavaFiles){
            this.UpdateSrcSnippets(sourcePath, foundSnippets);
        }

        // now find folderToVerify/README.md
        // run Update ReadmeSnippets on that
        File readmeInBaseDir = new File(folderToVerify, "README.md");
        this.UpdateReadmeSnippets(readmeInBaseDir.toPath(), foundSnippets);
    }

    public void UpdateReadmeSnippets(Path file, HashMap<String, List<String>> snippetMap) throws IOException {
        List<String> lines = Files.readAllLines(file, StandardCharsets.UTF_8); //TODO: stream this and save mem
        StringBuilder modifiedLines = this.UpdateSnippets(lines, snippetMap, "```Java", "```", 0, "");

        if(modifiedLines != null) {
            try {
                FileWriter modificationWriter = new FileWriter(Files.readString(file), StandardCharsets.UTF_8);
                modificationWriter.write(modifiedLines.toString());
            } catch (IOException e) {
                e.printStackTrace();
            }
        }
    }

    public void UpdateSrcSnippets(Path file, HashMap<String, List<String>> snippetMap) throws IOException {
        List<String> lines = Files.readAllLines(file, StandardCharsets.UTF_8);
        StringBuilder modifiedLines = this.UpdateSnippets(lines, snippetMap, "<pre>", "</pre>",1, "* ");

        if(modifiedLines != null) {
            try {
                FileWriter modificationWriter = new FileWriter(Files.readString(file), StandardCharsets.UTF_8);
                modificationWriter.write(modifiedLines.toString());
            } catch (IOException e) {
                e.printStackTrace();
            }
        }
    }

    public StringBuilder UpdateSnippets(List<String> lines, HashMap<String, List<String>> snippetMap, String preFence, String postFence, int prefixGroupNum, String additionalLinePrefix){
        StringBuilder modifiedLines = new StringBuilder();
        boolean inSnippet = false;
        boolean needsAmend = false;
        String lineSep = System.lineSeparator();

        for(String line: lines){
            Matcher begin = SNIPPET_CALL_BEGIN.matcher(line);
            Matcher end = SNIPPET_CALL_END.matcher(line);
            String current_snippet_id = "";

            if(begin.matches()){
                modifiedLines.append(line + lineSep);
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
                    modifiedSnippets.add(linePrefix + this._escapeString(snippet) + lineSep);
                }

                modifiedLines.append(linePrefix + preFence + lineSep);
                modifiedLines.append(String.join("", modifiedSnippets));
                modifiedLines.append(linePrefix + postFence + lineSep);
                modifiedLines.append(line + lineSep);
                needsAmend = true;
                inSnippet = false;
            }
            else {
                if(inSnippet){
                    // do nothing. we'll write everything at the end,
                    // we'd do a comparison here if we were verifying
                }
                else {
                    modifiedLines.append(line + lineSep);
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

    public List<VerifyResult> VerifySnippets(Path file, HashMap<String, List<String>> snippetMap){

        return new ArrayList<VerifyResult>();
    }

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

    private List<Path> _getPathsFromBaseDir(File baseDir, String glob_pattern){
        return new ArrayList<Path>();
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

                    if(minWhitespace == null || leadSpace.length() < minWhitespace.length())
                        minWhitespace = leadSpace;
                }
            }
        }

        for(String snippetLine: snippetText) {
            modifiedStrings.add(snippetLine.replaceFirst(minWhitespace, ""));
        }

        return modifiedStrings;
    }

    private int _getEndIndex(List<String> lines, int startIndex){
        for(int i = startIndex; i < lines.size(); i++){
            Matcher end = SNIPPET_CALL_END.matcher(lines.get(i));
            if(end.matches())
                return i;
        }

        return -1;
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

    private String _escapeString(String target){
        if(target != null && target.trim().length() > 0){
            for(String key: this.REPLACEMENT_SET.keySet()){
                target = target.replace(key, REPLACEMENT_SET.get(key));
            }
        }

        return target;
    }

    private List<Path> _glob(Path rootFolder, PathMatcher pathMatcher) throws IOException{
        List<Path> locatedPaths = new ArrayList<Path>();

        Files.walkFileTree(rootFolder, new SimpleFileVisitor<Path>() {
            @Override
            public FileVisitResult visitFile(Path file, BasicFileAttributes attrs) throws IOException {
                if (pathMatcher.matches(file)) {
                    locatedPaths.add(file);
                }
                return FileVisitResult.CONTINUE;
            }
            @Override
            public FileVisitResult visitFileFailed(Path file, IOException exc) throws IOException {
                return FileVisitResult.CONTINUE;
            }
        });

        return locatedPaths;
    }

    private List<Path> _globFiles(List<Path> paths, PathMatcher pathMatcher) throws IOException{
        List<Path> locatedPaths = new ArrayList<Path>();

        for(Path path: paths) {
            if (pathMatcher.matches(path)) {
                locatedPaths.add(path);
            }
        };

        return locatedPaths;
    }
}
