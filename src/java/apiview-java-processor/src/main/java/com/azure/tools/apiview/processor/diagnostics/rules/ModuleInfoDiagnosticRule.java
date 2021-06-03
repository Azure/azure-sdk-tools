package com.azure.tools.apiview.processor.diagnostics.rules;

import com.azure.tools.apiview.processor.analysers.ASTAnalyser;
import com.azure.tools.apiview.processor.diagnostics.DiagnosticRule;
import com.azure.tools.apiview.processor.model.APIListing;
import com.azure.tools.apiview.processor.model.Diagnostic;
import com.azure.tools.apiview.processor.model.DiagnosticKind;
import com.azure.tools.apiview.processor.model.Token;
import com.azure.tools.apiview.processor.model.TokenKind;
import com.github.javaparser.ast.CompilationUnit;

import java.util.ArrayList;
import java.util.Comparator;
import java.util.HashSet;
import java.util.List;
import java.util.Optional;
import java.util.Set;
import java.util.stream.Collectors;

import static com.azure.tools.apiview.processor.analysers.util.ASTUtils.makeId;

/**
 * This diagnostic rule checks that the module has `module-info.java` and also validates that the
 * name of the module matches the base package name.
 */
public class ModuleInfoDiagnosticRule implements DiagnosticRule {
    private String moduleName;
    private Set<String> packages = new HashSet<>();

    @Override
    public void scanIndividual(CompilationUnit cu, APIListing listing) {
        packages.add(cu.getPackageDeclaration().get().getNameAsString());
    }

    @Override
    public void scanFinal(APIListing listing) {
        // In this method, we first look for the presence of module-info.java.
        // If not present, add a warning message at the base package level
        // If present, validate that the module name is the same as the base package name

        // Base package name is the package that has the shortest name in a module
        String basePackageName = packages
                .stream()
                .min(Comparator.comparingInt(String::length))
                .orElse("");

        // Check for the presence of module-info
        Optional<Token> moduleInfoToken = listing.getTokens().stream()
                .filter(token -> token.getKind().equals(TokenKind.TYPE_NAME))
                .filter(token -> token.getDefinitionId() != null && token.getDefinitionId().equals(ASTAnalyser.MODULE_INFO_KEY))
                .findFirst();

        // Collect all packages that are exported
        Set<String> exportsPackages = listing.getTokens().stream()
                .filter(token -> token.getKind().equals(TokenKind.TYPE_NAME))
                .filter(token -> token.getDefinitionId() != null && token.getDefinitionId().startsWith("module-info" +
                        "-exports"))
                .map(token -> token.getValue())
                .collect(Collectors.toSet());

        if (!moduleInfoToken.isPresent()) {
            listing.addDiagnostic(new Diagnostic(DiagnosticKind.WARNING, makeId(basePackageName),
                    "This module is missing module-info.java"));
            return;
        }

        moduleName = moduleInfoToken.get().getValue();
        if (moduleName != null) {
            // special casing azure-core as the base package doesn't have any classes and hence not included in the
            // list of packages
            if (!moduleName.equals(basePackageName) && !moduleName.equals("com.azure.core")) {
                // add warning message if the module name does not match the base package name
                listing.addDiagnostic(new Diagnostic(DiagnosticKind.WARNING,
                        makeId(ASTAnalyser.MODULE_INFO_KEY), "Module name should be the same as base package " +
                        "name: " + basePackageName));
            }

            // Validate that all public packages are exported in module-info
            packages.stream()
                    .filter(publicPackage -> !exportsPackages.contains(publicPackage))
                    .forEach(missingExport -> {
                        listing.addDiagnostic(new Diagnostic(DiagnosticKind.ERROR,
                                makeId(ASTAnalyser.MODULE_INFO_KEY), "Public package not exported: " + missingExport));
                    });
        }
    }
}
