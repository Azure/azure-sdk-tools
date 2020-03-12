package com.azure.tools.apiview.processor.diagnostics.rules;

import com.azure.tools.apiview.processor.diagnostics.DiagnosticRule;
import com.azure.tools.apiview.processor.model.APIListing;
import com.azure.tools.apiview.processor.model.Diagnostic;
import com.github.javaparser.ast.CompilationUnit;

import java.util.Arrays;
import java.util.List;

import static com.azure.tools.apiview.processor.analysers.util.ASTUtils.getClassName;
import static com.azure.tools.apiview.processor.analysers.util.ASTUtils.getPublicOrProtectedMethods;
import static com.azure.tools.apiview.processor.analysers.util.ASTUtils.makeId;

public class BadPrefixesDiagnosticRule implements DiagnosticRule {

    private final List<String> badPrefixes;

    public BadPrefixesDiagnosticRule(String... badPrefixes) {
        if (badPrefixes == null || badPrefixes.length == 0) {
            throw new IllegalArgumentException("BadPrefixesDiagnosticRule created with no bad prefixes");
        }
        this.badPrefixes = Arrays.asList(badPrefixes);
    }

    @Override
    public void scan(final CompilationUnit cu, final APIListing listing) {
        // check all public / protected methods
        getPublicOrProtectedMethods(cu)
                .forEach(methodDeclaration -> check(methodDeclaration.getNameAsString(), makeId(methodDeclaration), listing));
    }

    private void check(String name, String id, APIListing listing) {
        if (badPrefixes.stream().anyMatch(name::startsWith)) {
            listing.addDiagnostic(new Diagnostic(id, "This has a bad prefix."));
        }
    }
}
