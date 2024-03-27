package com.azure.tools.apiview.processor.diagnostics;

import com.azure.tools.apiview.processor.diagnostics.rules.*;
import com.azure.tools.apiview.processor.diagnostics.rules.azure.*;
import com.azure.tools.apiview.processor.diagnostics.rules.clientcore.ClientCoreBuilderTraitsDiagnosticRule;
import com.azure.tools.apiview.processor.diagnostics.rules.clientcore.ClientCoreFluentSetterReturnTypeDiagnosticRule;
import com.azure.tools.apiview.processor.diagnostics.rules.clientcore.ExpandableEnumDiagnosticRule;
import com.azure.tools.apiview.processor.diagnostics.rules.general.*;
import com.azure.tools.apiview.processor.diagnostics.rules.utils.MiscUtils;
import com.azure.tools.apiview.processor.model.APIListing;
import com.github.javaparser.ast.CompilationUnit;

import java.util.ArrayList;
import java.util.List;
import java.util.regex.Pattern;

import static com.azure.tools.apiview.processor.diagnostics.rules.general.IllegalMethodNamesDiagnosticRule.Rule;
import static com.azure.tools.apiview.processor.diagnostics.rules.RequiredBuilderMethodsDiagnosticRule.DirectSubclassCheckFunction;
import static com.azure.tools.apiview.processor.diagnostics.rules.RequiredBuilderMethodsDiagnosticRule.ExactTypeNameCheckFunction;
import static com.azure.tools.apiview.processor.diagnostics.rules.RequiredBuilderMethodsDiagnosticRule.ParameterAllowedTypes;
import static com.azure.tools.apiview.processor.diagnostics.rules.general.BadAnnotationDiagnosticRule.BadAnnotation;

public class Diagnostics {
    private static final String AZURE_PACKAGE_PREFIX = "com.azure";
    private static final String CLIENT_CORE_PACKAGE_PREFIX = "io.clientcore";

    final List<DiagnosticRule> rules;

    private Diagnostics() {
        rules = new ArrayList<>();
    }

