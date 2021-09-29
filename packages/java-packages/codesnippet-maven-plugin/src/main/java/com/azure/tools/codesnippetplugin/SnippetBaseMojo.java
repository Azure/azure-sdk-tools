// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

package com.azure.tools.codesnippetplugin;

import com.azure.tools.codesnippetplugin.implementation.SnippetReplacer;
import org.apache.maven.plugin.AbstractMojo;
import org.apache.maven.plugin.MojoExecutionException;
import org.apache.maven.plugin.logging.Log;
import org.apache.maven.plugins.annotations.Parameter;

import java.io.File;
import java.io.IOException;
import java.nio.file.Path;

/**
 * Base Mojo for the codesnippet plugin.
 */
public abstract class SnippetBaseMojo extends AbstractMojo {
    /**
     * Default glob to match codesnippet files.
     */
    public static final String DEFAULT_CODESNIPPET_GLOB = "**/src/samples/java/**/*.java";

    /**
     * Default glob to match source files.
     */
    public static final String DEFAULT_SOURCE_GLOB = "**/src/main/java/**/*.java";

    /**
     * Glob for the files that contain codesnippet definitions.
     * <p>
     * Default value is {@link #DEFAULT_CODESNIPPET_GLOB}.
     */
    @Parameter(property = "codesnippetGlob", defaultValue = DEFAULT_CODESNIPPET_GLOB)
    private String codesnippetGlob;

    /**
     * Root directory to begin searching for codesnippet files.
     * <p>
     * Default value is {@code ${project.basedir}/src/samples/java}.
     */
    @Parameter(property = "codesnippetRootDirectory", defaultValue = "${project.basedir}/src/samples/java")
    private File codesnippetRootDirectory;

    /**
     * Glob for the source files to inject codesnippets.
     * <p>
     * Default value is {@link #DEFAULT_SOURCE_GLOB}.
     */
    @Parameter(property = "sourceGlob", defaultValue = DEFAULT_SOURCE_GLOB)
    private String sourceGlob;

    /**
     * Root directory to begin searching for source files.
     * <p>
     * Default value is {@code ${project.basedir}/src/main/java}.
     */
    @Parameter(property = "sourceRootDirectory", defaultValue = "${project.basedir}/src/main/java")
    private File sourceRootDirectory;

    /**
     * Flag indicating if source files should be targeted for codesnippet injection or validation.
     * <p>
     * Default value is true.
     */
    @Parameter(property = "includeSource", defaultValue = "true")
    private boolean includeSource;

    /**
     * Path of the README file.
     * <p>
     * Default value is {@code ${project.basedir}/README.md}.
     */
    @Parameter(property = "readmePath", defaultValue = "${project.basedir}/README.md")
    private File readmePath;

    /**
     * Flag indicating if README files should be targeted for codesnippet injection or validation.
     * <p>
     * Default value is true.
     */
    @Parameter(property = "includeReadme", defaultValue = "true")
    private boolean includeReadme;

    /**
     * Maximum line length for a Javadoc comment after the codesnippet has been injected.
     * <p>
     * Default value is {@code 120} characters.
     */
    @Parameter(property = "maxLineLength", defaultValue = "120")
    private int maxLineLength;

    /**
     * Skip running the plugin.
     * <p>
     * Default value is false.
     */
    @Parameter(property = "skip", defaultValue = "false")
    private boolean skip;

    /**
     * Gets the glob for the files that contain codesnippet definitions.
     *
     * @return The glob for the files that contain codesnippet definitions.
     */
    protected String getCodesnippetGlob() {
        return codesnippetGlob;
    }

    /**
     * Gets the root directory to begin searching for codesnippet files.
     *
     * @return The root directory to begin searching for codesnippet files.
     */
    protected File getCodesnippetRootDirectory() {
        return codesnippetRootDirectory;
    }

    /**
     * Gets the glob for the source files to inject codesnippets.
     *
     * @return The glob for the source files to inject codesnippets.
     */
    protected String getSourceGlob() {
        return sourceGlob;
    }

    /**
     * Gets the root directory to begin searching for source files.
     *
     * @return The root directory to begin searching for source files.
     */
    protected File getSourceRootDirectory() {
        return sourceRootDirectory;
    }

    /**
     * Gets whether source files should be targeted for codesnippet injection or validation.
     *
     * @return Whether source files should be targeted for codesnippet injection or validation.
     */
    protected boolean isIncludeSource() {
        return includeSource;
    }

    /**
     * Gets the path of the README file.
     *
     * @return The path of the README file.
     */
    protected File getReadmePath() {
        return readmePath;
    }

    /**
     * Gets whether the README should be targeted for codesnippet injection or validation.
     *
     * @return Whether the README should be targeted for codesnippet injection or validation.
     */
    protected  boolean isIncludeReadme() {
        return includeReadme;
    }

    /**
     * Gets the maximum line length for a Javadoc comment after the codesnippet has been injected.
     *
     * @return The maximum line length for a Javadoc comment after the codesnippet has been injected.
     */
    protected int getMaxLineLength() {
        return maxLineLength;
    }

    /**
     * Gets whether the plugin execution should be skipped.
     *
     * @return Whether the plugin execution should be skipped.
     */
    protected boolean isSkip() {
        return skip;
    }

    protected void executeCodesnippet(ExecutionMode executionMode) throws MojoExecutionException {
        if (isSkip()) {
            return;
        }

        Log log = getLog();

        Path codesnippetRootDirectory = getCodesnippetRootDirectory().toPath();
        log.debug(String.format("Using codesnippet root directory: %s", codesnippetRootDirectory));

        String codesnippetGlob = getCodesnippetGlob();
        log.debug(String.format("Using codesnippet glob: %s", codesnippetGlob));

        Path sourcesRootDirectory = getSourceRootDirectory().toPath();
        log.debug(String.format("Using sources root directory: %s", sourcesRootDirectory));

        String sourcesGlob = getSourceGlob();
        log.debug(String.format("Using source glob: %s", sourcesGlob));

        boolean includeSource = isIncludeSource();
        log.debug(String.format("Is source included? %b", includeSource));

        Path readmePath = getReadmePath().toPath();
        log.debug(String.format("Using README path: %s", readmePath));

        boolean includeReadme = isIncludeReadme();
        log.debug(String.format("Is README included? %b", includeSource));

        int maxLineLength = getMaxLineLength();
        log.debug(String.format("Using max line length: %d", maxLineLength));

        if (executionMode == ExecutionMode.UPDATE) {
            try {
                log.debug("Beginning codesnippet update execution.");
                SnippetReplacer.updateCodesnippets(codesnippetRootDirectory, codesnippetGlob, sourcesRootDirectory,
                    sourcesGlob, includeSource, readmePath, includeReadme, maxLineLength, log);
                log.debug("Completed codesnippet update execution.");
            } catch (IOException ex) {
                log.error(ex);
                throw new MojoExecutionException("Failed to update codesnippets.", ex);
            }
        } else if (executionMode == ExecutionMode.VERIFY) {
            try {
                log.debug("Beginning codesnippet verification execution.");
                SnippetReplacer.verifyCodesnippets(codesnippetRootDirectory, codesnippetGlob, sourcesRootDirectory,
                    sourcesGlob, includeSource, readmePath, includeReadme, maxLineLength, log);
                log.debug("Completed codesnippet verification execution.");
            } catch (IOException ex) {
                log.error(ex);
                throw new MojoExecutionException("Failed to verify codesnippets.", ex);
            }
        } else {
            throw new MojoExecutionException("Unsupported execution mode '" + executionMode + "' provided.");
        }
    }
}
