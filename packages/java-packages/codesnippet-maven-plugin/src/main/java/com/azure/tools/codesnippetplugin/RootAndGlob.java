// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

package com.azure.tools.codesnippetplugin;

import org.apache.maven.plugins.annotations.Parameter;

import java.io.File;
import java.io.IOException;
import java.nio.file.FileSystems;
import java.nio.file.FileVisitResult;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.PathMatcher;
import java.nio.file.SimpleFileVisitor;
import java.nio.file.attribute.BasicFileAttributes;
import java.util.ArrayList;
import java.util.List;
import java.util.Objects;

/**
 * Contains a search root and file glob that is used to determine which files to include in processing.
 */
public final class RootAndGlob {
    @Parameter
    private File root;

    @Parameter
    private String glob;

    /**
     * Creates a new {@link RootAndGlob}.
     */
    public RootAndGlob() {
    }

    /**
     * Gets the root directory to begin searching.
     *
     * @return The root directory where searching begins.
     */
    public File getRoot() {
        return root;
    }

    /**
     * Sets the root directory to begin searching.
     *
     * @param root The root directory where searching begins.
     * @return The updated RootAndGlob object.
     * @throws NullPointerException If {@code root} is null.
     */
    public RootAndGlob setRoot(File root) {
        this.root = Objects.requireNonNull(root, "'root' cannot be null.");
        return this;
    }

    /**
     * Gets whether the root directory exists.
     *
     * @return Whether the root directory exists.
     */
    public boolean rootExists() {
        return root.exists();
    }

    /**
     * Gets the glob that determines which files to include.
     *
     * @return The glob that determines which files to include.
     */
    public String getGlob() {
        return glob;
    }

    /**
     * Sets the glob that determines which files to include.
     *
     * @param glob The glob that determines which files to include.
     * @return The updated RootAndGlob object.
     * @throws NullPointerException If {@code glob} is null.
     */
    public RootAndGlob setGlob(String glob) {
        this.glob = Objects.requireNonNull(glob, "'glob' cannot be null.");
        return this;
    }

    /**
     * Gets the list of files that are included based on the root and glob.
     *
     * @return The list of files contained in the root directory that match the glob.
     * @throws IOException If an I/O failure occurs while walking the root directory.
     */
    public List<Path> globFiles() throws IOException {
        List<Path> locatedPaths = new ArrayList<>();
        PathMatcher pathMatcher = FileSystems.getDefault().getPathMatcher("glob:" + glob);

        Files.walkFileTree(root.toPath(), new SimpleFileVisitor<Path>() {
            @Override
            public FileVisitResult visitFile(Path file, BasicFileAttributes attrs) {
                if (pathMatcher.matches(file)) {
                    locatedPaths.add(file);
                }
                return FileVisitResult.CONTINUE;
            }

            @Override
            public FileVisitResult visitFileFailed(Path file, IOException exc) {
                return FileVisitResult.CONTINUE;
            }
        });

        return locatedPaths;
    }
}
