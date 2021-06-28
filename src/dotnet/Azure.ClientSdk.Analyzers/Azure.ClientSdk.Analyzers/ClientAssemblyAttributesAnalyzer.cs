using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Azure.ClientSdk.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class ClientAssemblyAttributesAnalyzer : DiagnosticAnalyzer
    {
        private static readonly string[] AllowedSuffixes = new[]
        {
            "Test",
            "Tests",
            "DynamicProxyGenAssembly2",
            "Benchmarks",
            "Performance",
            "Perf"
        };

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
            context.EnableConcurrentExecution();
            context.RegisterCompilationAction(Analyze);
        }

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
            ImmutableArray.Create(Descriptors.AZC0011);

        private void Analyze(CompilationAnalysisContext context)
        {
            foreach (var attribute in context.Compilation.Assembly.GetAttributes())
            {
                if (attribute.AttributeClass.Name != "InternalsVisibleToAttribute" ||
                    attribute.ConstructorArguments.Length != 1)
                {
                    continue;
                }

                var parameter = Convert.ToString(attribute.ConstructorArguments[0].Value);
                var allowed = false;

                foreach (var suffix in AllowedSuffixes)
                {
                    if (parameter.IndexOf(suffix, StringComparison.OrdinalIgnoreCase) != -1)
                    {
                        allowed = true;
                        break;
                    }
                }

                if (!allowed)
                {
                    context.ReportDiagnostic(
                        Diagnostic.Create(Descriptors.AZC0011,
                            attribute.ApplicationSyntaxReference.GetSyntax().GetLocation()));
                }
            }
        }
    }
}