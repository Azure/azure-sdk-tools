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
@Mojo( name = "report", defaultPhase = LifecyclePhase.PROCESS_SOURCES )
public class SnippetPluginEntry
    extends AbstractMojo
{
    /**
     * String to display
     */
    @Parameter( property = "report.mode", required = true )
    private String mode;

    @Parameter( property = "report.targetdir", required = true )
    private File targetdir;

    public void execute()
        throws MojoExecutionException
    {
        SnippetReplacer replacer = new SnippetReplacer(mode, targetdir);
        getLog().info( "Hello, world." );
        getLog().info(mode);
    }
}
