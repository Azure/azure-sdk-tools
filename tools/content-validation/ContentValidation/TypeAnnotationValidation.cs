using Microsoft.Playwright;
using System.Text.RegularExpressions;

namespace UtilityLibraries;

public class TypeAnnotationValidation : IValidation
{
    private IPlaywright _playwright;

    public TypeAnnotationValidation(IPlaywright playwright)
    {
        _playwright = playwright ?? throw new ArgumentNullException(nameof(playwright));
    }

    public async Task<TResult> Validate(string testLink)
    {
        var res = new TResult();
        List<string> errorList = new List<string>();

        // Create a browser instance.
        var browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync();
        await PlaywrightHelper.GotoageWithRetriesAsync(page, testLink);

        // Get all the class and method parameters in the test page.
        Dictionary<string, List<string>>? pyClassParamMap = null;
        Dictionary<string, List<string>>? pyMethodParamMap = null;
        pyClassParamMap = await GetParamMap(page, true);
        pyMethodParamMap = await GetParamMap(page, false);

        // Check each class and method parameter for correct type annotations and record any missing or incorrect ones.
        var list1 = ValidParamMap(pyClassParamMap!, true);
        var list2 = ValidParamMap(pyMethodParamMap!, false);
        errorList.AddRange(list1);
        errorList.AddRange(list2);

        for (int i = 0; i < errorList.Count; i++)
        {
            errorList[i] = $"{i + 1}. {errorList[i]}";
        }

        if (errorList.Count > 0)
        {
            res.Result = false;
            res.ErrorLink = testLink;
            res.ErrorInfo = "Missing Type Annotation";
            res.NumberOfOccurrences = errorList.Count;
            res.LocationsOfErrors = errorList;
        }

        await browser.CloseAsync();

        return res;
    }


    // If the parameter is "*" ,"/","**kwargs","*args","**kw", it indicates that no type annotation is required.
    // If the parameter follows the format a=b (e.g., param1=null), it means a default value has been assigned to the parameter.
    // If the parameter follows the format a:b (e.g., param1:int), it means a type annotation has been provided for the parameter.
    bool IsCorrectTypeAnnotation(string text)
    {
        if (text == "*" || text == "/" || text == "**kwargs" || text == "*args" || text == "**kw")
        {
            return true;
        }
        if (Regex.IsMatch(text, @"^[^=]+=[^=]+$"))
        {
            return true;
        }
        if (text.Contains(":"))
        {
            return true;
        }
        return false;
    }

    async Task<Dictionary<string, List<string>>> GetParamMap(IPage page, bool isClass)
    {
        Dictionary<string, List<string>> paramMap = new Dictionary<string, List<string>>();

        IReadOnlyList<ILocator>? HTMLElementList = null;

        if (isClass)
        {
            // Check if the h1 text contains 'Enum'.
            var h1Locator = page.Locator(".content h1:first-of-type");
            var h1Text = "";

            try
            {
                h1Text = await h1Locator.InnerTextAsync();
            }
            catch 
            {
                h1Locator = page.Locator(".content h1:first-child");
                h1Text = await h1Locator.InnerTextAsync();
            }
            
            if (h1Text.Contains("Enum", StringComparison.OrdinalIgnoreCase))
            {
                return paramMap;
            }

            HTMLElementList = await page.Locator(".content > .wrap.has-inner-focus").AllAsync();
        }
        else
        {
            HTMLElementList = await page.Locator(".memberInfo > .wrap.has-inner-focus").AllAsync();
        }

        for (int i = 0; i < HTMLElementList.Count; i++)
        {
            var HTMLElement = HTMLElementList[i];
            var codeText = await HTMLElement.InnerTextAsync();

            // Usage: This regex is used to extract a key and its parameters from strings in the format "key(params)". Example: For the codeText - "fn(param1: int, param2: str)", it extracts "fn" as the key and "param1: int, param2: str" as the parameters.
            var regex = new Regex(@"(?<key>.+?)\((?<params>.+?)\)");
            var match = regex.Match(codeText);

            string key = "";
            string paramsText = "";

            if (match.Success)
            {
                key = match.Groups["key"].Value.Trim();
                paramsText = match.Groups["params"].Value.Trim();
            }

            var paramList = SplitParameters(paramsText);

            paramMap[key] = paramList;
        }

        return paramMap;
    }



    List<string> SplitParameters(string paramsText)
    {
        var paramList = new List<string>();
        int bracketCount = 0;
        string currentParam = "";

        for (int i = 0; i < paramsText.Length; i++)
        {
            char c = paramsText[i];

            if (c == '[')
            {
                bracketCount++;
            }
            else if (c == ']')
            {
                bracketCount--;
            }
            else if (c == ',' && bracketCount == 0)
            {
                paramList.Add(currentParam.Trim());
                currentParam = "";
                continue;
            }

            currentParam += c;
        }

        if (!string.IsNullOrWhiteSpace(currentParam))
        {
            paramList.Add(currentParam.Trim());
        }

        return paramList;
    }

    List<string> ValidParamMap(Dictionary<string, List<string>> paramMap, bool isClass)
    {

        List<string> errorList = new List<string>();

        // Extract parameter maps for classes and methods.
        foreach (var item in paramMap)
        {
            string key = item.Key;
            var paramList = item.Value;

            // If a parameter is found to be missing a type annotation, add it to the errorList.
            string errorMessage = isClass ? "Class name:  " : "Method name: ";
            string errorParam = "";

            for (int i = 0; i < paramList.Count; i++)
            {
                var text = paramList[i];

                if (!IsCorrectTypeAnnotation(text))
                {
                    errorParam += text + " ;    ";
                }
            }

            if (errorParam.Length > 0)
            {
                errorList.Add(errorMessage + key + ",    arguments:  " + errorParam);
            }
        }

        return errorList;
    }
}