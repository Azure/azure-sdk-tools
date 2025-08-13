using Azure.Tools.ErrorAnalyzers;
using Azure.Tools.ErrorAnalyzers.ClientRuleAnalyzers;
using Azure.Tools.ErrorAnalyzers.GeneralRuleAnalyzers;
using Azure.Tools.ErrorAnalyzers.ManagementRuleAnalyzers;

namespace BasicUsage;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Azure SDK Error Analyzers - Simple Demo");
        Console.WriteLine("========================================");
        Console.WriteLine();
        
        // Step 1: Register analyzers
        Console.WriteLine("1. Registering analyzers...");
        ErrorAnalyzerService.RegisterProvider(new ClientAnalyzerProvider());
        ErrorAnalyzerService.RegisterProvider(new GeneralAnalyzerProvider());
        ErrorAnalyzerService.RegisterProvider(new ManagementAnalyzerProvider());
        Console.WriteLine("   ✓ Done");
        Console.WriteLine();

        // Step 2: Test with sample errors
        Console.WriteLine("2. Testing error analysis:");
        Console.WriteLine();

        var testErrors = new[]
        {
            new RuleError("AZC0012", "Type name 'Client' is too generic. Consider using a more descriptive multi-word name, such as 'ServiceClient'."),
            new RuleError("UNKNOWN_ERROR", "This is an unknown error type"),
            new RuleError("AZC0012", "Type name 'Manager' is too generic. Consider using a more descriptive multi-word name, such as 'ResourceManager'.")
        };

        foreach (var error in testErrors)
        {
            Console.WriteLine($"Error Type: {error.type}");
            Console.WriteLine($"Message: {error.message}");
            
            var fix = ErrorAnalyzerService.GetFix(error);
            if (fix != null)
            {
                Console.WriteLine($"Can Fix: True");
                
                if (fix is AgentPromptFix promptFix)
                {
                    Console.WriteLine();
                    Console.WriteLine("Generated Prompt:");
                    Console.WriteLine("----------------");
                    Console.WriteLine(promptFix.Prompt);
                    Console.WriteLine();
                    Console.WriteLine("Context:");
                    Console.WriteLine("--------");
                    Console.WriteLine(promptFix.Context);
                }
                else
                {
                    Console.WriteLine("No prompt generated");
                }
            }
            else
            {
                Console.WriteLine($"Can Fix: False");
            }
            
            Console.WriteLine();
            Console.WriteLine(new string('-', 80));
            Console.WriteLine();
        }

        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
    }
}

