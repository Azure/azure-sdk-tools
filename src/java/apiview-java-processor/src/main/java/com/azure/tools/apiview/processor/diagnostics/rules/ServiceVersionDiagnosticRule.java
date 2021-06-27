package com.azure.tools.apiview.processor.diagnostics.rules;

import com.azure.tools.apiview.processor.diagnostics.DiagnosticRule;
import com.azure.tools.apiview.processor.model.APIListing;
import com.azure.tools.apiview.processor.model.Diagnostic;
import com.azure.tools.apiview.processor.model.DiagnosticKind;
import com.github.javaparser.ast.CompilationUnit;

import static com.azure.tools.apiview.processor.analysers.util.ASTUtils.makeId;
import static com.azure.tools.apiview.processor.model.DiagnosticKind.WARNING;

/**
 * This diagnostic rule checks that a track 2 client library defines an enum that implements ServiceVersion.
 */
public class ServiceVersionDiagnosticRule implements DiagnosticRule {
    private boolean foundServiceVersion;
    private String serviceClientPackage;

    @Override
    public void scanIndividual(CompilationUnit cu, APIListing listing) {
        if (cu.getPrimaryTypeName().get().endsWith("Client")) {
            serviceClientPackage = cu.getPackageDeclaration().get().getNameAsString();
        }

        if (cu.getPrimaryTypeName().get().endsWith("ServiceVersion")) {
            foundServiceVersion = true;
            cu.getTypes().forEach(typeDeclaration -> {
                if (!typeDeclaration.isEnumDeclaration()) {
                    listing.addDiagnostic(new Diagnostic(WARNING, makeId(typeDeclaration), "Service version should be" +
                            " an enum and must implement 'ServiceVersion' interface."));
                    return;
                }

                boolean implementsServiceVersion = typeDeclaration.asEnumDeclaration().getImplementedTypes().stream()
                        .anyMatch(type -> type.getNameAsString().equals("ServiceVersion"));
                if (!implementsServiceVersion) {
                    listing.addDiagnostic(new Diagnostic(WARNING, makeId(typeDeclaration), "This type " +
                            "should implement 'ServiceVersion' interface."));
                }
            });
        }
    }

    @Override
    public void scanFinal(APIListing listing) {
        // If ServiceVersion type is not found and the module contains a client
        // show a diagnostic warning about missing service version type.
        if (!foundServiceVersion && serviceClientPackage != null) {
            listing.addDiagnostic(new Diagnostic(DiagnosticKind.WARNING, makeId(serviceClientPackage),
                    "ServiceVersion type is missing."));
        }
    }
}
