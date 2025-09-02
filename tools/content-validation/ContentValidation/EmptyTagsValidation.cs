using Microsoft.Playwright;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace UtilityLibraries;

public class EmptyTagsValidation : IValidation
{
    private IPlaywright _playwright;

    public EmptyTagsValidation(IPlaywright playwright)
    {
        _playwright = playwright ?? throw new ArgumentNullException(nameof(playwright));
    }

    public async Task<TResult> Validate(string testLink)
    {
        // Create a browser instance.
        var browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync();
        await PlaywrightHelper.GotoageWithRetriesAsync(page, testLink);

        var res = new TResult();
        var errorList = new List<string>();

        // Get all <li> elements
        var liElements = await page.Locator("main#main li").ElementHandlesAsync();

        foreach (var li in liElements)
        {
            // Get and trim inner text
            var rawText = await li.EvaluateAsync<string>("el => el.textContent");
            var text = rawText?.Trim() ?? "";

            if (text == "")
            {
                // Add to error list
                errorList.Add("<li></li>");
            }
        }
        

        if (errorList.Any())
        {
            var formattedList = errorList
                .GroupBy(item => item)
                .Select((group, index) => $"{index + 1}. Appears {group.Count()} times, `{group.Key}`")
                .ToList();

            res.Result = false;
            res.ErrorLink = testLink;
            res.NumberOfOccurrences = errorList.Count;
            res.ErrorInfo = "There are empty `<li>` tags on the page.";
            res.LocationsOfErrors = formattedList;
        }

        await browser.CloseAsync();

        return res;
    }
}
