package azuresdk.plugin;

import java.nio.file.Path;
import java.util.ArrayList;
import java.util.List;

public class VerifyResult {
    public String SnippetWithIssues;
    public Path ReadmeLocation;

    public VerifyResult(Path readmeLocation, String snippetIdWithIssues) {
        this.ReadmeLocation = readmeLocation;
        this.SnippetWithIssues = snippetIdWithIssues;
    }
}