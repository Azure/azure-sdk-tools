package azuresdk.plugin;
import java.io.FileWriter;
import java.nio.charset.StandardCharsets;
import java.nio.file.*;
import java.nio.file.attribute.BasicFileAttributes;
import java.util.*;
import java.util.regex.*;

import com.google.common.base.Verify;
import org.apache.maven.plugin.Mojo;
import org.apache.maven.plugin.MojoExecutionException;
import org.apache.maven.plugin.logging.Log;

import java.io.File;
import java.io.IOException;
import java.util.stream.Collectors;

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
        put(">", "&gt;");
        put("<", "&lt;");
        put("@", "{@literal @}");
        put("{", "&#123;");
        put("}", "&#125;");
        put("(", "&#40;");
        put(")", "&#41;");
        put("/", "&#47;");
        put("\\", "&#92;");
    }};

    public SnippetReplacer(){}

    public SnippetReplacer(String mode, File folderToVerify, Log logger) throws MojoExecutionException, IOException {
        switch (mode) {
            case "update":
                this.runUpdate(folderToVerify);
                break;
            case "verify":
                this.runVerification(folderToVerify, logger);
                break;
            default:
                throw new MojoExecutionException(String.format("Unrecognized snippet-replacer mode: %s.", mode));
        }
    }

    public void runVerification(File folderToVerify, Log logger) throws IOException, MojoExecutionException {
        List<Path> allLocatedJavaFiles = glob(folderToVerify.toPath(), JAVA_GLOB);
        List<Path> snippetSources = globFiles(allLocatedJavaFiles, SAMPLE_PATH_GLOB);
        List<VerifyResult> discoveredIssues = new ArrayList<>();
        HashMap<String, List<String>> foundSnippets = new HashMap<>();

        // scan the sample files for all the snippet files
        foundSnippets = this.getAllSnippets(snippetSources);

        // walk across all the java files, run UpdateSrcSnippets
        for (Path sourcePath : allLocatedJavaFiles) {
            discoveredIssues.addAll(this.verifySrcSnippets(sourcePath, foundSnippets));
        }

        // now find folderToVerify/README.md
        // run Update ReadmeSnippets on that
        File readmeInBaseDir = new File(folderToVerify, "README.md");
        discoveredIssues.addAll(this.verifyReadmeSnippets(readmeInBaseDir.toPath(), foundSnippets));

        if (discoveredIssues.size() > 0) {
            for (VerifyResult result : discoveredIssues) {
                logger.error(String.format("SnippetId %s needs update in file %s.", result.SnippetWithIssues, result.ReadmeLocation.toString()));
            }
            throw new MojoExecutionException("Discovered snippets in need of updating. Please run this plugin in update mode and commit the changes.");
        }
    }

    public void runUpdate(File folderToVerify) throws IOException, MojoExecutionException {
        List<Path> allLocatedJavaFiles = glob(folderToVerify.toPath(), JAVA_GLOB);
        List<Path> snippetSources = globFiles(allLocatedJavaFiles, SAMPLE_PATH_GLOB);
        HashMap<String, List<String>> foundSnippets = new HashMap<String, List<String>>();

        // scan the sample files for all the snippet files
        foundSnippets = this.getAllSnippets(snippetSources);

        // walk across all the java files, run UpdateSrcSnippets
        for (Path sourcePath : allLocatedJavaFiles) {
            this.updateSrcSnippets(sourcePath, foundSnippets);
        }

        // now find folderToVerify/README.md
        // run Update ReadmeSnippets on that
        File readmeInBaseDir = new File(folderToVerify, "README.md");
        this.updateReadmeSnippets(readmeInBaseDir.toPath(), foundSnippets);
    }

    public void updateReadmeSnippets(Path file, HashMap<String, List<String>> snippetMap) throws IOException, MojoExecutionException {
        List<String> lines = Files.readAllLines(file, StandardCharsets.UTF_8);
        StringBuilder modifiedLines = this.updateSnippets(file, lines, snippetMap, "", "", 0, "");

        if (modifiedLines != null) {
            try (FileWriter modificationWriter = new FileWriter(file.toAbsolutePath().toString(), StandardCharsets.UTF_8)) {
                modificationWriter.write(modifiedLines.toString());
            }
        }
    }

    public void updateSrcSnippets(Path file, HashMap<String, List<String>> snippetMap) throws IOException, MojoExecutionException {
        List<String> lines = Files.readAllLines(file, StandardCharsets.UTF_8);
        StringBuilder modifiedLines = this.updateSnippets(file, lines, snippetMap, "<pre>", "</pre>",1, "* ");

        if (modifiedLines != null) {
            try (FileWriter modificationWriter = new FileWriter(file.toAbsolutePath().toString(), StandardCharsets.UTF_8)) {
                modificationWriter.write(modifiedLines.toString());
            }
        }
    }

    public StringBuilder updateSnippets(Path file, List<String> lines, HashMap<String, List<String>> snippetMap, String preFence, String postFence, int prefixGroupNum, String additionalLinePrefix) throws MojoExecutionException {
        StringBuilder modifiedLines = new StringBuilder();
        boolean inSnippet = false;
        boolean needsAmend = false;
        String lineSep = System.lineSeparator();

        for (String line : lines) {
            Matcher begin = SNIPPET_CALL_BEGIN.matcher(line);
            Matcher end = SNIPPET_CALL_END.matcher(line);
            String currentSnippetId = "";

            if (begin.matches()) {
                modifiedLines.append(line + lineSep);
                currentSnippetId = begin.group(2);
                inSnippet = true;
            }
            else if (end.matches()) {
                List<String> newSnippets = null;
                if (snippetMap.containsKey(end.group(2))) {
                    newSnippets = snippetMap.get(end.group(2));
                }
                else {
                    throw new MojoExecutionException(String.format("Unable to locate snippet with Id of %s. Reference in %s", end.group(2), file.toString()));
                }

                List<String> modifiedSnippets = new ArrayList<>();

                // We use this additional prefix because in src snippet cases we need to prespace
                // for readme snippet cases we DONT need the prespace at all.
                String linePrefix = this.prefixFunction(end, prefixGroupNum, additionalLinePrefix);

                for (String snippet : this.respaceLines(newSnippets)) {
                    String moddedSnippet = this.escapeString(snippet);
                    modifiedSnippets.add((moddedSnippet.length() > 0 ? linePrefix : linePrefix.stripTrailing()) + moddedSnippet + lineSep);
                }

                if (preFence != null && preFence.length() > 0) {
                    modifiedLines.append(linePrefix + preFence + lineSep);
                }

                modifiedLines.append(String.join("", modifiedSnippets));

                if (postFence != null && postFence.length() > 0) {
                    modifiedLines.append(linePrefix + postFence + lineSep);
                }

                modifiedLines.append(line + lineSep);
                needsAmend = true;
                inSnippet = false;
            }
            else {
                if (inSnippet) {
                    // do nothing. we'll write everything at the end,
                    // we'd do a comparison here if we were verifying
                }
                else {
                    modifiedLines.append(line + lineSep);
                }
            }
        }

        if (needsAmend) {
            return modifiedLines;
        }
        else{
            return null;
        }
    }

    public List<VerifyResult> verifyReadmeSnippets(Path file, HashMap<String, List<String>> snippetMap) throws IOException, MojoExecutionException {
        List<String> lines = Files.readAllLines(file, StandardCharsets.UTF_8);
        return this.verifySnippets(file, lines, snippetMap, "```Java", "```", 0, "");
    }

    public List<VerifyResult> verifySrcSnippets(Path file, HashMap<String, List<String>> snippetMap) throws IOException, MojoExecutionException {
        List<String> lines = Files.readAllLines(file, StandardCharsets.UTF_8);
        return this.verifySnippets(file, lines, snippetMap, "<pre>", "</pre>", 1, "* ");
    }

    public List<VerifyResult> verifySnippets(Path file, List<String> lines, HashMap<String, List<String>> snippetMap, String preFence, String postFence, int prefixGroupNum, String additionalLinePrefix) throws MojoExecutionException {
        boolean inSnippet = false;
        String lineSep = System.lineSeparator();
        List<String> currentSnippetSet = null;
        List<VerifyResult> foundIssues = new ArrayList<>();

        for (String line : lines) {
            Matcher begin = SNIPPET_CALL_BEGIN.matcher(line);
            Matcher end = SNIPPET_CALL_END.matcher(line);
            String currentSnippetId = "";

            if (begin.matches()) {
                currentSnippetId = begin.group(2);
                inSnippet = true;
                currentSnippetSet = new ArrayList<>();
            }
            else if (end.matches()) {
                List<String> newSnippets;
                if (snippetMap.containsKey(end.group(2))) {
                    newSnippets = snippetMap.get(end.group(2));
                }
                else {
                    throw new MojoExecutionException(String.format("Unable to locate snippet with Id of %s. Reference in %s", end.group(2), file.toString()));
                }
                List<String> modifiedSnippets = new ArrayList<>();

                // We use this additional prefix because in src snippet cases we need to prespace
                // for readme snippet cases we DONT need the prespace at all.
                String linePrefix = this.prefixFunction(end, prefixGroupNum, additionalLinePrefix);

                for (String snippet : this.respaceLines(newSnippets)) {
                    String moddedSnippet = this.escapeString(snippet);
                    modifiedSnippets.add((moddedSnippet.length() > 0 ? linePrefix : linePrefix.stripTrailing()) + moddedSnippet + lineSep);
                }

                Collections.sort(modifiedSnippets);
                Collections.sort(currentSnippetSet);

                if (!modifiedSnippets.equals(currentSnippetSet)) {
                    foundIssues.add(new VerifyResult(file, currentSnippetId));
                }

                inSnippet = false;
                currentSnippetSet = null;
            }
            else {
                if (inSnippet) {
                    // need to ensure not to grab the pre or post fence
                    if (!line.contains(preFence) && !line.contains(postFence))
                        currentSnippetSet.add(line + lineSep);
                }
            }
        }

        return foundIssues;
    }

    public HashMap<String, List<String>> getAllSnippets(List<Path> snippetSources) throws IOException, MojoExecutionException {
        HashMap<String, List<String>> locatedSnippets = new HashMap<>();
        List<VerifyResult> detectedIssues = new ArrayList<>();

        for(Path samplePath: snippetSources){
            List<String> fileContent = Files.readAllLines(samplePath, StandardCharsets.UTF_8);
            HashMap<String, List<String>> tempSnippetMap = new HashMap<>();
            SnippetDictionary snippetReader = new SnippetDictionary();
            int counter = 0;

            for (String line : fileContent) {
                Matcher begin = SNIPPET_DEF_BEGIN.matcher(line);
                Matcher end = SNIPPET_DEF_END.matcher(line);

                if (begin.matches() ){
                    String id_beginning = begin.group(1);
                    snippetReader.beginSnippet((id_beginning));
                }
                else if (end.matches()) {
                    String id_ending = end.group(1);
                    List<String> snippetContent = snippetReader.finalizeSnippet((id_ending));
                    if (!tempSnippetMap.containsKey((id_ending))) {
                        tempSnippetMap.put(id_ending, snippetContent);
                    }
                    else {
                        // detect duplicate in file
                        detectedIssues.add(new VerifyResult(samplePath, id_ending));
                    }
                }
                else if (snippetReader.isActive()) {
                    snippetReader.processLine(line);
                }

                counter++;
            }

            // we need to examine them individually, as we want to get a complete list of all the duplicates in a run
            for (String snippetId : tempSnippetMap.keySet()) {
                if (!locatedSnippets.containsKey(snippetId)) {
                    locatedSnippets.put(snippetId, tempSnippetMap.get(snippetId));
                }
                else {
                    // detect duplicate across multiple files
                    detectedIssues.add(new VerifyResult(samplePath, snippetId));
                }
            }
        };

        if (detectedIssues.size() > 0) {
            throw new MojoExecutionException("Duplicate Snippet Definitions Detected. " + System.lineSeparator() + this.getErrorString(detectedIssues));
        }

        return locatedSnippets;
    }

    private String getErrorString(List<VerifyResult> errors) {
        StringBuilder results = new StringBuilder();

        for (VerifyResult result : errors) {
            results.append(String.format("Duplicate snippetId %s detected in %s.", result.SnippetWithIssues, result.ReadmeLocation.toString()) + System.lineSeparator());
        }

        return results.toString();
    }

    private List<String> respaceLines(List<String> snippetText) {
        // get List of all the the leading whitespace in the sample
        // toss out lines that are empty (as they shouldn't mess with the minimum)
        String minWhitespace = null;
        List<String> modifiedStrings = new ArrayList<>();

        for (String snippetLine : snippetText) {
            // only look at non-whitespace only strings for the min indent
            if(snippetLine.trim().length() != 0) {
                Matcher leadSpaceMatch = WHITESPACE_EXTRACTION.matcher(snippetLine);

                if (leadSpaceMatch.matches()) {
                    String leadSpace = leadSpaceMatch.group(1);

                    if (minWhitespace == null || leadSpace.length() < minWhitespace.length())
                        minWhitespace = leadSpace;
                }
            }
        }

        for (String snippetLine : snippetText) {
            modifiedStrings.add(snippetLine.replaceFirst(minWhitespace, ""));
        }

        return modifiedStrings;
    }

    private int getEndIndex(List<String> lines, int startIndex) {
        for (int i = startIndex; i < lines.size(); i++) {
            Matcher end = SNIPPET_CALL_END.matcher(lines.get(i));
            if (end.matches())
                return i;
        }

        return -1;
    }

    private String prefixFunction(Matcher match, int groupNum, String additionalPrefix) {
        // if we pass -1 as the matcher groupNum, we don't want any prefix at all
        if (match == null || groupNum < 1) {
            return "";
        } else {
            return match.group(groupNum) + additionalPrefix;
        }
    }

    private String escapeString(String target) {
        if (target != null && target.trim().length() > 0) {
            for (String key : this.REPLACEMENT_SET.keySet()) {
                target = target.replace(key, REPLACEMENT_SET.get(key));
            }
        }

        return target;
    }

    private List<Path> glob(Path rootFolder, PathMatcher pathMatcher) throws IOException {
        List<Path> locatedPaths = new ArrayList<>();

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

    private List<Path> globFiles(List<Path> paths, PathMatcher pathMatcher) throws IOException {
        List<Path> locatedPaths = new ArrayList<>();

        for (Path path : paths) {
            if (pathMatcher.matches(path)) {
                locatedPaths.add(path);
            }
        };

        return locatedPaths;
    }
}
