using Microsoft.Playwright;
using UtilityLibraries;

namespace ValidationRule.Test;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public class TestValidations
{
    public static List<HTMLRule> HTMLRulesForExtraLabelValidation = new List<HTMLRule>();
    public static List<HTMLRule> HTMLRulesForGarbledTextValidation = new List<HTMLRule>();
    public static List<HTMLRule> HTMLRulesForUnnecessarySymbolsValidation = new List<HTMLRule>();
    public static List<HTMLRule> HTMLRulesForTypeAnnotationValidation = new List<HTMLRule>();
    public static List<HTMLRule> HTMLRulesForMissingContentValidation = new List<HTMLRule>();
    public static List<HTMLRule> HTMLRulesForDuplicateServiceValidation = new List<HTMLRule>();

    public static IPlaywright playwright;
    public static IBrowser browser;

    static TestValidations()
    {
        playwright = Playwright.CreateAsync().GetAwaiter().GetResult();
        // Create a shared browser instance for all tests
        browser = playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true }).GetAwaiter().GetResult();
        foreach (var item in LocalData.Items)
        {
            switch (item.Type)
            {
                case "ExtraLabelValidation":
                    HTMLRulesForExtraLabelValidation.AddRange(item.Rules);
                    break;
                case "GarbledTextValidation":
                    HTMLRulesForGarbledTextValidation.AddRange(item.Rules);
                    break;
                case "UnnecessarySymbolsValidation":
                    HTMLRulesForUnnecessarySymbolsValidation.AddRange(item.Rules);
                    break;
                case "TypeAnnotationValidation":
                    HTMLRulesForTypeAnnotationValidation.AddRange(item.Rules);
                    break;
                case "MissingContentValidation":
                    HTMLRulesForMissingContentValidation.AddRange(item.Rules);
                    break;
                case "DuplicateServiceValidation":
                    HTMLRulesForDuplicateServiceValidation.AddRange(item.Rules);
                    break;
            }
        }
    }

    [OneTimeTearDown]
    public void Cleanup()
    {
        browser?.CloseAsync().GetAwaiter().GetResult();
        playwright?.Dispose();
    }


    [Test]
    [TestCaseSource(nameof(HTMLRulesForExtraLabelValidation))]   
    public async Task TestExtraLabelValidationRules(HTMLRule rule)
    {
        string Type = "ExtraLabelValidation";

        IValidation validation = ValidationFactory.CreateValidation(Type, browser);

        var res = await validation.Validate(rule.FileUri!);

        string logMessage = @$"{Type} - {rule.RuleName} :  {(res.Result == rule.Expected ? "Passed" : "Failed")}";
        Console.WriteLine(logMessage);

        string errorMessage = @$"
            =====================================
                Validation-Type : {Type} 
                    Validation-Rule : {rule.RuleName}
                    failed for the file : {(rule.FileUri?.LastIndexOf("HTML") >= 0 ? rule.FileUri.Substring(rule.FileUri.LastIndexOf("HTML")) : rule.FileUri)}
            =====================================
                ";


        Assert.That(res.Result, Is.EqualTo(rule.Expected), errorMessage);
    }

    [Test]
    [TestCaseSource(nameof(HTMLRulesForGarbledTextValidation))]
    public async Task TestGarbledTextValidationRules(HTMLRule rule)
    {
        string Type = "GarbledTextValidation";

        IValidation validation = ValidationFactory.CreateValidation(Type, browser);

        var res = await validation.Validate(rule.FileUri!);

        string logMessage = @$"{Type} - {rule.RuleName} :  {(res.Result == rule.Expected ? "Passed" : "Failed")}";
        Console.WriteLine(logMessage);

        string errorMessage = @$"
            =====================================
                Validation-Type : {Type} 
                    Validation-Rule : {rule.RuleName}
                    failed for the file : {(rule.FileUri?.LastIndexOf("HTML") >= 0 ? rule.FileUri.Substring(rule.FileUri.LastIndexOf("HTML")) : rule.FileUri)}
            =====================================
                ";

        Assert.That(res.Result, Is.EqualTo(rule.Expected), errorMessage);
    }

    [Test]
    [TestCaseSource(nameof(HTMLRulesForUnnecessarySymbolsValidation))]
    public async Task TestUnnecessarySymbolsValidationRules(HTMLRule rule)
    {
        string Type = "UnnecessarySymbolsValidation";

        IValidation validation = ValidationFactory.CreateValidation(Type, browser);

        var res = await validation.Validate(rule.FileUri!);

        string logMessage = @$"{Type} - {rule.RuleName} :  {(res.Result == rule.Expected ? "Passed" : "Failed")}";
        Console.WriteLine(logMessage);

        string errorMessage = @$"
            =====================================
                Validation-Type : {Type} 
                    Validation-Rule : {rule.RuleName}
                    failed for the file : {(rule.FileUri?.LastIndexOf("HTML") >= 0 ? rule.FileUri.Substring(rule.FileUri.LastIndexOf("HTML")) : rule.FileUri)}
            =====================================
                ";

        Assert.That(res.Result, Is.EqualTo(rule.Expected), errorMessage);
    }

    [Test]
    [TestCaseSource(nameof(HTMLRulesForTypeAnnotationValidation))]
    public async Task TestTypeAnnotationValidationRules(HTMLRule rule)
    {
        string Type = "TypeAnnotationValidation";

        IValidation validation = ValidationFactory.CreateValidation(Type, browser);

        var res = await validation.Validate(rule.FileUri!);

        string logMessage = @$"{Type} - {rule.RuleName} :  {(res.Result == rule.Expected ? "Passed" : "Failed")}";
        Console.WriteLine(logMessage);

        string errorMessage = @$"
            =====================================
                Validation-Type : {Type} 
                    Validation-Rule : {rule.RuleName}
                    failed for the file : {(rule.FileUri?.LastIndexOf("HTML") >= 0 ? rule.FileUri.Substring(rule.FileUri.LastIndexOf("HTML")) : rule.FileUri)}
            =====================================
                ";

        Assert.That(res.Result, Is.EqualTo(rule.Expected), errorMessage);
    }


    [Test]
    [TestCaseSource(nameof(HTMLRulesForMissingContentValidation))]
    public async Task TestMissingContentValidationRules(HTMLRule rule)
    {
        string Type = "MissingContentValidation";

        IValidation validation = ValidationFactory.CreateValidation(Type, browser);

        var res = await validation.Validate(rule.FileUri!);

        string logMessage = @$"{Type} - {rule.RuleName} :  {(res.Result == rule.Expected ? "Passed" : "Failed")}";
        Console.WriteLine(logMessage);

        string errorMessage = @$"
            =====================================
                Validation-Type : {Type} 
                    Validation-Rule : {rule.RuleName}
                    failed for the file : {(rule.FileUri?.LastIndexOf("HTML") >= 0 ? rule.FileUri.Substring(rule.FileUri.LastIndexOf("HTML")) : rule.FileUri)}
            =====================================
                ";

        Assert.That(res.Result, Is.EqualTo(rule.Expected), errorMessage);
    }

    [Test]
    [TestCaseSource(nameof(HTMLRulesForDuplicateServiceValidation))]
    public async Task TestDuplicateServiceValidationRules(HTMLRule rule)
    {
        string Type = "DuplicateServiceValidation";

        IValidation validation = ValidationFactory.CreateValidation(Type, browser);

        var res = await validation.Validate(rule.FileUri!);

        string logMessage = @$"{Type} - {rule.RuleName} :  {(res.Result == rule.Expected ? "Passed" : "Failed")}";
        Console.WriteLine(logMessage);

        string errorMessage = @$"
            =====================================
                Validation-Type : {Type} 
                    Validation-Rule : {rule.RuleName}
                    failed for the file : {(rule.FileUri?.LastIndexOf("HTML") >= 0 ? rule.FileUri.Substring(rule.FileUri.LastIndexOf("HTML")) : rule.FileUri)}
            =====================================
                ";

        Assert.That(res.Result, Is.EqualTo(rule.Expected), errorMessage);
    }

}


public static class ValidationFactory
{
    public static IValidation CreateValidation(string validationType, IBrowser browser)
    {
        return validationType switch
        {
            "UnnecessarySymbolsValidation" => new UnnecessarySymbolsValidation(browser),
            "ExtraLabelValidation" => new ExtraLabelValidation(browser),
            "TypeAnnotationValidation" => new TypeAnnotationValidation(browser),
            "GarbledTextValidation" => new GarbledTextValidation(browser),
            "MissingContentValidation" => new MissingContentValidation(browser),
            "DuplicateServiceValidation" => new DuplicateServiceValidation(browser),
            _ => throw new ArgumentException($"Unknown validation type: {validationType}")
        };
    }
}
