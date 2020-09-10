package com.azure.tools.apiview.processor.diagnostics.rules;

import com.azure.tools.apiview.processor.diagnostics.DiagnosticRule;
import com.azure.tools.apiview.processor.model.APIListing;
import com.azure.tools.apiview.processor.model.Diagnostic;
import com.github.javaparser.ast.CompilationUnit;
import com.github.javaparser.ast.Modifier;

import static com.azure.tools.apiview.processor.analysers.util.ASTUtils.makeId;

public class ConsiderFinalClassDiagnosticRule implements DiagnosticRule {

    @Override
    public void scan(final CompilationUnit cu, final APIListing listing) {
        cu.getTypes().forEach(type -> {
            if (type.isEnumDeclaration()) return;
            if (type.hasModifier(Modifier.Keyword.ABSTRACT)) return;
            if (!type.hasModifier(Modifier.Keyword.FINAL)) {
                listing.addDiagnostic(new Diagnostic(makeId(type),
                        "Consider making all classes final by default - only make non-final if subclassing is supported."));
            }
        });
    }
}
