package com.azure.tools.apiview.processor.diagnostics.rules;

import com.azure.tools.apiview.processor.diagnostics.DiagnosticRule;
import com.azure.tools.apiview.processor.model.APIListing;
import com.azure.tools.apiview.processor.model.Diagnostic;
import com.github.javaparser.ast.CompilationUnit;
import com.github.javaparser.ast.nodeTypes.NodeWithJavadoc;

import static com.azure.tools.apiview.processor.analysers.util.ASTUtils.getClasses;
import static com.azure.tools.apiview.processor.analysers.util.ASTUtils.getPublicOrProtectedConstructors;
import static com.azure.tools.apiview.processor.analysers.util.ASTUtils.getPublicOrProtectedFields;
import static com.azure.tools.apiview.processor.analysers.util.ASTUtils.getPublicOrProtectedMethods;
import static com.azure.tools.apiview.processor.analysers.util.ASTUtils.makeId;
import static com.azure.tools.apiview.processor.model.DiagnosticKind.WARNING;

public class MissingJavaDocDiagnosticRule implements DiagnosticRule {
    private static boolean IGNORE_OVERRIDES = true;

    @Override
    public void scanIndividual(final CompilationUnit cu, final APIListing listing) {
        getClasses(cu).forEach(typeDeclaration -> {
            if (!typeDeclaration.hasJavaDocComment()) {
                // the type is missing JavaDoc
                listing.addDiagnostic(new Diagnostic(WARNING, makeId(typeDeclaration), "This API is missing JavaDoc."));
            }

            getPublicOrProtectedConstructors(typeDeclaration).forEach(n -> checkJavaDoc(n, makeId(n), listing));
            getPublicOrProtectedFields(typeDeclaration).forEach(n -> checkJavaDoc(n, makeId(n), listing));

            getPublicOrProtectedMethods(typeDeclaration).forEach(n -> {
                if (IGNORE_OVERRIDES && n.isAnnotationPresent("Override")) {
                    // no-op - we don't check for Javadoc on methods that are overrides - we assume that the parent
                    // class from which this method is specified already has sufficiently detailed JavaDoc and we have
                    // validated that elsewhere.
                } else {
                    checkJavaDoc(n, makeId(n), listing);
                }
            });
        });
    }

    private void checkJavaDoc(NodeWithJavadoc n, String id, APIListing listing) {
        if (!n.hasJavaDocComment()) {
            listing.addDiagnostic(new Diagnostic(WARNING, id, "This API is missing JavaDoc."));
        }
    }
}
