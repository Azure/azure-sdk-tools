package com.azure.tools.apiview.processor.diagnostics.rules.general;

import com.azure.tools.apiview.processor.diagnostics.DiagnosticRule;
import com.azure.tools.apiview.processor.model.APIListing;
import com.azure.tools.apiview.processor.model.Diagnostic;
import com.github.javaparser.ast.CompilationUnit;
import com.github.javaparser.ast.body.MethodDeclaration;
import com.github.javaparser.ast.body.TypeDeclaration;

import java.util.concurrent.atomic.AtomicInteger;
import java.util.regex.Pattern;

import static com.azure.tools.apiview.processor.analysers.util.ASTUtils.*;
import static com.azure.tools.apiview.processor.model.DiagnosticKind.INFO;
import static com.azure.tools.apiview.processor.model.DiagnosticKind.WARNING;

/**
 *
 */
public class MissingEqualsAndHashCodeDiagnosticRule implements DiagnosticRule {

    private final Pattern packageRegex;

    public MissingEqualsAndHashCodeDiagnosticRule(String packageRegex) {
        this.packageRegex = Pattern.compile(packageRegex);
    }

    @Override
    public void scanIndividual(final CompilationUnit cu, final APIListing listing) {
        getClasses(cu)
            .filter(type -> packageRegex.matcher(getPackageName(type)).matches())
            .forEach(typeDeclaration -> {
                boolean hasEquals = hasValidEqualsMethod(typeDeclaration);
                boolean hasHashCode = hasValidHashCodeMethod(typeDeclaration);

                if (!hasEquals && !hasHashCode) {
                    listing.addDiagnostic(new Diagnostic(
                            INFO,
                            makeId(typeDeclaration),
                            "Both equals and hashCode methods are missing. Consider adding both methods."
                    ));
                } else if (!hasEquals) {
                    listing.addDiagnostic(new Diagnostic(
                            WARNING,
                            makeId(typeDeclaration),
                            "equals method is missing, but hashCode method is present. Consider adding the equals method."
                    ));
                } else if (!hasHashCode) {
                    listing.addDiagnostic(new Diagnostic(
                            WARNING,
                            makeId(typeDeclaration),
                            "hashCode method is missing, but equals method is present. Consider adding the hashCode method."
                    ));
                }
            });
    }

    private boolean hasValidEqualsMethod(TypeDeclaration<?> typeDeclaration) {
        return typeDeclaration.getMethods().stream()
                .anyMatch(this::isValidEqualsMethod);
    }

    private boolean hasValidHashCodeMethod(TypeDeclaration<?> typeDeclaration) {
        return typeDeclaration.getMethods().stream()
                .anyMatch(this::isValidHashCodeMethod);
    }

    private boolean isValidEqualsMethod(MethodDeclaration method) {
        return method.getNameAsString().equals("equals")
                && method.isAnnotationPresent("Override")
                && method.getParameters().size() == 1
                && method.getParameters().get(0).getType().asString().equals("Object")
                && method.getType().asString().equals("boolean");
    }

    private boolean isValidHashCodeMethod(MethodDeclaration method) {
        return method.getNameAsString().equals("hashCode")
                && method.isAnnotationPresent("Override")
                && method.getParameters().isEmpty()
                && method.getType().asString().equals("int");
    }
}
