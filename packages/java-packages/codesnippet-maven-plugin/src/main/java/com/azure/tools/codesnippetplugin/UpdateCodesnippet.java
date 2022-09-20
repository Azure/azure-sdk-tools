// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

package com.azure.tools.codesnippetplugin;


import org.apache.maven.plugin.MojoExecutionException;
import org.apache.maven.plugin.MojoFailureException;
import org.apache.maven.plugins.annotations.LifecyclePhase;
import org.apache.maven.plugins.annotations.Mojo;

/**
 * Mojo for updating codesnippets.
 */
@Mojo(name = "update-codesnippet", threadSafe = true, defaultPhase = LifecyclePhase.PROCESS_SOURCES)
public final class UpdateCodesnippet extends SnippetBaseMojo {
    @Override
    public void execute() throws MojoExecutionException, MojoFailureException {
        executeCodesnippet(ExecutionMode.UPDATE);
    }
}
