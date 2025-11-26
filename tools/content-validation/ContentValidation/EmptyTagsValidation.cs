using Microsoft.Playwright;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace UtilityLibraries;

public class EmptyTagsValidation : IValidation
{
    private IBrowser _browser;

    public EmptyTagsValidation(IBrowser browser)
    {
        _browser = browser ?? throw new ArgumentNullException(nameof(browser));
    }

    public async Task<TResult> Validate(string testLink)
    {
        // Create a new page from the shared browser instance.
        var page = await _browser.NewPageAsync();
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

        await page.CloseAsync();

        return res;
    }
}
