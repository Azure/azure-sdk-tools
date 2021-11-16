package com.azure.tools.apiview.processor.diagnostics.rules;

import com.azure.tools.apiview.processor.diagnostics.DiagnosticRule;
import com.azure.tools.apiview.processor.model.APIListing;
import com.azure.tools.apiview.processor.model.Diagnostic;
import com.github.javaparser.ast.CompilationUnit;
import com.github.javaparser.ast.nodeTypes.NodeWithJavadoc;

import java.util.Arrays;
import java.util.Locale;

import static com.azure.tools.apiview.processor.analysers.util.ASTUtils.getClasses;
import static com.azure.tools.apiview.processor.analysers.util.ASTUtils.getPublicOrProtectedConstructors;
import static com.azure.tools.apiview.processor.analysers.util.ASTUtils.getPublicOrProtectedFields;
import static com.azure.tools.apiview.processor.analysers.util.ASTUtils.getPublicOrProtectedMethods;
import static com.azure.tools.apiview.processor.analysers.util.ASTUtils.makeId;
import static com.azure.tools.apiview.processor.model.DiagnosticKind.WARNING;

public class NoLocalesInJavadocUrlDiagnosticRule implements DiagnosticRule {

    private final String[] languageTags;

    public NoLocalesInJavadocUrlDiagnosticRule() {
        languageTags = Arrays.stream(Locale.getAvailableLocales())
           .map(Locale::toLanguageTag)
           .map(String::toLowerCase)
           .toArray(String[]::new);
    }

    @Override
    public void scanIndividual(final CompilationUnit cu, final APIListing listing) {
        getClasses(cu).forEach(typeDeclaration -> {
            // Javadoc on the type itself
            checkJavaDoc(typeDeclaration, makeId(typeDeclaration), listing);

            // then get constructors, fields, and methods and check the javadoc on them
            getPublicOrProtectedConstructors(typeDeclaration).forEach(n -> checkJavaDoc(n, makeId(n), listing));
            getPublicOrProtectedFields(typeDeclaration).forEach(n -> checkJavaDoc(n, makeId(n), listing));
            getPublicOrProtectedMethods(typeDeclaration).forEach(n -> checkJavaDoc(n, makeId(n), listing));
        });
    }

    private void checkJavaDoc(NodeWithJavadoc<?> n, String id, APIListing listing) {
        n.getJavadocComment().ifPresent(javadoc -> {
            final String javadocString = javadoc.toString().toLowerCase();

            Arrays.stream(languageTags).forEach(langTag -> {
                if (javadocString.contains("/" + langTag + "/")) {
                    listing.addDiagnostic(new Diagnostic(WARNING, id,
                        "The JavaDoc string for this API contains what appears to be a locale ('/" + langTag + "/'). " +
                            "Commonly, this indicates a URL in the JavaDoc is linking to a specific locale. If so, this " +
                            "should be removed, so we do not assume about the users locale when accessing a URL."));
                }
            });
        });
    }
}
