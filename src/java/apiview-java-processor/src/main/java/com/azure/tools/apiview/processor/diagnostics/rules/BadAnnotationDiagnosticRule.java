package com.azure.tools.apiview.processor.diagnostics.rules;

import com.azure.tools.apiview.processor.diagnostics.DiagnosticRule;
import com.azure.tools.apiview.processor.model.APIListing;
import com.azure.tools.apiview.processor.model.Diagnostic;
import com.github.javaparser.ast.CompilationUnit;
import com.github.javaparser.ast.expr.AnnotationExpr;

import java.util.*;
import java.util.function.Consumer;

import static com.azure.tools.apiview.processor.analysers.util.ASTUtils.*;
import static com.azure.tools.apiview.processor.model.DiagnosticKind.WARNING;

/**
 * Reviews annotations used throughout the source code to ensure that they do not come from our list of known
 * 'bad' annotations. If we detect one being used, we provide feedback to the user about their options.
 */
public class BadAnnotationDiagnosticRule implements DiagnosticRule {

    private List<BadAnnotation> badAnnotations;

    public BadAnnotationDiagnosticRule(BadAnnotation... badAnnotations) {
        this.badAnnotations = Arrays.asList(badAnnotations);
    }

    @Override
    public void scanIndividual(final CompilationUnit cu, final APIListing listing) {
        Consumer<AnnotationExpr> annotationConsumer = a -> {
            badAnnotations.forEach(badAnnotation -> {
                // check if the badAnnotation is the same as the found annotation
                if (badAnnotation.annotation.equals(a.getNameAsString())) {
                    // we've got a match - file it as a diagnostic
                    listing.addDiagnostic(new Diagnostic(WARNING, makeId(a), badAnnotation.errorMessage));
                }
            });
        };

        getClasses(cu).forEach(typeDeclaration -> {
            // check annotations on the type itself
            typeDeclaration.getAnnotations().stream().forEach(annotationConsumer);

            // check annotations on fields, constructors, methods
            getPublicOrProtectedFields(typeDeclaration).flatMap(method -> method.getAnnotations().stream()).forEach(annotationConsumer);
            getPublicOrProtectedConstructors(typeDeclaration).flatMap(method -> method.getAnnotations().stream()).forEach(annotationConsumer);
            getPublicOrProtectedMethods(typeDeclaration).flatMap(method -> method.getAnnotations().stream()).forEach(annotationConsumer);
        });
    }

    public static class BadAnnotation {
        private final String annotation;
        private final String errorMessage;

        public BadAnnotation(String annotation, String errorMessage) {
            this.annotation = annotation;
            this.errorMessage = errorMessage;
        }
    }
}
