package azuresdk.plugin;


import org.apache.maven.plugin.AbstractMojo;
import org.apache.maven.plugin.MojoExecutionException;

import org.apache.maven.plugins.annotations.LifecyclePhase;
import org.apache.maven.plugins.annotations.Mojo;
import org.apache.maven.plugins.annotations.Parameter;

import java.io.File;
import java.io.FileWriter;
import java.io.IOException;

/**
 * Goal which touches a timestamp file.
 */
@Mojo( name = "snippet-engine", defaultPhase = LifecyclePhase.PROCESS_SOURCES )
public class SnippetPluginEntry
    extends AbstractMojo
{
    /**
     * String to display
     */
    @Parameter( property = "report.mode", required = true )
    private String mode;

    @Parameter( defaultValue = "${project.basedir}", property = "report.targetDir", required = true )
    private File targetDir;

    public void execute()
        throws MojoExecutionException
    {
        SnippetReplacer replacer = new SnippetReplacer(mode, targetDir);
        getLog().info( "Hello, world." );


        getLog().info(mode);
        getLog().info(targetDir.getAbsolutePath());
    }
}
