// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

package com.azure.tools.codesnippetplugin;

import org.apache.maven.plugin.MojoExecutionException;
import org.apache.maven.plugin.MojoFailureException;
import org.apache.maven.plugins.annotations.LifecyclePhase;
import org.apache.maven.plugins.annotations.Mojo;

/**
 * Mojo for verifying codesnippets.
 */
@Mojo(name = "verify-codesnippet", threadSafe = true, defaultPhase = LifecyclePhase.VERIFY)
public final class VerifyCodesnippet extends SnippetBaseMojo {
    @Override
    public void execute() throws MojoExecutionException, MojoFailureException {
        executeCodesnippet(ExecutionMode.VERIFY);
    }
}
