// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

package com.azure.tools.codesnippetplugin;

import com.azure.tools.codesnippetplugin.implementation.SnippetReplacer;
import org.apache.maven.plugin.AbstractMojo;
import org.apache.maven.plugin.MojoExecutionException;
import org.apache.maven.plugin.MojoFailureException;
import org.apache.maven.plugin.logging.Log;
import org.apache.maven.plugins.annotations.Parameter;

import java.io.File;
import java.io.IOException;
import java.nio.file.Path;
import java.util.function.Supplier;

/**
 * Base Mojo for the codesnippet plugin.
 */
public abstract class SnippetBaseMojo extends AbstractMojo {
    private static final String PROPERTY_PREFIX = "codesnippet.";

    /**
     * Default glob to match codesnippet files.
     */
    public static final String DEFAULT_CODESNIPPET_GLOB = "**/src/samples/java/**/*.java";

    /**
     * Default glob to match source files.
     */
    public static final String DEFAULT_SOURCE_GLOB = "**/src/main/java/**/*.java";

    /**
    * Default glob to match README files.
    */
    public static final String DEFAULT_README_GLOB = "**/README.md";

    /**
     * Glob for the files that contain codesnippet definitions.
     * <p>
     * Default value is {@link #DEFAULT_CODESNIPPET_GLOB}.
     */
    @Parameter(property = PROPERTY_PREFIX + "codesnippetGlob", defaultValue = DEFAULT_CODESNIPPET_GLOB)
    private String codesnippetGlob;

    /**
     * Root directory to begin searching for codesnippet files.
     * <p>
     * Default value is {@code ${project.basedir}/src/samples/java}.
     */
    @Parameter(property = PROPERTY_PREFIX + "codesnippetRootDirectory",
        defaultValue = "${project.basedir}/src/samples/java")
    private File codesnippetRootDirectory;

    /**
     * Glob for the source files to inject codesnippets.
     * <p>
     * Default value is {@link #DEFAULT_SOURCE_GLOB}.
     */
    @Parameter(property = PROPERTY_PREFIX + "sourceGlob", defaultValue = DEFAULT_SOURCE_GLOB)
    private String sourceGlob;

    /**
     * Root directory to begin searching for source files.
     * <p>
     * Default value is {@code ${project.basedir}/src/main/java}.
     */
    @Parameter(property = PROPERTY_PREFIX + "sourceRootDirectory", defaultValue = "${project.basedir}/src/main/java")
    private File sourceRootDirectory;

    /**
     * Flag indicating if source files should be targeted for codesnippet injection or validation.
     * <p>
     * Default value is true.
     */
    @Parameter(property = PROPERTY_PREFIX + "includeSource", defaultValue = "true")
    private boolean includeSource;

    /**
    * Glob for the README files to inject codesnippets.
    * <p>
    * Default value is {@link #DEFAULT_README_GLOB}.
    */
    @Parameter(property = PROPERTY_PREFIX + "readmeGlob", defaultValue = DEFAULT_README_GLOB)
    private String readmeGlob;

    /**
    * Root directory to begin searching for README files.
    * <p>
    * Default value is {@code ${project.basedir}}.
    */
    @Parameter(property = PROPERTY_PREFIX + "readmeRootDirectory", defaultValue = "${project.basedir}")
    private File readmeRootDirectory;

    /**
     * Flag indicating if README files should be targeted for codesnippet injection or validation.
     * <p>
     * Default value is true.
     */
    @Parameter(property = PROPERTY_PREFIX + "includeReadme", defaultValue = "true")
    private boolean includeReadme;

    /**
     * Maximum line length for a Javadoc comment after the codesnippet has been injected.
     * <p>
     * Default value is {@code 120} characters.
     */
    @Parameter(property = PROPERTY_PREFIX + "maxLineLength", defaultValue = "120")
    private int maxLineLength;

    /**
     * Skip running the plugin.
     * <p>
     * Default value is false.
     */
    @Parameter(property = PROPERTY_PREFIX + "skip", defaultValue = "false")
    private boolean skip;

    /**
     * Whether execution will fail if there is an error updating or verifying codesnippets.
     * <p>
     * Default value is true.
     */
    @Parameter(property = PROPERTY_PREFIX + "failOnError", defaultValue = "true")
    private boolean failOnError;

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
     * Gets the glob for the README files to inject codesnippets.
     *
     * @return The glob for the README files to inject codesnippets.
     */
    protected String getReadmeGlob() {
        return readmeGlob;
    }

