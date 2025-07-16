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

    public List<IgnoreItem> equalList = IgnoreData.GetIgnoreList("TypeAnnotationValidation", "equal");
    public List<IgnoreItem> ignoreList = IgnoreData.GetIgnoreList("CommonValidation", "contains");

    public class ParamError
    {
        public string ErrorMessage { get; set; } = "";
        public IElementHandle Element { get; set; } = null!;
    }

    public async Task<TResult> Validate(string testLink)
    {
        var res = new TResult();
        List<string> errorList = new List<string>();
        // Launch browser and new page
        var browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync();
        await PlaywrightHelper.GotoageWithRetriesAsync(page, testLink);

        // Get parameter maps with elements
        var pyClassParamMap = await GetParamMap(page, true);
        var pyMethodParamMap = await GetParamMap(page, false);

        // Validate parameters and collect errors with element references
        var classErrors = ValidParamMap(pyClassParamMap, true);
        var methodErrors = ValidParamMap(pyMethodParamMap, false);

        var allErrors = classErrors.Concat(methodErrors).ToList();

        foreach (var error in allErrors)
        {
            var elem = error.Element;

            // Execute JS to find nearest H2/H3 heading from error element
            var nearestHTagText = await elem.EvaluateAsync<string?>(@"element => {
                function findNearestHeading(startNode) {
                    let currentNode = startNode;

                    while (currentNode && currentNode.tagName !== 'BODY' && currentNode.tagName !== 'HTML') {
                        let sibling = currentNode.previousElementSibling;
                        while (sibling) {
                            if (['H2', 'H3'].includes(sibling.tagName)) {
                                return sibling.textContent?.trim() || '';
                            }

                            let childHeading = findNearestHeadingInChildren(sibling);
                            if (childHeading) {
                                return childHeading;
                            }

                            sibling = sibling.previousElementSibling;
                        }
                        currentNode = currentNode.parentElement;
                    }
                    return null;
                }

                function findNearestHeadingInChildren(node) {
                    for (let child of node.children) {
                        if (['H2', 'H3'].includes(child.tagName)) {
                            return child.textContent?.trim() || '';
                        }
                        let grandChildHeading = findNearestHeadingInChildren(child);
                        if (grandChildHeading) {
                            return grandChildHeading;
                        }
                    }
                    return null;
                }

                return findNearestHeading(element);
            }");

            if (!string.IsNullOrEmpty(nearestHTagText))
            {
                nearestHTagText = nearestHTagText.Replace("\n", "").Replace("\t", "");

                if (ignoreList.Any(item => nearestHTagText.Equals(item.IgnoreText, StringComparison.OrdinalIgnoreCase)))
                {
                    continue; // Skip if the nearest heading text is in the ignore list
                }

                errorList.Add($"{error.ErrorMessage}");
            }
        }

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
        else
        {
            res.Result = true;
        }

        await browser.CloseAsync();

        return res;
    }


    // Determine whether the parameter correctly contains type annotation
    bool IsCorrectTypeAnnotation(string text)
    {
        if (equalList.Any(item => text.Equals(item.IgnoreText)))
        {
            return true;
        }

        if (Regex.IsMatch(text, @"^[^=]+=[^=]+$"))  // pattern like a=b
        {
            return true;
        }
        return false;
    }

    // Get parameter mappings and their corresponding element handles
    async Task<List<(string key, List<string> paramList, IElementHandle element)>> GetParamMap(IPage page, bool isClass)
    {
        var result = new List<(string, List<string>, IElementHandle)>();

        IReadOnlyList<IElementHandle>? elementHandles = null;

        if (isClass)
        {
            // Skip Enum type classes
            var h1Locator = page.Locator(".content h1:first-of-type");
            string h1Text = "";
            try
            {
                h1Text = await h1Locator.InnerTextAsync();
            }
            catch
            {
                var h1Alt = page.Locator(".content h1:first-child");
                h1Text = await h1Alt.InnerTextAsync();
            }
            if (h1Text.Contains("Enum", StringComparison.OrdinalIgnoreCase))
            {
                return result;
            }

            elementHandles = await page.Locator(".content > .wrap.has-inner-focus").ElementHandlesAsync();
        }
        else
        {
            elementHandles = await page.Locator(".memberInfo > .wrap.has-inner-focus").ElementHandlesAsync();
        }

        foreach (var elem in elementHandles)
        {
            var codeText = await elem.InnerTextAsync();

            var regex = new Regex(@"(?<key>.+?)\((?<params>.+?)\)");
            var match = regex.Match(codeText);

            if (match.Success)
            {
                var key = match.Groups["key"].Value.Trim();
                var paramsText = match.Groups["params"].Value.Trim();
                var paramList = SplitParameters(paramsText);
                result.Add((key, paramList, elem));
            }
        }

        return result;
    }

    // Split parameters by comma, considering brackets that may contain commas
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

    // Validate whether parameters all have type annotations, returning error info and corresponding element
    List<ParamError> ValidParamMap(List<(string key, List<string> paramList, IElementHandle element)> paramMap, bool isClass)
    {
        var errorList = new List<ParamError>();

        foreach (var (key, paramList, element) in paramMap)
        {
            if (ignoreList.Any(item => key.Contains(item.IgnoreText)))
                continue;

            string errorParams = "";

            foreach (var param in paramList)
            {
                if (!IsCorrectTypeAnnotation(param))
                {
                    errorParams += param + " ; ";
                }
            }

            if (!string.IsNullOrEmpty(errorParams))
            {
                errorList.Add(new ParamError
                {
                    ErrorMessage = (isClass ? "Class name:  " : "Method name: ") + key + ", arguments: " + errorParams,
                    Element = element
                });
            }
        }

        return errorList;
    }
}
