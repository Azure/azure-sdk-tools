package com.azure.tools.apiview.processor.diagnostics;

import com.azure.tools.apiview.processor.diagnostics.rules.*;
import com.azure.tools.apiview.processor.diagnostics.rules.azure.*;
import com.azure.tools.apiview.processor.diagnostics.rules.clientcore.ClientCoreBuilderTraitsDiagnosticRule;
import com.azure.tools.apiview.processor.diagnostics.rules.clientcore.ClientCoreFluentSetterReturnTypeDiagnosticRule;
import com.azure.tools.apiview.processor.diagnostics.rules.clientcore.ExpandableEnumDiagnosticRule;
import com.azure.tools.apiview.processor.diagnostics.rules.general.*;
import com.azure.tools.apiview.processor.diagnostics.rules.utils.MiscUtils;
import com.azure.tools.apiview.processor.model.APIListing;
import com.azure.tools.apiview.processor.model.Flavor;
import com.github.javaparser.ast.CompilationUnit;

import java.util.ArrayList;
import java.util.List;
import java.util.regex.Pattern;

import static com.azure.tools.apiview.processor.model.Flavor.*;

import static com.azure.tools.apiview.processor.diagnostics.rules.general.IllegalMethodNamesDiagnosticRule.Rule;
import static com.azure.tools.apiview.processor.diagnostics.rules.RequiredBuilderMethodsDiagnosticRule.DirectSubclassCheckFunction;
import static com.azure.tools.apiview.processor.diagnostics.rules.RequiredBuilderMethodsDiagnosticRule.ExactTypeNameCheckFunction;
import static com.azure.tools.apiview.processor.diagnostics.rules.RequiredBuilderMethodsDiagnosticRule.ParameterAllowedTypes;
import static com.azure.tools.apiview.processor.diagnostics.rules.general.BadAnnotationDiagnosticRule.BadAnnotation;

public class Diagnostics {
    final List<DiagnosticRule> rules;

    public Diagnostics(APIListing apiListing) {
        rules = new ArrayList<>();

        System.out.println("  Setting up diagnostics...");

        Flavor flavor = Flavor.getFlavor(apiListing);

        // Special rules for com.azure or io.clientcore libraries only
        switch (flavor) {
            case AZURE: {
                System.out.println("    Applying com.azure specific diagnostics...");
                addAzureCoreDiagnostics();
                break;
            }
            case GENERIC: {
                System.out.println("    Applying io.clientcore specific diagnostics...");
                addClientCoreDiagnostics();
                break;
            }
            case UNKNOWN:
            default: {
                System.out.println("    Unknown library flavor...");
                break;
            }
        }

        System.out.println("    Applying general-purpose diagnostics...");

        // general rules applicable in all cases
        rules.add(new UpperCaseEnumValuesDiagnosticRule());
        rules.add(new ImportsDiagnosticRule("com.sun"));
        rules.add(new IllegalPackageAPIExportsDiagnosticRule("implementation", "netty"));
        rules.add(new NoPublicFieldsDiagnosticRule());
        rules.add(new UpperCaseNamingDiagnosticRule("URL", "HTTP", "XML", "JSON", "SAS", "CPK", "API"));
        rules.add(new ConsiderFinalClassDiagnosticRule());
        rules.add(new IllegalMethodNamesDiagnosticRule(
                new Rule("Builder$", "tokenCredential"), // it should just be 'credential'
                new Rule("Builder$", "^set"),            // we shouldn't have setters in the builder
                new Rule("^isHas"),
                new Rule("^setHas")
        ));
        rules.add(new MissingJavaDocDiagnosticRule());
        rules.add(new MissingJavadocCodeSnippetsRule());
        rules.add(new NoLocalesInJavadocUrlDiagnosticRule());
        rules.add(new ModuleInfoDiagnosticRule());
        rules.add(new BadAnnotationDiagnosticRule(
                new BadAnnotation("JacksonXmlRootElement",
                        "From the Jackson JavaDoc: \"NOTE! Since 2.4 this annotation is usually not necessary and " +
                                "you should use JsonRootName instead. About the only expected usage may be to have different " +
                                "root name for XML content than other formats.\"")
        ));
    }

