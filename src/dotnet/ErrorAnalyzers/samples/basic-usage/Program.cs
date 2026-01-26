using Azure.Tools.ErrorAnalyzers;

namespace BasicUsage;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Azure SDK Error Analyzers - Simple Demo");
        Console.WriteLine("========================================");
        Console.WriteLine();
        
        // Step 1: No registration needed! All rules are automatically available.
        Console.WriteLine("1. All rules are automatically available - no registration required!");
        Console.WriteLine("   ✓ Ready to analyze errors");
        
        // Show available rules (fallback is not exposed publicly)
        var availableRules = ErrorAnalyzerService.GetRegisteredRules();
        Console.WriteLine($"   ✓ {availableRules.Count()} specific rules available");
        Console.WriteLine();

        // Step 2: Test with sample errors
        Console.WriteLine("2. Testing error analysis:");
        Console.WriteLine();

        var testErrors = new[]
        {
            new RuleError("AZC0030", "Model name 'RequestData' ends with 'Data'. Consider using a more specific suffix like 'Info' or 'Details'."),
            new RuleError("UNKNOWN_ERROR", "This is an unknown error type"),
            new RuleError("AZC0034", "Type name 'Response' conflicts with System.Net.Response. Consider renaming to 'CreateBlobResponse' to avoid confusion.")
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
                    
                    if (!string.IsNullOrEmpty(promptFix.Context))
                    {
                        Console.WriteLine();
                        Console.WriteLine("Context:");
                        Console.WriteLine("--------");
                        Console.WriteLine(promptFix.Context);
                    }
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

