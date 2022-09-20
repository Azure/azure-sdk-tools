package com.azure.tools.apiview.processor.diagnostics.rules;

import com.azure.tools.apiview.processor.diagnostics.DiagnosticRule;
import com.azure.tools.apiview.processor.model.APIListing;
import com.azure.tools.apiview.processor.model.Diagnostic;
import com.github.javaparser.ast.CompilationUnit;

import java.util.Arrays;
import java.util.List;

import static com.azure.tools.apiview.processor.analysers.util.ASTUtils.*;

import static com.azure.tools.apiview.processor.model.DiagnosticKind.*;

public class UpperCaseNamingDiagnosticRule implements DiagnosticRule {

    private final List<String> illegalNames;

    public UpperCaseNamingDiagnosticRule(String... illegalNames) {
        if (illegalNames == null || illegalNames.length == 0) {
            throw new IllegalArgumentException("UpperCaseNamingDiagnosticRule created with no illegal names");
        }
        this.illegalNames = Arrays.asList(illegalNames);
    }

    @Override
    public void scanIndividual(final CompilationUnit cu, final APIListing listing) {
        // check class name
        getClassName(cu).ifPresent(name -> check(name, makeId(cu), listing));

        // check all public / protected methods
        getPublicOrProtectedMethods(cu).forEach(methodDeclaration ->
            check(methodDeclaration.getNameAsString(), makeId(methodDeclaration), listing));
    }

    private void check(String name, String id, APIListing listing) {
        if (illegalNames.stream().anyMatch(name::contains)) {
            listing.addDiagnostic(new Diagnostic(WARNING, id, "This is named with incorrect casing."));
        }
    }
}