    private void addAzureCoreDiagnostics() {
        rules.add(new PackageNameDiagnosticRule(Pattern.compile("^" + AZURE.getPackagePrefix() + "(\\.[a-z0-9]+)+$")));
        rules.add(new AzureCoreBuilderTraitsDiagnosticRule());
        rules.add(new MissingAnnotationsDiagnosticRule(AZURE.getPackagePrefix()));
        rules.add(new AzureCoreFluentSetterReturnTypeDiagnosticRule());
        rules.add(new ServiceVersionDiagnosticRule());
        rules.add(new ExpandableEnumDiagnosticRule("ExpandableStringEnum"));
        rules.add(new MavenPackageAndDescriptionDiagnosticRule());

        // common APIs for all builders (below we will do rules for http or amqp builders)
        rules.add(new RequiredBuilderMethodsDiagnosticRule(null)
            .add("configuration", new ExactTypeNameCheckFunction("Configuration"))
            .add("clientOptions", new ExactTypeNameCheckFunction("ClientOptions"))
            .add("connectionString", new ExactTypeNameCheckFunction("String"))
            .add("credential", new ExactTypeNameCheckFunction(new ParameterAllowedTypes("TokenCredential",
                    "AzureKeyCredential", "AzureSasCredential", "AzureNamedKeyCredential", "KeyCredential")))
            .add("endpoint", new ExactTypeNameCheckFunction("String"))
            .add("serviceVersion", m -> MiscUtils.checkMethodParameterTypeSuffix(m, "ServiceVersion")));
        rules.add(new RequiredBuilderMethodsDiagnosticRule("amqp")
            .add("proxyOptions", new ExactTypeNameCheckFunction("ProxyOptions"))
            .add("retry", new ExactTypeNameCheckFunction("AmqpRetryOptions"))
            .add("transportType", new DirectSubclassCheckFunction("AmqpTransportType")));
        rules.add(new RequiredBuilderMethodsDiagnosticRule("http")
            .add("addPolicy", new ExactTypeNameCheckFunction("HttpPipelinePolicy"))
            .add("httpClient", new ExactTypeNameCheckFunction("HttpClient"))
            .add("httpLogOptions", new ExactTypeNameCheckFunction("HttpLogOptions"))
            .add("pipeline", new ExactTypeNameCheckFunction("HttpPipeline"))
            .add("retryPolicy", new ExactTypeNameCheckFunction("RetryPolicy")));
    }

    private void addClientCoreDiagnostics() {
        rules.add(new ClientCoreBuilderTraitsDiagnosticRule());
        rules.add(new MissingAnnotationsDiagnosticRule(GENERIC.getPackagePrefix()));
        rules.add(new ClientCoreFluentSetterReturnTypeDiagnosticRule());
        rules.add(new ExpandableEnumDiagnosticRule("ExpandableEnum"));

        // common APIs for all builders (below we will do rules for http or amqp builders)
        rules.add(new RequiredBuilderMethodsDiagnosticRule(null)
            .add("endpoint", new ExactTypeNameCheckFunction("String"))
            .add("configuration", new ExactTypeNameCheckFunction("Configuration"))
            .add("credential", new ExactTypeNameCheckFunction(new ParameterAllowedTypes("KeyCredential")))
            .add("addHttpPipelinePolicy", new ExactTypeNameCheckFunction("HttpPipelinePolicy"))
            .add("httpClient", new ExactTypeNameCheckFunction("HttpClient"))
            .add("httpLogOptions", new ExactTypeNameCheckFunction("HttpLogOptions"))
            .add("httpPipeline", new ExactTypeNameCheckFunction("HttpPipeline"))
            .add("httpRetryOptions", new ExactTypeNameCheckFunction("HttpRetryOptions"))
            .add("httpRedirectOptions", new ExactTypeNameCheckFunction("HttpRedirectOptions"))
            .add("proxyOptions", new ExactTypeNameCheckFunction("ProxyOptions")));
    }
    
    private void add(DiagnosticRule rule) {
        rules.add(rule);
    }

    /**
     * Runs any diagnostics that can be run against the single compilation unit that has been provided.
     */
    public void scanIndividual(CompilationUnit cu, APIListing listing) {
        // We do not scan compilation units that are missing any primary type (i.e. they are completely commented out).
        if (!cu.getPrimaryType().isPresent()) {
            return;
        }
        rules.forEach(rule -> rule.scanIndividual(cu, listing));
    }

    /**
     * Called once to allow for any full analysis to be performed after all individual scans have been completed.
     */
    public void scanFinal(APIListing listing) {
        rules.forEach(rule -> rule.scanFinal(listing));
    }
}
