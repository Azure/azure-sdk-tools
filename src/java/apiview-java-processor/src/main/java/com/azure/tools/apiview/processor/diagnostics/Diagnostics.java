package com.azure.tools.apiview.processor.diagnostics;

import com.azure.tools.apiview.processor.diagnostics.rules.BadPrefixesDiagnosticRule;
import com.azure.tools.apiview.processor.diagnostics.rules.IllegalPackageAPIExportsDiagnosticRule;
import com.azure.tools.apiview.processor.diagnostics.rules.ImportsDiagnosticRule;
import com.azure.tools.apiview.processor.diagnostics.rules.MissingAnnotationsDiagnosticRule;
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

public class Diagnostics {
    private static final List<DiagnosticRule> diagnostics = new ArrayList<>();
    static {
        diagnostics.add(new PackageNameDiagnosticRule());
        diagnostics.add(new ImportsDiagnosticRule("com.sun"));
        diagnostics.add(new IllegalPackageAPIExportsDiagnosticRule("implementation", "netty"));
        diagnostics.add(new NoPublicFieldsDiagnosticRule());
        diagnostics.add(new UpperCaseNamingDiagnosticRule("URL", "HTTP", "XML", "JSON", "SAS", "CPK", "API"));
        diagnostics.add(new BadPrefixesDiagnosticRule("isHas", "setHas"));
        diagnostics.add(new RequiredBuilderMethodsDiagnosticRule()
            .add("addPolicy", new ExactTypeNameCheckFunction("HttpPipelinePolicy"))
            .add("configuration", new ExactTypeNameCheckFunction("Configuration"))
            .add("credential", new ExactTypeNameCheckFunction("TokenCredential"))
            .add("connectionString", new ExactTypeNameCheckFunction("String"))
            .add("endpoint", new ExactTypeNameCheckFunction("String"))
            .add("httpClient", new ExactTypeNameCheckFunction("HttpClient"))
            .add("httpLogOptions", new ExactTypeNameCheckFunction("HttpLogOptions"))
            .add("pipeline", new ExactTypeNameCheckFunction("HttpPipeline"))
            .add("retryPolicy", new ExactTypeNameCheckFunction("HttpPipelinePolicy"))
            .add("serviceVersion", new DirectSubclassCheckFunction("ServiceVersion")));
        diagnostics.add(new MissingAnnotationsDiagnosticRule());
    }

    public static void scan(CompilationUnit cu, APIListing listing) {
        for (DiagnosticRule rule : diagnostics) {
            rule.scan(cu, listing);
        }
    }
}
