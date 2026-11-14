// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

package com.azure.tools.apiviewplugin;

import com.azure.tools.apiview.processor.Main;
import com.azure.tools.apiview.processor.model.ApiListing;
import java.io.File;
import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.Paths;
import org.apache.maven.artifact.Artifact;
import org.apache.maven.plugin.AbstractMojo;
import org.apache.maven.plugin.MojoExecutionException;
import org.apache.maven.plugin.MojoFailureException;
import org.apache.maven.plugins.annotations.LifecyclePhase;
import org.apache.maven.plugins.annotations.Mojo;
import org.apache.maven.plugins.annotations.Parameter;
import org.apache.maven.project.MavenProject;

/**
 * Generates api.md and api.metadata.yml from a package's built sources JAR.
 */
@Mojo(name = "generate", defaultPhase = LifecyclePhase.PACKAGE, threadSafe = true)
public class GenerateMojo extends AbstractMojo {
    @Parameter(defaultValue = "${project}", readonly = true, required = true)
    private MavenProject project;

    /** Existing sources JAR; defaults to the attached sources artifact or build output. */
    @Parameter(property = "apiview.sourcesJar")
    private File sourcesJar;

    /** Build-artifact destination, never the package root. */
    @Parameter(property = "apiview.outputDirectory", defaultValue = "${project.build.directory}/api-review")
    private File outputDirectory;

    /** Repository root for exported sources without Git metadata. */
    @Parameter(property = "apiview.repositoryRoot")
    private File repositoryRoot;

    /** Explicit repository-relative module path, using forward slashes. */
    @Parameter(property = "apiview.packageRelativePath")
    private String packageRelativePath;

    /** Skips generation. */
    @Parameter(property = "apiview.skip", defaultValue = "false")
    private boolean skip;

    @Override
    public void execute() throws MojoExecutionException, MojoFailureException {
        if (skip || "pom".equals(project.getPackaging())) {
            getLog().info("Skipping API review generation.");
            return;
        }
        String version = project.getOriginalModel().getVersion();
        if (version == null || version.trim().isEmpty() || version.contains("${")) {
            throw new MojoFailureException("The package POM must declare a literal project version.");
        }
        version = version.trim();
        Path packageDirectory = project.getBasedir().toPath().toAbsolutePath().normalize();
        File input = findSourcesJar(packageDirectory);
        if (!input.isFile() || !input.getName().endsWith("-sources.jar")) {
            throw new MojoFailureException("Expected an existing -sources.jar: " + input
                + ". Build the package first, or set apiview.sourcesJar.");
        }
        Path output = resolve(packageDirectory, outputDirectory).toAbsolutePath().normalize();
        if (output.equals(packageDirectory)) {
            throw new MojoFailureException("Output must be a build artifact directory, not the package root.");
        }
        try {
            String relativePath = resolvePackagePath(packageDirectory);
            ApiListing listing = Main.processSourcesJar(input);
            if (!version.equals(listing.getPackageVersion())
                || !project.getArtifactId().equals(listing.getMavenPom().getArtifactId())
                || (!listing.getMavenPom().getGroupId().isEmpty()
                    && !project.getGroupId().equals(listing.getMavenPom().getGroupId()))) {
                throw new MojoFailureException("Sources JAR identity does not match the current Maven package.");
            }
            new ReviewBundleWriter().write(listing, version, relativePath, output);
            getLog().info("Generated API review bundle in " + output);
        } catch (IOException | RuntimeException exception) {
            throw new MojoExecutionException("Failed to generate the API review bundle.", exception);
        }
    }

    private File findSourcesJar(Path packageDirectory) {
        if (sourcesJar != null) {
            return resolve(packageDirectory, sourcesJar).toFile();
        }
        for (Artifact artifact : project.getAttachedArtifacts()) {
            if ("sources".equals(artifact.getClassifier()) && artifact.getFile() != null) {
                return artifact.getFile();
            }
        }
        return new File(project.getBuild().getDirectory(), project.getBuild().getFinalName() + "-sources.jar");
    }

    private String resolvePackagePath(Path packageDirectory) throws IOException, MojoFailureException {
        if (packageRelativePath != null) {
            String value = packageRelativePath.replace('\\', '/');
            if (value.trim().isEmpty() || value.startsWith("/") || value.matches("^[A-Za-z]:.*")
                || value.contains("\n") || value.contains("\r")) {
                throw new MojoFailureException("apiview.packageRelativePath must be a nonempty repository-relative path.");
            }
            for (String segment : value.split("/")) {
                if ("..".equals(segment)) {
                    throw new MojoFailureException("apiview.packageRelativePath cannot contain '..'.");
                }
            }
            String normalized = Paths.get(value).normalize().toString().replace('\\', '/');
            return normalized.isEmpty() ? "." : normalized;
        }
        Path root = repositoryRoot == null ? findGitRoot(packageDirectory) : resolve(packageDirectory, repositoryRoot);
        if (root == null) {
            throw new MojoFailureException("Cannot locate the repository root. Set apiview.repositoryRoot"
                + " or apiview.packageRelativePath.");
        }
        root = root.toRealPath();
        Path directory = packageDirectory.toRealPath();
        if (!directory.startsWith(root)) {
            throw new MojoFailureException("The package directory is outside apiview.repositoryRoot.");
        }
        String result = root.relativize(directory).toString().replace('\\', '/');
        return result.isEmpty() ? "." : result;
    }

    private static Path findGitRoot(Path directory) {
        for (Path current = directory; current != null; current = current.getParent()) {
            if (Files.exists(current.resolve(".git"))) {
                return current;
            }
        }
        return null;
    }

    private static Path resolve(Path packageDirectory, File file) {
        Path path = file.toPath();
        return path.isAbsolute() ? path : packageDirectory.resolve(path);
    }
}