    /**
     * Returns the diagnostics that are relevant to the given API listing.
     */
    public static Diagnostics getDiagnostics(APIListing apiListing) {
        Diagnostics d = new Diagnostics();

        // Special rules for com.azure or io.clientcore libraries only
        if (apiListing.getPackageName().startsWith(AZURE_PACKAGE_PREFIX)) {
            d.add(new PackageNameDiagnosticRule(Pattern.compile("^" + AZURE_PACKAGE_PREFIX + "(\\.[a-z0-9]+)+$")));
            d.add(new AzureCoreBuilderTraitsDiagnosticRule());
            d.add(new MissingAnnotationsDiagnosticRule(AZURE_PACKAGE_PREFIX));
            d.add(new AzureCoreFluentSetterReturnTypeDiagnosticRule());
            d.add(new ServiceVersionDiagnosticRule());
            d.add(new ExpandableEnumDiagnosticRule("ExpandableStringEnum"));
            d.add(new MavenPackageAndDescriptionDiagnosticRule());

            // common APIs for all builders (below we will do rules for http or amqp builders)
            d.add(new RequiredBuilderMethodsDiagnosticRule(null)
                    .add("configuration", new ExactTypeNameCheckFunction("Configuration"))
                    .add("clientOptions", new ExactTypeNameCheckFunction("ClientOptions"))
                    .add("connectionString", new ExactTypeNameCheckFunction("String"))
                    .add("credential", new ExactTypeNameCheckFunction(new ParameterAllowedTypes("TokenCredential",
                            "AzureKeyCredential", "AzureSasCredential", "AzureNamedKeyCredential", "KeyCredential")))
                    .add("endpoint", new ExactTypeNameCheckFunction("String"))
                    .add("serviceVersion", m -> MiscUtils.checkMethodParameterTypeSuffix(m, "ServiceVersion")));
            d.add(new RequiredBuilderMethodsDiagnosticRule("amqp")
                    .add("proxyOptions", new ExactTypeNameCheckFunction("ProxyOptions"))
                    .add("retry", new ExactTypeNameCheckFunction("AmqpRetryOptions"))
                    .add("transportType", new DirectSubclassCheckFunction("AmqpTransportType")));
            d.add(new RequiredBuilderMethodsDiagnosticRule("http")
                    .add("addPolicy", new ExactTypeNameCheckFunction("HttpPipelinePolicy"))
                    .add("httpClient", new ExactTypeNameCheckFunction("HttpClient"))
                    .add("httpLogOptions", new ExactTypeNameCheckFunction("HttpLogOptions"))
                    .add("pipeline", new ExactTypeNameCheckFunction("HttpPipeline"))
                    .add("retryPolicy", new ExactTypeNameCheckFunction("RetryPolicy")));
        } else if (apiListing.getPackageName().startsWith(CLIENT_CORE_PACKAGE_PREFIX)) {
            d.add(new PackageNameDiagnosticRule(Pattern.compile("^" + CLIENT_CORE_PACKAGE_PREFIX + "(\\.[a-z0-9]+)+$")));
            d.add(new ClientCoreBuilderTraitsDiagnosticRule());
            d.add(new MissingAnnotationsDiagnosticRule(CLIENT_CORE_PACKAGE_PREFIX));
            d.add(new ClientCoreFluentSetterReturnTypeDiagnosticRule());
            d.add(new ExpandableEnumDiagnosticRule("ExpandableEnum"));

            // common APIs for all builders (below we will do rules for http or amqp builders)
            d.add(new RequiredBuilderMethodsDiagnosticRule(null)
                    .add("configuration", new ExactTypeNameCheckFunction("Configuration"))
                    .add("clientOptions", new ExactTypeNameCheckFunction("ClientOptions"))
                    .add("credential", new ExactTypeNameCheckFunction(new ParameterAllowedTypes("KeyCredential")))
                    .add("endpoint", new ExactTypeNameCheckFunction("String"))
                    .add("serviceVersion", m -> MiscUtils.checkMethodParameterTypeSuffix(m, "ServiceVersion"))
                    .add("addHttpPipelinePolicy", new ExactTypeNameCheckFunction("HttpPipelinePolicy"))
                    .add("httpClient", new ExactTypeNameCheckFunction("HttpClient"))
                    .add("httpLogOptions", new ExactTypeNameCheckFunction("HttpLogOptions"))
                    .add("httpPipeline", new ExactTypeNameCheckFunction("HttpPipeline"))
                    .add("httpRetryOptions", new ExactTypeNameCheckFunction("RetryPolicy")));
        }

        // general rules applicable in all cases
        d.add(new UpperCaseEnumValuesDiagnosticRule());
        d.add(new ImportsDiagnosticRule("com.sun"));
        d.add(new IllegalPackageAPIExportsDiagnosticRule("implementation", "netty"));
        d.add(new NoPublicFieldsDiagnosticRule());
        d.add(new UpperCaseNamingDiagnosticRule("URL", "HTTP", "XML", "JSON", "SAS", "CPK", "API"));
        d.add(new ConsiderFinalClassDiagnosticRule());
        d.add(new IllegalMethodNamesDiagnosticRule(
                new Rule("Builder$", "tokenCredential"), // it should just be 'credential'
                new Rule("Builder$", "^set"),            // we shouldn't have setters in the builder
                new Rule("^isHas"),
                new Rule("^setHas")
        ));
        d.add(new MissingJavaDocDiagnosticRule());
        d.add(new MissingJavadocCodeSnippetsRule());
        d.add(new NoLocalesInJavadocUrlDiagnosticRule());
        d.add(new ModuleInfoDiagnosticRule());
        d.add(new BadAnnotationDiagnosticRule(
                new BadAnnotation("JacksonXmlRootElement",
                        "From the Jackson JavaDoc: \"NOTE! Since 2.4 this annotation is usually not necessary and " +
                                "you should use JsonRootName instead. About the only expected usage may be to have different " +
                                "root name for XML content than other formats.\"")
        ));

        return d;
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
