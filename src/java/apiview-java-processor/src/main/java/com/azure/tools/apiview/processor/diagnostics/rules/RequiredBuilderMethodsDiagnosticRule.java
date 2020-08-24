package com.azure.tools.apiview.processor.diagnostics.rules;

import com.azure.tools.apiview.processor.diagnostics.DiagnosticRule;
import com.azure.tools.apiview.processor.model.APIListing;
import com.azure.tools.apiview.processor.model.Diagnostic;
import com.github.javaparser.ast.CompilationUnit;
import com.github.javaparser.ast.body.MethodDeclaration;
import com.github.javaparser.ast.body.Parameter;
import com.github.javaparser.ast.type.ClassOrInterfaceType;
import com.github.javaparser.ast.type.Type;

import java.util.Arrays;
import java.util.HashMap;
import java.util.Map;
import java.util.Optional;
import java.util.concurrent.atomic.AtomicInteger;
import java.util.function.Consumer;
import java.util.function.Function;

import static com.azure.tools.apiview.processor.analysers.util.ASTUtils.*;

/**
 * Not all builders require all methods, but we should warn regardless so it can be considered.
 */
public class RequiredBuilderMethodsDiagnosticRule implements DiagnosticRule {

    // maps from the expected method name to the type of the arguments passed into that method.
    // Normally this would be a single argument, but we allow here for multiple arguments, just in case.
    private final Map<String, Function<MethodDeclaration, Optional<Diagnostic>>> builderMethods;

    public RequiredBuilderMethodsDiagnosticRule() {
        this.builderMethods = new HashMap<>();
    }

    public RequiredBuilderMethodsDiagnosticRule add(String methodName, Function<MethodDeclaration, Optional<Diagnostic>> func) {
        builderMethods.put(methodName, func);
        return this;
    }

    @Override
    public void scan(final CompilationUnit cu, final APIListing listing) {
        // check if the class has the @ServiceClientBuilder annotation, if not, do nothing
        getClasses(cu).forEach(typeDeclaration -> {
            if (typeDeclaration.isAnnotationPresent("ServiceClientBuilder")) {
                AtomicInteger count = new AtomicInteger();
                getPublicOrProtectedMethods(typeDeclaration).forEach(methodDeclaration -> {
                    String methodName = methodDeclaration.getNameAsString();
                    if (builderMethods.containsKey(methodName)) {
                        count.incrementAndGet();
                        builderMethods.get(methodName).apply(methodDeclaration).ifPresent(listing::addDiagnostic);
                    }
                });

                if (count.get() < builderMethods.size()) {
                    listing.addDiagnostic(new Diagnostic(makeId(cu),
                            "Not all expected builder methods are present.",
                            "https://azure.github.io/azure-sdk/java_design.html#java-service-client-builder-consistency"));
                }
            }
        });
    }

    public static class ExactTypeNameCheckFunction implements Function<MethodDeclaration, Optional<Diagnostic>> {
        private final ParameterAllowedTypes[] expectedTypes;

        // For each parameter that we check for, we allow for there to be multiple types allowed for it,
        // e.g. credential(TokenCredential) or credential(AzureKeyCredential)
        public ExactTypeNameCheckFunction(ParameterAllowedTypes... expectedTypes) {
            this.expectedTypes = expectedTypes;
        }

        public ExactTypeNameCheckFunction(String... expectedTypes) {
            this(Arrays.stream(expectedTypes).map(ParameterAllowedTypes::new).toArray(ParameterAllowedTypes[]::new));
        }

        @Override
        public Optional<Diagnostic> apply(final MethodDeclaration methodDeclaration) {
            for (int i = 0; i < expectedTypes.length; i++) {
                ParameterAllowedTypes expectedType = expectedTypes[i];
                Type actualType = methodDeclaration.getParameter(i).getType();

                String actualTypeName = ((ClassOrInterfaceType)actualType).getNameAsString();

                if (!expectedType.supports(actualTypeName)) {
                    return Optional.of(
                            new Diagnostic(
                                    makeId(methodDeclaration),
                                    "Incorrect type being supplied to this builder method. Expected " + expectedType +
                                            ", but was " + actualTypeName + "."));
                }
            }

            return Optional.empty();
        }
    }

    public static class DirectSubclassCheckFunction implements Function<MethodDeclaration, Optional<Diagnostic>> {
        private final String parentTypeName;

        public DirectSubclassCheckFunction(String parentTypeName) {
            this.parentTypeName = parentTypeName;
        }

        @Override
        public Optional<Diagnostic> apply(final MethodDeclaration methodDeclaration) {
            Type parameterType = methodDeclaration.getParameter(0).getType();
            ClassOrInterfaceType classOrInterfaceType = parameterType.asClassOrInterfaceType();

            // TODO

            return Optional.empty();
        }
    }

    public static class ParameterAllowedTypes {
        private final String[] allowedTypes;

        public ParameterAllowedTypes(String... allowedTypes) {
            this.allowedTypes = allowedTypes;
        }

        public boolean supports(String type) {
            for (int i = 0; i < allowedTypes.length; i++) {
                if (type.equals(allowedTypes[i])) {
                    return true;
                }
            }
            return false;
        }

        @Override
        public String toString() {
            return "[" + Arrays.toString(allowedTypes) + "]";
        }
    }
}
