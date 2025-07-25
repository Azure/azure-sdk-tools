using Azure.Tools.ErrorAnalyzers;
using Azure.Tools.ErrorAnalyzers.ClientRuleAnalyzers;
using Azure.Tools.ErrorAnalyzers.GeneralRuleAnalyzers;
using Azure.Tools.ErrorAnalyzers.ManagementRuleAnalyzers;

namespace BasicUsage;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Azure SDK Error Analyzers - Test Console");
        Console.WriteLine("========================================");
        Console.WriteLine();

        RegisterAnalyzerProviders();
        
        TestErrorAnalysis();
        
        AdvancedExample.RunAdvancedExample();
        
        Console.WriteLine();
        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
    }

    static void RegisterAnalyzerProviders()
    {
        Console.WriteLine("Registering analyzer providers...");
        
        ErrorAnalyzerService.RegisterProvider(new ClientAnalyzerProvider());
        ErrorAnalyzerService.RegisterProvider(new GeneralAnalyzerProvider());
        ErrorAnalyzerService.RegisterProvider(new ManagementAnalyzerProvider());
        
        Console.WriteLine("✓ All analyzer providers registered");
        Console.WriteLine();
    }

    static void TestErrorAnalysis()
    {
        Console.WriteLine("Testing error analysis capabilities:");
        Console.WriteLine();

        var testErrors = new[]
        {
            new RuleError("AZC0012", "Type name 'Client' is too generic. Consider using a more descriptive multi-word name, such as 'ServiceClient'."),
            new RuleError("UNKNOWN_ERROR", "This is an unknown error type that won't have a fix"),
            new RuleError("AZC0012", "Type name 'Manager' is too generic. Consider using a more descriptive multi-word name, such as 'ResourceManager'.")
        };

        foreach (var error in testErrors)
        {
            Console.WriteLine($"Processing error: {error.type}");
            Console.WriteLine($"Message: {error.message}");
            
            bool canHandle = ErrorAnalyzerService.CanHandle(error.type);
            Console.WriteLine($"Can handle: {canHandle}");
            
            if (canHandle)
            {
                var fix = ErrorAnalyzerService.GetFix(error);
                if (fix != null)
                {
                    Console.WriteLine($"Fix available: {fix.Action}");
                    if (fix is RenameFix renameFix)
                    {
                        Console.WriteLine($"  Rename from '{renameFix.OriginalName}' to '{renameFix.NewName}'");
                    }
                    else if (fix is AgentPromptFix promptFix)
                    {
                        Console.WriteLine($"  Suggested fix: {promptFix.Prompt}");
                    }
                }
                else
                {
                    Console.WriteLine("No fix available");
                }
            }
            
            Console.WriteLine();
        }

        TestBatchProcessing(testErrors);
    }

    static void TestBatchProcessing(RuleError[] errors)
    {
        Console.WriteLine("Testing batch processing:");
        Console.WriteLine();

        var fixes = ErrorAnalyzerService.GetFixes(errors).ToList();
        Console.WriteLine($"Total fixes generated: {fixes.Count}");
        
        foreach (var fix in fixes)
        {
            Console.WriteLine($"- {fix.Action} fix available");
        }
        
        Console.WriteLine();
        
        var availableAnalyzers = ErrorAnalyzerService.GetAllAnalyzers().ToList();
        Console.WriteLine($"Total registered analyzers: {availableAnalyzers.Count}");
    }
}
