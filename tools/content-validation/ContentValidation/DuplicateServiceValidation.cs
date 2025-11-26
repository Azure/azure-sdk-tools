using Microsoft.Playwright;

namespace UtilityLibraries;

public class DuplicateServiceValidation : IValidation
{
    private IBrowser _browser;

    public DuplicateServiceValidation(IBrowser browser)
    {
        _browser = browser ?? throw new ArgumentNullException(nameof(browser));
    }

    public async Task<TResult> Validate(string testLink)
    {
        //Create a new page from the shared browser instance.
        var page = await _browser.NewPageAsync();
        await PlaywrightHelper.GotoageWithRetriesAsync(page, testLink);

        var res = new TResult();
        var set = new HashSet<string>();
        var errorList = new List<string>();

        //Get all service tags in the test page.
        var aElements = await page.Locator("li.has-three-text-columns-list-items.is-unstyled a[data-linktype='relative-path']").AllAsync();

        //Check if there are duplicate services.
        foreach (var element in aElements)
        {
            var text = await element.InnerTextAsync();

            //Store the names in the `HashSet`.
            //When `HashSet` returns false, duplicate service names are stored in another array.
            if (!set.Add(text))
            {
                errorList.Add(text);

                res.Result = false;
                res.ErrorLink = testLink;
                res.NumberOfOccurrences += 1;
            }

        }
        res.ErrorInfo = "Have Duplicate Service: " + string.Join(",", errorList);

        await page.CloseAsync();

        return res;
    }
}
