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
import java.util.ArrayList;
import java.util.Arrays;
import java.util.List;
import java.util.stream.Collectors;

/**
 * Base Mojo for the codesnippet plugin.
 */
@SuppressWarnings("unused")
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
     * Additional root directories and globs for codesnippets.
     */
    @Parameter
    private RootAndGlob[] additionalCodesnippets;

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
     * Additional root directories and globs for READMEs.
     */
    @Parameter
    private RootAndGlob[] additionalReadmes;

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
     * Runs codesnippets for the specified {@link ExecutionMode}.
     *
     * @param executionMode The codesnippet execution mode.
     * @throws MojoExecutionException If codesnippets fails to run successfully.
     * @throws MojoFailureException If non-codesnippet exception occurs during processing.
     */
    protected void executeCodesnippet(ExecutionMode executionMode) throws MojoExecutionException, MojoFailureException {
        Log log = getLog();

        if (skip) {
            log.info("Skipping codesnippet execution since skip is set.");
            return;
        }

        RootAndGlob codesnippetRootAndGlob = new RootAndGlob()
            .setRoot(logConfiguration("Using codesnippet root directory: %s", codesnippetRootDirectory, log))
            .setGlob(logConfiguration("Using codesnippet glob: %s", codesnippetGlob, log));

        List<RootAndGlob> additionalCodesnippetRootAndGlobs = processRootAndGlobs(additionalCodesnippets,
            DEFAULT_CODESNIPPET_GLOB);
        if (!additionalCodesnippetRootAndGlobs.isEmpty()) {
            log.debug("Using additional codesnippet roots and globs:");
            for (RootAndGlob rootAndGlob : additionalCodesnippetRootAndGlobs) {
                log.debug(String.format("\tRoot: %s, Glob: %s", rootAndGlob.getRoot(), rootAndGlob.getGlob()));
            }
        }

        RootAndGlob sourcesRootAndGlob = new RootAndGlob()
            .setRoot(logConfiguration("Using sources root directory: %s", sourceRootDirectory, log))
            .setGlob(logConfiguration("Using source glob: %s", sourceGlob, log));

        logConfiguration("Is source included? %b", includeSource, log);

        RootAndGlob readmeRootAndGlob = new RootAndGlob()
            .setRoot(logConfiguration("Using README root directory: %s", readmeRootDirectory, log))
            .setGlob(logConfiguration("Using README glob: %s", readmeGlob, log));

        List<RootAndGlob> additionalReadmeRootAndGlobs = processRootAndGlobs(additionalReadmes, DEFAULT_README_GLOB);
        if (!additionalReadmeRootAndGlobs.isEmpty()) {
            log.debug("Using additional README roots and globs:");
            for (RootAndGlob rootAndGlob : additionalReadmeRootAndGlobs) {
                log.debug(String.format("\tRoot: %s, Glob: %s", rootAndGlob.getRoot(), rootAndGlob.getGlob()));
            }
        }

        logConfiguration("Is README included? %b", includeReadme, log);

        logConfiguration("Using max line length: %d", maxLineLength, log);
        logConfiguration("Should fail on error: %b", failOnError, log);

        if (executionMode == ExecutionMode.UPDATE) {
            try {
                log.debug("Beginning codesnippet update execution.");
                SnippetReplacer.updateCodesnippets(codesnippetRootAndGlob, additionalCodesnippetRootAndGlobs,
                    sourcesRootAndGlob, includeSource, readmeRootAndGlob, additionalReadmeRootAndGlobs, includeReadme,
                    maxLineLength, failOnError, log);
                log.debug("Completed codesnippet update execution.");
            } catch (IOException ex) {
                log.error(ex);

                // IOExceptions aren't a codesnippet failure but a general failure.
                throw new MojoFailureException("Failed to update codesnippets.", ex);
            }
        } else if (executionMode == ExecutionMode.VERIFY) {
            try {
                log.debug("Beginning codesnippet verification execution.");
                SnippetReplacer.verifyCodesnippets(codesnippetRootAndGlob, additionalCodesnippetRootAndGlobs,
                    sourcesRootAndGlob, includeSource, readmeRootAndGlob, additionalReadmeRootAndGlobs, includeReadme,
                    maxLineLength, failOnError, log);
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

    private static <T> T logConfiguration(String formattable, T configuration, Log log) {
        log.debug(String.format(formattable, configuration));

        return configuration;
    }

    private static List<RootAndGlob> processRootAndGlobs(RootAndGlob[] rootAndGlobs, String defaultGlob) {
        if (rootAndGlobs == null) {
            return new ArrayList<>();
        }

        for (RootAndGlob rootAndGlob : rootAndGlobs) {
            if (rootAndGlob.getGlob() == null) {
                rootAndGlob.setGlob(defaultGlob);
            }
        }

        return Arrays.stream(rootAndGlobs).collect(Collectors.toList());
    }
}
