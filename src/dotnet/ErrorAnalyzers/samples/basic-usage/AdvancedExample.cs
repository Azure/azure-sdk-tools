using Azure.Tools.ErrorAnalyzers;
using Azure.Tools.ErrorAnalyzers.ClientRuleAnalyzers;
using Azure.Tools.ErrorAnalyzers.GeneralRuleAnalyzers;
using Azure.Tools.ErrorAnalyzers.ManagementRuleAnalyzers;

namespace BasicUsage;

/// <summary>
/// Advanced example showing different usage patterns of the Azure SDK Error Analyzers library.
/// </summary>
class AdvancedExample
{
    public static void RunAdvancedExample()
    {
        Console.WriteLine("\n" + new string('=', 60));
        Console.WriteLine("ADVANCED USAGE EXAMPLES");
        Console.WriteLine(new string('=', 60));

        RegisterProviders();
        
        DemonstrateBasicUsage();
        DemonstrateBatchProcessing();
        DemonstrateErrorTypeChecking();
        DemonstrateProviderIntegration();
    }

    static void RegisterProviders()
    {
        Console.WriteLine("\n📦 PROVIDER REGISTRATION");
        Console.WriteLine("------------------------");
        
        ErrorAnalyzerService.RegisterProvider(new ClientAnalyzerProvider());
        ErrorAnalyzerService.RegisterProvider(new GeneralAnalyzerProvider());
        ErrorAnalyzerService.RegisterProvider(new ManagementAnalyzerProvider());
        
        var totalAnalyzers = ErrorAnalyzerService.GetAllAnalyzers().Count();
        Console.WriteLine($"✅ Registered {totalAnalyzers} analyzers across all providers");
    }

    static void DemonstrateBasicUsage()
    {
        Console.WriteLine("\n🔧 BASIC USAGE - Individual Error Processing");
        Console.WriteLine("---------------------------------------------");

        var errors = new[]
        {
            new RuleError("AZC0012", "Type name 'Client' is too generic. Consider using a more descriptive multi-word name, such as 'BlobServiceClient'."),
            new RuleError("AZC0012", "Type name 'Helper' is too generic. Consider using a more descriptive multi-word name, such as 'DataProcessingHelper'."),
            new RuleError("UNKNOWN001", "This error type is not supported by any analyzer"),
        };

        foreach (var error in errors)
        {
            Console.WriteLine($"\n🚨 Error: {error.type}");
            Console.WriteLine($"   Message: {error.message[..Math.Min(error.message.Length, 80)]}...");
            
            if (ErrorAnalyzerService.CanHandle(error.type))
            {
                var fix = ErrorAnalyzerService.GetFix(error);
                if (fix != null)
                {
                    Console.WriteLine($"   ✅ Fix: {fix.Action}");
                    if (fix is RenameFix rename)
                    {
                        Console.WriteLine($"      📝 '{rename.OriginalName}' → '{rename.NewName}'");
                    }
                }
                else
                {
                    Console.WriteLine($"   ⚠️  No fix strategy available");
                }
            }
            else
            {
                Console.WriteLine($"   ❌ Error type not supported");
            }
        }
    }

    static void DemonstrateBatchProcessing()
    {
        Console.WriteLine("\n⚡ BATCH PROCESSING - Multiple Errors at Once");
        Console.WriteLine("---------------------------------------------");

        var codebaseErrors = new[]
        {
            new RuleError("AZC0012", "Type name 'Service' is too generic. Consider using a more descriptive multi-word name, such as 'StorageService'."),
            new RuleError("AZC0012", "Type name 'Data' is too generic. Consider using a more descriptive multi-word name, such as 'UserData'."),
            new RuleError("AZC0012", "Type name 'Config' is too generic. Consider using a more descriptive multi-word name, such as 'DatabaseConfig'."),
            new RuleError("OTHER001", "Some other error type"),
            new RuleError("AZC0012", "Type name 'Utils' is too generic. Consider using a more descriptive multi-word name, such as 'StringUtils'."),
        };

        Console.WriteLine($"📊 Processing {codebaseErrors.Length} errors from codebase scan...");
        
        var fixes = ErrorAnalyzerService.GetFixes(codebaseErrors).ToList();
        
        Console.WriteLine($"🎯 Generated {fixes.Count} fixes:");
        foreach (var fix in fixes)
        {
            if (fix is RenameFix rename)
            {
                Console.WriteLine($"   • Rename: {rename.OriginalName} → {rename.NewName}");
            }
            else
            {
                Console.WriteLine($"   • {fix.Action} fix available");
            }
        }
        
        var unhandledCount = codebaseErrors.Length - fixes.Count;
        if (unhandledCount > 0)
        {
            Console.WriteLine($"⚠️  {unhandledCount} errors could not be automatically fixed");
        }
    }

    static void DemonstrateErrorTypeChecking()
    {
        Console.WriteLine("\n🔍 ERROR TYPE VALIDATION");
        Console.WriteLine("-------------------------");

        var errorTypesToCheck = new[] { "AZC0012", "AZC0001", "UNKNOWN", "GENERAL001", "" };
        
        foreach (var errorType in errorTypesToCheck)
        {
            if (string.IsNullOrEmpty(errorType))
            {
                Console.WriteLine("❌ Empty error type - invalid");
                continue;
            }
            
            bool canHandle = ErrorAnalyzerService.CanHandle(errorType);
            Console.WriteLine($"{(canHandle ? "✅" : "❌")} {errorType,-12} - {(canHandle ? "Supported" : "Not supported")}");
        }
    }

    static void DemonstrateProviderIntegration()
    {
        Console.WriteLine("\n🏗️  PROVIDER INTEGRATION PATTERNS");
        Console.WriteLine("----------------------------------");

        Console.WriteLine("📚 Available provider types:");
        Console.WriteLine("   • ClientAnalyzerProvider    - Azure SDK client library rules");
        Console.WriteLine("   • GeneralAnalyzerProvider   - General .NET coding rules");
        Console.WriteLine("   • ManagementAnalyzerProvider - Azure management library rules");
        
        Console.WriteLine("\n🔧 Integration scenarios:");
        Console.WriteLine("   • Build-time integration: Register providers in MSBuild targets");
        Console.WriteLine("   • IDE integration: Register providers in language service");
        Console.WriteLine("   • CI/CD integration: Batch process errors from static analysis");
        Console.WriteLine("   • Unit testing: Mock providers for testing custom rules");
        
        Console.WriteLine("\n📈 Performance characteristics:");
        Console.WriteLine("   • Thread-safe provider registration");
        Console.WriteLine("   • Lazy analyzer initialization");
        Console.WriteLine("   • O(n) linear search through analyzers");
        Console.WriteLine("   • Zero reflection overhead");
    }
}
