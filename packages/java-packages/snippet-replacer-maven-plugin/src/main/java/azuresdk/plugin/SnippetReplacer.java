package azuresdk.plugin;
import java.util.regex.*;
import org.apache.maven.plugin.MojoExecutionException;

import java.io.File;
import java.io.FileWriter;
import java.io.IOException;

public class SnippetReplacer {
    private static Pattern SNIPPET_BEGIN = Pattern.compile("\\s*\\/\\/\\s*BEGIN\\:\\s+([a-zA-Z0-9\\.\\#\\-\\_]*)\\s*");
    private static Pattern SNIPPET_END = Pattern.compile("\\s*\\/\\/\\s*END\\:\\s+([a-zA-Z0-9\\.\\#\\_]*)\\s*");
    private static Pattern SNIPPET_CALL = Pattern.compile("(.*\\*).*\\{\\@codesnippet(.*)\\}");
    private static Pattern WHITESPACE_EXTRACTION = Pattern.compile("(\\s*).*");
    private static string SAMPLE_PATH_GLOB = "**/src/samples/java/**";

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

        return false;
    }

    public boolean GrepSnippets(String[] fileContent){

    }


}
