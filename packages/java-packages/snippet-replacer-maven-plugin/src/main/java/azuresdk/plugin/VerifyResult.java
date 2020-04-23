package azuresdk.plugin;

import java.nio.file.Path;
import java.util.ArrayList;
import java.util.List;

public class VerifyResult {
    public List<String> SnippetsNeedingUpdate = new ArrayList<String>();
    public Path ReadmeLocation;

    public VerifyResult(Path readmeLocation, List<String> snippetsWithIssues){
        this.ReadmeLocation = readmeLocation;
        this.SnippetsNeedingUpdate = snippetsWithIssues;
    }
}