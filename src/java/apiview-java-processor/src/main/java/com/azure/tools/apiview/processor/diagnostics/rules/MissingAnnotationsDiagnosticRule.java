package com.azure.tools.apiview.processor.diagnostics.rules;

import com.azure.tools.apiview.processor.diagnostics.DiagnosticRule;
import com.azure.tools.apiview.processor.model.APIListing;
import com.azure.tools.apiview.processor.model.Diagnostic;
import com.github.javaparser.ast.CompilationUnit;

import java.util.concurrent.atomic.AtomicInteger;

import static com.azure.tools.apiview.processor.analysers.util.ASTUtils.getClasses;
import static com.azure.tools.apiview.processor.analysers.util.ASTUtils.getPublicOrProtectedMethods;
import static com.azure.tools.apiview.processor.analysers.util.ASTUtils.makeId;

import static com.azure.tools.apiview.processor.model.DiagnosticKind.*;

public class MissingAnnotationsDiagnosticRule implements DiagnosticRule {

    @Override
    public void scan(final CompilationUnit cu, final APIListing listing) {
        getClasses(cu).forEach(typeDeclaration -> {
            String className = typeDeclaration.getNameAsString();

            if (className.endsWith("Builder")) {
                // check if @ServiceClientBuilder annotation is present
                if (!typeDeclaration.isAnnotationPresent("ServiceClientBuilder")) {
                    listing.addDiagnostic(new Diagnostic(
                        INFO,
                        makeId(cu),
                        "Classes named *Builder are potential candidates to have the @ServiceClientBuilder annotation applied.",
                        "https://azure.github.io/azure-sdk/java_design.html#service-client-builder"));
                }
            } else if (className.endsWith("Client")) {
                // check if the @ServiceClient annotation is present
                if (!typeDeclaration.isAnnotationPresent("ServiceClient")) {
                    listing.addDiagnostic(new Diagnostic(
                        INFO,
                        makeId(cu),
                        "Classes named *Client are potential candidates to have the @ServiceClient annotation applied.",
                        "https://azure.github.io/azure-sdk/java_design.html#service-client"));
                }

                // check all public / protected methods in client classes. Because we can't easily determine if a method
                // should have an annotation, all we can do is count the number of methods that are annotated and compare
                // this with the total number of methods. If the ratio is not high enough, we will warn the user that there
                // may be missing annotations.
                final AtomicInteger methodCount = new AtomicInteger();
                final AtomicInteger annotatedMethodCount = new AtomicInteger();
                getPublicOrProtectedMethods(cu).forEach(methodDeclaration -> {
                    methodCount.incrementAndGet();
                    if (methodDeclaration.isAnnotationPresent("ServiceMethod")) {
                        annotatedMethodCount.incrementAndGet();
                    }
                });

                if (annotatedMethodCount.get() / (double) methodCount.get() < 0.75) {
                    // warn user to double check
                    listing.addDiagnostic(new Diagnostic(
                        WARNING,
                        makeId(cu),
                    "There is a low number of methods annotated with @ServiceMethod. " +
                            "Please review to ensure all appropriate methods have this annotation.",
                        "https://azure.github.io/azure-sdk/java_design.html#service-client"));
                }
            }
        });
    }
}
