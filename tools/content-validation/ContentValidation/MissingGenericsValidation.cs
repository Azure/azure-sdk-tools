using Microsoft.Playwright;

namespace UtilityLibraries;

public class MissingGenericsValidation : IValidation
{
    private IPlaywright _playwright;

    public MissingGenericsValidation(IPlaywright playwright)
    {
        _playwright = playwright;
    }
    public async Task<TResult> Validate(string testLink)
    {
        var browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync();
        await PlaywrightHelper.GotoageWithRetriesAsync(page, testLink);

        var res = new TResult();
        var allTags = await page.QuerySelectorAllAsync("body *");

        HashSet<string> missingGenericType = new HashSet<string>();
        List<string> errorList = new List<string>();

        HashSet<string> validHtmlTags = new HashSet<string>
        {
            // List of valid HTML tags
            "a", "abbr", "acronym", "address", "applet", "area", "article", "aside", "audio", "b", "base", "basefont", "bdi", "bdo", "big", "blockquote", "body", "br", "button", "canvas", "caption", "center", "cite", "code", "col", "colgroup", "data", "datalist", "dd", "del", "details", "dfn", "dialog", "dir", "div", "dl", "dt", "em", "embed", "fieldset", "figcaption", "figure", "font", "footer", "form", "frame", "frameset", "h1", "h2", "h3", "h4", "h5", "h6", "head", "header", "hgroup", "hr", "html", "i", "iframe", "img", "input", "ins", "kbd", "keygen", "label", "legend", "li", "link", "main", "map", "mark", "menu", "menuitem", "meta", "meter", "nav", "noframes", "noscript", "object", "ol", "optgroup", "option", "output", "p", "param", "picture", "pre", "progress", "q", "rp", "rt", "ruby", "s", "samp", "script", "section", "select", "small", "source", "span", "strike", "strong", "style", "sub", "summary", "sup", "table", "tbody", "td", "textarea", "tfoot", "th", "thead", "time", "title", "tr", "track", "tt", "u", "ul", "var", "video", "wbr", "rect", "svg", "path", "panel-controller", "search-expander", "overflow-menu", "panel-controller", "bread-crumbs", "circle", "line", "local-time"
        };

        var tagNames = await page.EvaluateAsync<string[]>("() => Array.from(document.querySelectorAll('body *'), el => el.tagName.toLowerCase())");

        var tagNameCounts = new Dictionary<string, int>();
        foreach (var tagName in tagNames)
        {
            if (!validHtmlTags.Contains(tagName))
            {
                if (!tagNameCounts.ContainsKey(tagName))
                {
                    tagNameCounts[tagName] = 0;
                }
                else
                {
                    tagNameCounts[tagName]++;
                }

                var count = tagNameCounts[tagName];
                var context = await page.EvaluateAsync<string>($"() => {{ const element = document.querySelectorAll('body {tagName}')[{count}]; return element ? element.parentElement.innerHTML : ''; }}");
                errorList.Add(context);

                // Extract the missing generic type from the context
                var extractedType = context.Contains('<') 
                    ? context.Substring(0, context.IndexOf('<')).Trim().Split(new[] { ' ', '(', ')' }, StringSplitOptions.RemoveEmptyEntries).Last()  
                    : context;
                missingGenericType.Add(extractedType);
            }
        }

        var formattedList = errorList
            .GroupBy(item => item)
            .Select((group, Index) => $"{Index + 1}. Appears {group.Count()} times ,  {group.Key}")
            .ToList();

        if (missingGenericType.Count > 0)
        {
            res.Result = false;
            res.ErrorLink = testLink;
            res.NumberOfOccurrences = missingGenericType.Count;
            res.ErrorInfo = "Missing generic: " + string.Join(",", missingGenericType);
            res.LocationsOfErrors = formattedList;
        }

        await browser.CloseAsync();

        return res;
    }
}