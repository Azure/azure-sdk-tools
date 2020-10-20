package com.azure.tools.apiview.processor.diagnostics.rules;

import com.azure.tools.apiview.processor.diagnostics.DiagnosticRule;
import com.azure.tools.apiview.processor.model.APIListing;
import com.azure.tools.apiview.processor.model.Diagnostic;
import com.github.javaparser.ast.CompilationUnit;
import com.github.javaparser.ast.Modifier;

import static com.azure.tools.apiview.processor.analysers.util.ASTUtils.makeId;

import static com.azure.tools.apiview.processor.model.DiagnosticKind.*;

public class ConsiderFinalClassDiagnosticRule implements DiagnosticRule {

    @Override
    public void scan(final CompilationUnit cu, final APIListing listing) {
        // TODO we would ideally delay this diagnostic until we have completed analysing, so that we can look up
        // to see if any other types extend from this particular compilation unit. At present we somewhat embarrasingly
        // tell people to consider final for types that are extended in the same library.
        cu.getTypes().forEach(type -> {
            if (type.isEnumDeclaration()) return;
            if (type.hasModifier(Modifier.Keyword.ABSTRACT)) return;
            if (type.isClassOrInterfaceDeclaration() && type.asClassOrInterfaceDeclaration().isInterface()) return;
            if (type.isAnnotationDeclaration()) return;
            if (!type.hasModifier(Modifier.Keyword.FINAL)) {
                listing.addDiagnostic(new Diagnostic(
                    INFO,
                    makeId(type),
                    "Consider making all classes final by default - only make non-final if subclassing is supported."));
            }
        });
    }
}
