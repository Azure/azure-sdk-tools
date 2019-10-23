package com.azure.tools.apiview.processor.diagnostics.rules;

import com.azure.tools.apiview.processor.diagnostics.DiagnosticRule;
import com.azure.tools.apiview.processor.model.APIListing;
import com.azure.tools.apiview.processor.model.Diagnostic;
import com.github.javaparser.ast.CompilationUnit;

import static com.azure.tools.apiview.processor.analysers.util.ASTUtils.*;

public class PackageNameDiagnosticRule implements DiagnosticRule {

    @Override
    public void scan(final CompilationUnit cu, final APIListing listing) {
        getPackageName(cu).ifPresent(packageName -> {
            // we need to map the issue to the class id, because package text isn't printed in the APIView output
            getClassName(cu).map(listing.getKnownTypes()::get).ifPresent(typeId -> {
                if (!packageName.startsWith("com.azure")) {
                    listing.addDiagnostic(new Diagnostic(typeId, "Package name must start with 'com.azure'."));
                }

                if (packageName.contains("_")) {
                    listing.addDiagnostic(new Diagnostic(typeId, "Package name must not have underscores."));
                }

                if (packageName.contains("-")) {
                    listing.addDiagnostic(new Diagnostic(typeId, "Package name must not have hyphens."));
                }

                if (!packageName.equals(packageName.toLowerCase())) {
                    listing.addDiagnostic(new Diagnostic(typeId, "Package name must be entirely lower-case."));
                }
            });
        });
    }
}
