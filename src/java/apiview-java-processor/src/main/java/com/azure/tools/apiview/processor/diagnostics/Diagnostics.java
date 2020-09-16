package com.azure.tools.apiview.processor.diagnostics;

import com.azure.tools.apiview.processor.diagnostics.rules.ConsiderFinalClassDiagnosticRule;
import com.azure.tools.apiview.processor.diagnostics.rules.FluentSetterReturnTypeDiagnosticRule;
import com.azure.tools.apiview.processor.diagnostics.rules.IllegalMethodNamesDiagnosticRule;
import com.azure.tools.apiview.processor.diagnostics.rules.IllegalPackageAPIExportsDiagnosticRule;
import com.azure.tools.apiview.processor.diagnostics.rules.ImportsDiagnosticRule;
import com.azure.tools.apiview.processor.diagnostics.rules.MissingAnnotationsDiagnosticRule;
import com.azure.tools.apiview.processor.diagnostics.rules.MissingJavaDocDiagnosticRule;
import com.azure.tools.apiview.processor.diagnostics.rules.NoPublicFieldsDiagnosticRule;
import com.azure.tools.apiview.processor.diagnostics.rules.PackageNameDiagnosticRule;
import com.azure.tools.apiview.processor.diagnostics.rules.RequiredBuilderMethodsDiagnosticRule;
import com.azure.tools.apiview.processor.diagnostics.rules.UpperCaseNamingDiagnosticRule;
import com.azure.tools.apiview.processor.model.APIListing;
import com.github.javaparser.ast.CompilationUnit;

import java.util.ArrayList;
import java.util.List;

import static com.azure.tools.apiview.processor.diagnostics.rules.RequiredBuilderMethodsDiagnosticRule.ExactTypeNameCheckFunction;
import static com.azure.tools.apiview.processor.diagnostics.rules.RequiredBuilderMethodsDiagnosticRule.DirectSubclassCheckFunction;

import static com.azure.tools.apiview.processor.diagnostics.rules.RequiredBuilderMethodsDiagnosticRule.ParameterAllowedTypes;

import static com.azure.tools.apiview.processor.diagnostics.rules.IllegalMethodNamesDiagnosticRule.Rule;

public class Diagnostics {
    private static final List<DiagnosticRule> diagnostics = new ArrayList<>();
    static {
        diagnostics.add(new PackageNameDiagnosticRule());
        diagnostics.add(new ImportsDiagnosticRule("com.sun"));
        diagnostics.add(new IllegalPackageAPIExportsDiagnosticRule("implementation", "netty"));
        diagnostics.add(new NoPublicFieldsDiagnosticRule());
        diagnostics.add(new UpperCaseNamingDiagnosticRule("URL", "HTTP", "XML", "JSON", "SAS", "CPK", "API"));
        diagnostics.add(new MissingAnnotationsDiagnosticRule());
        diagnostics.add(new FluentSetterReturnTypeDiagnosticRule());
        diagnostics.add(new ConsiderFinalClassDiagnosticRule());
        diagnostics.add(new IllegalMethodNamesDiagnosticRule(
            new Rule("Builder$", "tokenCredential"), // it should just be 'credential'
            new Rule("Builder$", "^set"),            // we shouldn't have setters in the builder
            new Rule("^isHas"),
            new Rule("^setHas")
        ));
        diagnostics.add(new MissingJavaDocDiagnosticRule());

        // common APIs for all builders (below we will do rules for http or amqp builders)
        diagnostics.add(new RequiredBuilderMethodsDiagnosticRule(null)
            .add("configuration", new ExactTypeNameCheckFunction("Configuration"))
            .add("clientOptions", new ExactTypeNameCheckFunction("ClientOptions"))
            .add("connectionString", new ExactTypeNameCheckFunction("String"))
            .add("credential", new ExactTypeNameCheckFunction(new ParameterAllowedTypes("TokenCredential", "AzureKeyCredential")))
            .add("endpoint", new ExactTypeNameCheckFunction("String"))
            .add("serviceVersion", new DirectSubclassCheckFunction("ServiceVersion")));
        diagnostics.add(new RequiredBuilderMethodsDiagnosticRule("amqp")
            .add("proxyOptions", new ExactTypeNameCheckFunction("ProxyOptions"))
            .add("retry", new ExactTypeNameCheckFunction("AmqpRetryOptions"))
            .add("transportType", new DirectSubclassCheckFunction("AmqpTransportType")));
        diagnostics.add(new RequiredBuilderMethodsDiagnosticRule("http")
            .add("addPolicy", new ExactTypeNameCheckFunction("HttpPipelinePolicy"))
            .add("httpClient", new ExactTypeNameCheckFunction("HttpClient"))
            .add("httpLogOptions", new ExactTypeNameCheckFunction("HttpLogOptions"))
            .add("pipeline", new ExactTypeNameCheckFunction("HttpPipeline"))
            .add("retryPolicy", new ExactTypeNameCheckFunction("RetryPolicy")));
    }

    public static void scan(CompilationUnit cu, APIListing listing) {
        // We do not scan compilation units that are missing any primary type (i.e. they are completely commented out).
        if (! cu.getPrimaryType().isPresent()) {
            return;
        }
        for (DiagnosticRule rule : diagnostics) {
            rule.scan(cu, listing);
        }
    }
}