    /**
     * Gets the root directory to begin searching for README files.
     *
     * @return The root directory to begin searching for README files.
     */
    protected File getReadmeRootDirectory() {
        return readmeRootDirectory;
    }

    /**
     * Gets whether the README should be targeted for codesnippet injection or validation.
     *
     * @return Whether the README should be targeted for codesnippet injection or validation.
     */
    protected boolean isIncludeReadme() {
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

    /**
     * Gets whether execution will fail if there is an error updating or verifying codesnippets.
     *
     * @return Whether execution will fail if there is an error updating or verifying codesnippets.
     */
    protected boolean isFailOnError() {
        return failOnError;
    }

    /**
     * Runs codesnippets for the specified {@link ExecutionMode}.
     *
     * @param executionMode The codesnippet execution mode.
     * @throws MojoExecutionException If codesnippets fails to run successfully.
     * @throws MojoFailureException If non-codesnippet exception occurs during processing.
     */
    protected void executeCodesnippet(ExecutionMode executionMode) throws MojoExecutionException, MojoFailureException {
        Log log = getLog();

        if (isSkip()) {
            log.info("Skipping codesnippet execution since skip is set.");
            return;
        }

        Path codesnippetRootDirectory = getAndLogConfiguration("Using codesnippet root directory: %s",
            () -> getCodesnippetRootDirectory().toPath(), log);
        String codesnippetGlob = getAndLogConfiguration("Using codesnippet glob: %s", this::getCodesnippetGlob, log);

        Path sourcesRootDirectory = getAndLogConfiguration("Using sources root directory: %s",
            () -> getSourceRootDirectory().toPath(), log);
        String sourcesGlob = getAndLogConfiguration("Using source glob: %s", this::getSourceGlob, log);

        boolean includeSource = getAndLogConfiguration("Is source included? %b", this::isIncludeSource, log);

        Path readmeRootDirectory = getAndLogConfiguration("Using README root directory: %s",
            () -> getReadmeRootDirectory().toPath(), log);
        String readmeGlob = getAndLogConfiguration("Using README glob: %s", this::getReadmeGlob, log);
        boolean includeReadme = getAndLogConfiguration("Is README included? %b", this::isIncludeReadme, log);

        int maxLineLength = getAndLogConfiguration("Using max line length: %d", this::getMaxLineLength, log);
        boolean failOnError = getAndLogConfiguration("Should fail on error: %b", this::isFailOnError, log);

        if (executionMode == ExecutionMode.UPDATE) {
            try {
                log.debug("Beginning codesnippet update execution.");
                SnippetReplacer.updateCodesnippets(codesnippetRootDirectory, codesnippetGlob, sourcesRootDirectory,
                    sourcesGlob, includeSource, readmeRootDirectory, readmeGlob, includeReadme, maxLineLength,
                    failOnError, log);
                log.debug("Completed codesnippet update execution.");
            } catch (IOException ex) {
                log.error(ex);

                // IOExceptions aren't a codesnippet failure but a general failure.
                throw new MojoFailureException("Failed to update codesnippets.", ex);
            }
        } else if (executionMode == ExecutionMode.VERIFY) {
            try {
                log.debug("Beginning codesnippet verification execution.");
                SnippetReplacer.verifyCodesnippets(codesnippetRootDirectory, codesnippetGlob, sourcesRootDirectory,
                    sourcesGlob, includeSource, readmeRootDirectory, readmeGlob, includeReadme, maxLineLength,
                    failOnError, log);
                log.debug("Completed codesnippet verification execution.");
            } catch (IOException ex) {
                log.error(ex);

                // IOExceptions aren't a codesnippet failure but a general failure.
                throw new MojoFailureException("Failed to verify codesnippets.", ex);
            }
        } else {
            // This fails no matter what the configuration is as this isn't a codesnippet updating error but a
            // configuration error.
            throw new MojoFailureException("Unsupported execution mode '" + executionMode + "' provided.");
        }
    }

    private static <T> T getAndLogConfiguration(String formattable, Supplier<T> configurationGetter, Log log) {
        T configuration = configurationGetter.get();
        log.debug(String.format(formattable, configuration));

        return configuration;
    }
}
