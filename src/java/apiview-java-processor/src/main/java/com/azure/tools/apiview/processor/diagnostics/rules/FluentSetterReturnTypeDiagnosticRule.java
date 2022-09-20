package com.azure.tools.apiview.processor.diagnostics.rules;

import com.azure.tools.apiview.processor.diagnostics.DiagnosticRule;
import com.azure.tools.apiview.processor.model.APIListing;
import com.azure.tools.apiview.processor.model.Diagnostic;
import com.github.javaparser.ast.CompilationUnit;
import com.github.javaparser.ast.body.TypeDeclaration;

import static com.azure.tools.apiview.processor.analysers.util.ASTUtils.getPublicOrProtectedMethods;
import static com.azure.tools.apiview.processor.analysers.util.ASTUtils.makeId;
import static com.azure.tools.apiview.processor.model.DiagnosticKind.*;

public class FluentSetterReturnTypeDiagnosticRule implements DiagnosticRule {

    public FluentSetterReturnTypeDiagnosticRule() {
        // no-op
    }

    @Override
    public void scanIndividual(final CompilationUnit cu, final APIListing listing) {
        cu.getTypes().forEach(type ->
            type.getAnnotationByName("Fluent").ifPresent(a -> processFluentType(type, listing)));
    }

    private void processFluentType(final TypeDeclaration<?> type, final APIListing listing) {
        // get all setter methods (we will find them just by looking for all methods that start with 'set')
        final String typeName = type.getNameAsString();

        getPublicOrProtectedMethods(type)
                .filter(method -> method.getNameAsString().startsWith("set"))
                .forEach(method -> {
                    if (!method.getType().toString().equals(typeName)) {
                        listing.addDiagnostic(new Diagnostic(
                            ERROR,
                            makeId(method),
                            "Setter methods in a @Fluent class must return the same type as the fluent type."));
                    }
                });
    }
}
