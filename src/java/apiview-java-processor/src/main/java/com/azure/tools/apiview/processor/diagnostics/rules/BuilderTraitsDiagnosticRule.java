package com.azure.tools.apiview.processor.diagnostics.rules;

import com.azure.tools.apiview.processor.diagnostics.DiagnosticRule;
import com.azure.tools.apiview.processor.model.APIListing;
import com.azure.tools.apiview.processor.model.Diagnostic;
import com.github.javaparser.ast.CompilationUnit;
import com.github.javaparser.ast.body.MethodDeclaration;
import com.github.javaparser.ast.body.Parameter;

import java.util.*;

import static com.azure.tools.apiview.processor.analysers.util.ASTUtils.*;
import static com.azure.tools.apiview.processor.model.DiagnosticKind.ERROR;
import static com.azure.tools.apiview.processor.model.DiagnosticKind.WARNING;

/**
 * This diagnostic ensures that builder traits are applied correctly against client builders.
 */
public class BuilderTraitsDiagnosticRule implements DiagnosticRule {
    private final Map<String, List<TraitMethod>> traits;

    public BuilderTraitsDiagnosticRule() {
        traits = new HashMap<>();
        traits.put("AzureKeyCredentialTrait", Arrays.asList(
            new TraitMethod("credential", "AzureKeyCredential")
        ));
        traits.put("AzureNamedKeyCredentialTrait", Arrays.asList(
            new TraitMethod("credential", "AzureNamedKeyCredential")
        ));
        traits.put("AzureSasCredentialTrait", Arrays.asList(
            new TraitMethod("credential", "AzureSasCredential")
        ));
        traits.put("TokenCredentialTrait", Arrays.asList(
            new TraitMethod("credential", "TokenCredential")
        ));
        traits.put("ConfigurationTrait", Arrays.asList(
            new TraitMethod("configuration", "Configuration")
        ));
        traits.put("ConnectionStringTrait", Arrays.asList(
            new TraitMethod("connectionString", "String")
        ));
        traits.put("EndpointTrait", Arrays.asList(
            new TraitMethod("endpoint", "String")
        ));
        traits.put("HttpTrait", Arrays.asList(
            new TraitMethod("httpClient", "HttpClient"),
            new TraitMethod("pipeline", "HttpPipeline"),
            new TraitMethod("addPolicy", "HttpPipelinePolicy"),
            new TraitMethod("retryOptions", "RetryOptions"),
            new TraitMethod("httpLogOptions", "HttpLogOptions"),
            new TraitMethod("clientOptions", "ClientOptions")
        ));
        traits.put("AmqpTrait", Arrays.asList(
            new TraitMethod("retryOptions", "AmqpRetryOptions"),
            new TraitMethod("transportType", "AmqpTransportType"),
            new TraitMethod("proxyOptions", "ProxyOptions"),
            new TraitMethod("clientOptions", "ClientOptions")
        ));
    }

    @Override
    public void scanIndividual(final CompilationUnit cu, final APIListing listing) {
        // If the CU has a @ServiceClientBuilder annotation:
        //   Look for methods with names of methods we know should belong to the trait:
        //     Ensure the matching trait is stated as being implemented. If the method exists but the trait interface is
        //     not defined as being implemented, add a warning diagnostic that the trait should be implemented.
        //     If the method name exists, but it has different parameters that what we expect, add a warning diagnostic
        //     suggesting to use the trait.

        cu.getTypes().forEach(type -> {
            if (!type.isAnnotationPresent("ServiceClientBuilder")) return;

            // iterate through trait-by-trait
            for (Map.Entry<String, List<TraitMethod>> trait : traits.entrySet()) {
                String traitName = trait.getKey();
                List<TraitMethod> traitMethods = trait.getValue();

                for (TraitMethod traitMethod : traitMethods) {
                    String methodName = traitMethod.methodName;

                    // firstly check if the exact method (name + params) is present
                    List<MethodDeclaration> matchingMethods = type.getMethodsBySignature(methodName, traitMethod.methodParamTypes);
                    if (!matchingMethods.isEmpty()) {
                        // ensure we implement the trait
                        if (!isTypeImplementingInterface(type, traitName)) {
                            // we have an exact match to a trait method, but we do not implement the trait!
                            for (MethodDeclaration m : matchingMethods) {
                                listing.addDiagnostic(new Diagnostic(
                                        ERROR,
                                        makeId(m),
                                        "This builder has methods that exactly match a method in the " + traitName
                                                + " trait, but the builder does not implement this trait. Consider " +
                                                "implementing the trait to ensure greater consistency."));
                            }
                        }
                    }

                    // now check for other methods with the trait method name (and some / all of the expected params),
                    // but with additional non-standard params as well
                    List<MethodDeclaration> matchingNameMethods = type.getMethodsByName(methodName);
                    for (MethodDeclaration matchingNameMethod : matchingNameMethods) {
                        int matchingParams = 0;
                        for (Parameter parameter : matchingNameMethod.getParameters()) {
                            for (String traitParamType : traitMethod.methodParamTypes) {
                                if (parameter.getTypeAsString().equals(traitParamType)) {
                                    matchingParams++;
                                }
                            }
                        }

                        if (matchingParams > 0 && matchingParams < matchingNameMethod.getParameters().size()) {
                            // the method has a matching name, and some of the params match, but there are extra ones
                            // that mean this method is non-standard and not part of the trait.
                            String message = isTypeImplementingInterface(type, traitName) ?
                                    "This builder implements the " + traitName + " trait, but offers duplicate " +
                                    "functionality. Consider whether this impacts consistency with other builders." :
                                    "This builder does not implement the " + traitName + " trait, but offers " +
                                    "similar functionality. Consider implementing the trait and aligning the parameters " +
                                    "in this builder method.";
                            listing.addDiagnostic(new Diagnostic(WARNING, makeId(matchingNameMethod), message));
                        }
                    }
                }
            }
        });
    }

    private static class TraitMethod {
        private final String methodName;
        private final String[] methodParamTypes;

        public TraitMethod(String methodName, String... methodParamTypes) {
            this.methodName = methodName;
            this.methodParamTypes = methodParamTypes;
        }
    }
}
