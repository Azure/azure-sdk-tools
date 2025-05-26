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

    static TestValidations()
    {
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


    [Test]
    [TestCaseSource(nameof(HTMLRulesForExtraLabelValidation))]   
    public async Task TestExtraLabelValidationRules(HTMLRule rule)
    {
        var playwright = await Playwright.CreateAsync();

        string Type = "ExtraLabelValidation";

        IValidation validation = ValidationFactory.CreateValidation(Type, playwright);

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

        playwright.Dispose();
    }

    [Test]
    [TestCaseSource(nameof(HTMLRulesForGarbledTextValidation))]
    public async Task TestGarbledTextValidationRules(HTMLRule rule)
    {
        var playwright = await Playwright.CreateAsync();

        string Type = "GarbledTextValidation";

        IValidation validation = ValidationFactory.CreateValidation(Type, playwright);

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

        playwright.Dispose();
    }

    [Test]
    [TestCaseSource(nameof(HTMLRulesForUnnecessarySymbolsValidation))]
    public async Task TestUnnecessarySymbolsValidationRules(HTMLRule rule)
    {
        var playwright = await Playwright.CreateAsync();

        string Type = "UnnecessarySymbolsValidation";

        IValidation validation = ValidationFactory.CreateValidation(Type, playwright);

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

        playwright.Dispose();
    }

    [Test]
    [TestCaseSource(nameof(HTMLRulesForTypeAnnotationValidation))]
    public async Task TestTypeAnnotationValidationRules(HTMLRule rule)
    {
        var playwright = await Playwright.CreateAsync();

        string Type = "TypeAnnotationValidation";

        IValidation validation = ValidationFactory.CreateValidation(Type, playwright);

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

        playwright.Dispose();
    }


    [Test]
    [TestCaseSource(nameof(HTMLRulesForMissingContentValidation))]
    public async Task TestMissingContentValidationRules(HTMLRule rule)
    {
        var playwright = await Playwright.CreateAsync();

        string Type = "MissingContentValidation";

        IValidation validation = ValidationFactory.CreateValidation(Type, playwright);

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

        playwright.Dispose();
    }

    [Test]
    [TestCaseSource(nameof(HTMLRulesForDuplicateServiceValidation))]
    public async Task TestDuplicateServiceValidationRules(HTMLRule rule)
    {
        var playwright = await Playwright.CreateAsync();

        string Type = "DuplicateServiceValidation";

        IValidation validation = ValidationFactory.CreateValidation(Type, playwright);

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

        playwright.Dispose();
    }

}


public static class ValidationFactory
{
    public static IValidation CreateValidation(string validationType, IPlaywright playwright)
    {
        return validationType switch
        {
            "UnnecessarySymbolsValidation" => new UnnecessarySymbolsValidation(playwright),
            "ExtraLabelValidation" => new ExtraLabelValidation(playwright),
            "TypeAnnotationValidation" => new TypeAnnotationValidation(playwright),
            "GarbledTextValidation" => new GarbledTextValidation(playwright),
            "MissingContentValidation" => new MissingContentValidation(playwright),
            "DuplicateServiceValidation" => new DuplicateServiceValidation(playwright),
            _ => throw new ArgumentException($"Unknown validation type: {validationType}")
        };
    }
}
