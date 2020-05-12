package azuresdk.plugin;


import org.apache.maven.plugin.AbstractMojo;
import org.apache.maven.plugin.MojoExecutionException;

import org.apache.maven.plugins.annotations.LifecyclePhase;
import org.apache.maven.plugins.annotations.Mojo;
import org.apache.maven.plugins.annotations.Parameter;

import java.io.File;
import java.io.FileWriter;
import java.io.IOException;

@Mojo( name = "snippet-engine", defaultPhase = LifecyclePhase.PROCESS_SOURCES )
public class SnippetPluginEntry
    extends AbstractMojo
{
    @Parameter( property = "report.mode", required = true )
    private String mode;

    @Parameter( defaultValue = "${project.basedir}", property = "report.targetDir", required = true )
    private File targetDir;

    public void execute()
    {
        try {
            SnippetReplacer replacer = new SnippetReplacer(mode, targetDir, getLog());
            getLog().info("Completed Execution for Snippet Replacer");
        } catch (Exception e) {
            getLog().error(e);
            return;
        }
    }
}
