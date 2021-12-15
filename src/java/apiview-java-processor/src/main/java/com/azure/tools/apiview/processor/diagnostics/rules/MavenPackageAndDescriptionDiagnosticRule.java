// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

package com.azure.tools.apiview.processor.diagnostics.rules;

import com.azure.tools.apiview.processor.diagnostics.DiagnosticRule;
import com.azure.tools.apiview.processor.model.APIListing;
import com.azure.tools.apiview.processor.model.Diagnostic;
import com.azure.tools.apiview.processor.model.DiagnosticKind;
import com.azure.tools.apiview.processor.model.Token;
import com.azure.tools.apiview.processor.model.maven.Pom;
import com.github.javaparser.ast.CompilationUnit;

import java.util.regex.Pattern;

import static com.azure.tools.apiview.processor.model.TokenKind.TEXT;

/**
 * Diagnostic rule that validates the Maven package name and description match the convention specified in the
 * guidelines.
 */
public final class MavenPackageAndDescriptionDiagnosticRule implements DiagnosticRule {
    private static final Pattern MAVEN_NAME = Pattern.compile("Microsoft Azure client library for .*");
    private static final Pattern MAVEN_DESCRIPTION =
        Pattern.compile("This package contains the Microsoft Azure .* client library");


    @Override
    public void scanIndividual(CompilationUnit cu, APIListing listing) {
        Pom pom = listing.getMavenPom();

        // Maven name
        String nameId = getId("name", pom.getName());
        if (!MAVEN_NAME.matcher(pom.getName()).matches()) {
            listing.addDiagnostic(new Diagnostic(DiagnosticKind.WARNING, nameId,
                "Maven library name should follow the pattern 'Microsoft Azure client library for <service name>'.",
                "https://azure.github.io/azure-sdk/java_introduction.html#java-maven-name"));
        }

        // Maven description
        String descriptionId = getId("description", pom.getDescription());
        if (!MAVEN_DESCRIPTION.matcher(pom.getDescription()).matches()) {
            listing.addDiagnostic(new Diagnostic(DiagnosticKind.WARNING, descriptionId,
                "Maven library description should follow the pattern 'This package contains the Microsoft Azure <service> client library'.",
                "https://azure.github.io/azure-sdk/java_introduction.html#java-maven-description"));
        }
    }

    private static String getId(String key, Object value) {
        return new Token(TEXT, value == null ? "<default value>" : value.toString(), "-" + key + "-" + value)
            .getDefinitionId();
    }
}
