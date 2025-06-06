using Microsoft.Playwright;
using System.Text.RegularExpressions;

namespace UtilityLibraries;

public class CodeFormatValidation : IValidation
{
    private IPlaywright _playwright;

    public CodeFormatValidation(IPlaywright playwright)
    {
        _playwright = playwright ?? throw new ArgumentNullException(nameof(playwright));
    }

    public async Task<TResult> Validate(string testLink)
    {
        //Create a browser instance.
        var browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync();
        await PlaywrightHelper.GotoageWithRetriesAsync(page, testLink);

        var res = new TResult();
        var errorList = new List<string>();

        //Get all code content in the test page.
        var codeElements = await page.Locator("code").AllAsync();

        //Check if there are wrong code format.
        foreach (var element in codeElements)
        {
            var codeText = await element.InnerTextAsync();

            // Check the number of spaces at the beginning of the code line by line, and add errorList if it is an odd number. 
            // This method is not the best solution. Currently, there is no library in C# that can check the format of Java code. 
            // We can consider adding optimization in the future.
            string[] lines = codeText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                if(line.Trim().Length == 0)
                {
                    continue; // Skip empty lines
                }
                var match = Regex.Match(line, @"^(\s*)");
                if (match.Success)
                {
                    int spaceCount = match.Groups[1].Value.Length;

                    if (spaceCount % 2 != 0)
                    {
                        errorList.Add($"Improper whitespace formatting detected in code snippet: `{line}`");
                        break;
                    }
                }
            }

        }

        var formattedList = errorList
            .GroupBy(item => item)
            .Select((group, Index) => $"{Index + 1}. Appears {group.Count()} times ,  {group.Key}")
            .ToList();

        if (errorList.Count > 0)
        {
            res.Result = false;
            res.ErrorLink = testLink;
            res.NumberOfOccurrences = errorList.Count;
            res.ErrorInfo = "Incorrect code format: there is an error in the space format, please check.";
            res.LocationsOfErrors = formattedList;
        }

        await browser.CloseAsync();

        return res;
    }
}



