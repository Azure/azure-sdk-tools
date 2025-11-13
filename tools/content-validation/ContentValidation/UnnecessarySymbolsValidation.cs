using Microsoft.Playwright;
using System.Text.RegularExpressions;

namespace UtilityLibraries;

public class UnnecessarySymbolsValidation : IValidation
{
    private IPlaywright _playwright;

    public HashSet<string> valueSet = new HashSet<string>();

    public List<string> errorList = new List<string>();

    public TResult res = new TResult();

    // Prefix list for checking if the content before the "[" is in the list.
    public List<IgnoreItem> regularList = IgnoreData.GetIgnoreList("CommonValidation", "regular");

    public List<IgnoreItem> commonIgnore = IgnoreData.GetIgnoreList("CommonValidation", "contains");

    public List<IgnoreItem> prefixList = IgnoreData.GetIgnoreList("UnnecessarySymbolsValidation", "prefix");
    
    public List<IgnoreItem> ignoreListBefore = IgnoreData.GetIgnoreList("UnnecessarySymbolsValidation", "before]");

    // Prefix list for checking if the content before the "[" is in the list.
    public List<IgnoreItem> containList01 = IgnoreData.GetIgnoreList("UnnecessarySymbolsValidation", "<contains>");

    // Content list for checking if the content between "[ ]" is in the list.
    public List<IgnoreItem> containList02 = IgnoreData.GetIgnoreList("UnnecessarySymbolsValidation", "[contains]");

    public UnnecessarySymbolsValidation(IPlaywright playwright)
    {
        _playwright = playwright ?? throw new ArgumentNullException(nameof(playwright));
    }

    public async Task<TResult> Validate(string testLink)
    {

        //Create a browser instance.
        var browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync();
        await PlaywrightHelper.GotoageWithRetriesAsync(page, testLink);

        // This method needs to be called before "GetHtmlContent()" because "GetHtmlContent()" will delete the code element.
        //Fetch all 'code' content to store in a list. Use regular expressions to find matching unnecessary symbols.
        var codeBlocks = await page.Locator("code").AllInnerTextsAsync();
        ValidateCodeContent(codeBlocks);

        //Fetch all text content to store in a list. Use regular expressions to find matching unnecessary symbols.
        string htmlContent = await GetHtmlContent(page);
        ValidateHtmlContent(htmlContent);


        var formattedList = errorList
            .GroupBy(item => item)
            .Select((group, Index) => $"{Index + 1}. Appears {group.Count()} times ,  {group.Key}")
            .ToList();

        if (errorList.Count > 0)
        {
            res.Result = false;
            res.ErrorLink = testLink;
            res.NumberOfOccurrences = errorList.Count;
            res.ErrorInfo = $"Unnecessary symbols found: `{string.Join(" ,", valueSet)}`";
            res.LocationsOfErrors = formattedList;
        }

        await browser.CloseAsync();

        return res;
    }

    private void ValidateCodeContent(IReadOnlyList<string> codeBlocks)
    {
        string includePattern = @"~";

        foreach (string codeBlock in codeBlocks)
        {
            string[] lines = codeBlock.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in lines)
            {
                if (prefixList.Any(item => line.Trim().Contains(item.IgnoreText)) || commonIgnore.Any(item => line.Trim().Contains(item.IgnoreText)))
                {
                    continue;
                }

                var matchCollections = Regex.Matches(line, includePattern);
                foreach (Match match in matchCollections)
                {
                    string unnecessarySymbol = $"\"{match.Value}\""; 
                    valueSet.Add(unnecessarySymbol);
                    errorList.Add($"Unnecessary symbol: {unnecessarySymbol} in code: `{line}`");
                }
            }
        }
    }

    private void ValidateHtmlContent(string htmlContent)
    {
        // Usage: Find the text that include [ , ], < , >, &, ~, and /// symbols.
        string includePattern = @"[\[\]<>~]|/{3}";

        // Usage: 
        // (?<=\w\s)[<>](?=\s\w): When the text contains symbols  < or >, exclude cases where they are used in a comparative context (e.g., a > b).
        // <\s*[a-zA-Z_][a-zA-Z0-9_]*(\s*,\s*[a-zA-Z_][a-zA-Z0-9_]*)*\s*(\[\s*\])*\s*>: <String>, <Integer, Double>, <String[]>, <String, Integer[], MyClass[]>
        // <\s*\?\s*(extends\s+[A-Za-z_][A-Za-z0-9_]*\s*,\?\s*)*\s*>: <? extends T, ? extends T, U>, <?>
        // Example: HTMLText - A list of stemming rules in the following format: "word => stem", for example: "ran => run".
        string excludePattern1 = @"(?<=\w\s)[<>](?=\s\w)|<\s*[a-zA-Z_][a-zA-Z0-9_]*(\s*,\s*[a-zA-Z_][a-zA-Z0-9_]*)*\s*(\[\s*\])*\s*>|<\s*\?\s*(extends\s+[A-Za-z_][A-Za-z0-9_]*\s*,\?\s*)*\s*>";

        // New pattern to match the specified conditions.(e.g., /** hello , **note:** , "word.)
        string newPatternForJava = @"\s\""[a-zA-Z]+\.(?![a-zA-Z])|^\s*/?\*\*.*$";

        string[] lines = htmlContent.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);

        for (int index = 0; index < lines.Length; index++)
        {
            string line = lines[index];

            // Check for valid interval symbols
            if (IsValidInterval(line))
            {
                continue; // Skip this line if it contains a valid interval symbol
            }
            
            var matchCollections = Regex.Matches(line, includePattern);
            
            foreach (Match match in matchCollections)
            {
                // Console.WriteLine($"Unnecessary symbol: {match.Value} in text: `{line}`");
                if (match.Value.Equals("<") || match.Value.Equals(">"))
                {
                    if (AreAngleBracketsPaired(line))
                    {
                        continue; 
                    }

                    // This case is not an issue in java doc, we will move it in ignore pattern.
                    if (containList01.Any(item => line.Contains(item.IgnoreText)) || commonIgnore.Any(item => line.Contains(item.IgnoreText)))
                    {
                        continue;
                    }

                    if (line.Contains(">>") && line.Contains("<<"))
                    {
                        continue;
                    }
                    if (Regex.IsMatch(line, excludePattern1))
                    {
                        continue;
                    }
                    // Usage: When the text contains symbols => , -< , ->, exclude cases where they are used in a comparative context (e.g., a > b).
                    // Example: HTMLText - A list of stemming rules in the following format: "word => stem", for example: "ran => run".
                    // Link: https://learn.microsoft.com/en-us/python/api/azure-search-documents/azure.search.documents.indexes.models.stemmeroverridetokenfilter?view=azure-python#keyword-only-parameters
                    int i = match.Index - 1;
                    if (i >= 0 && (line[i] == '=' || line[i] == '-'))
                    {
                        continue;
                    }
                }

                if (match.Value.Equals("]"))
                {
                    if (line.Contains("["))
                    {
                        continue;
                    }
                    
                    if (ignoreListBefore.Any(item => line.Contains(item.IgnoreText)))
                    {
                        continue;
                    }
                }

                if (match.Value.Equals("["))
                {
                    if (IsBracketCorrect(line, match.Index))
                    {
                        continue;
                    }
                    if (containList02.Any(item => line.Contains(item.IgnoreText)))
                    {
                        continue;
                    }
                }

                List<string> matchSymbols = new List<string>() { "[", "]", "<", ">" };
                string unnecessarySymbol = $"\"{match.Value}\""; ;
                valueSet.Add(unnecessarySymbol);
                
                if(matchSymbols.Contains(match.Value))
                {
                    errorList.Add($"Symbols `{unnecessarySymbol}` do not match in text: `{line}`");
                }
                else{
                    errorList.Add($"Unnecessary symbol: `{unnecessarySymbol}` in text: `{line}`");
                }
               
            }

            // Check the new patternForJava
            Match matchData = Regex.Match(line, newPatternForJava);
            if (matchData.Success)
            {
                string matchedContent = matchData.Value;
                string unnecessarySymbol = $"\"{matchedContent}\"";
                valueSet.Add(unnecessarySymbol);
                errorList.Add($"Unnecessary symbol: `{unnecessarySymbol}` in text: `{line}`"); 
            }
        }
    }

    private bool IsBracketCorrect(string input, int index)
    {
        // Check if the content before "[" is in the prefix list
        foreach (var ignoreItem in prefixList)
        {
            string prefix = ignoreItem.IgnoreText;
            int prefixLength = prefix.Length;
            if (index >= prefixLength)
            {
                string prefixStr = input.Substring(index - prefixLength, prefixLength);
                if (prefixStr.Equals(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        // Use a stack to track '[' and ']'
        Stack<char> stack = new Stack<char>();

        // Traverse the string to check if brackets are paired correctly
        for (int i = 0; i < input.Length; i++)
        {
            if (input[i] == '[')
            {
                // Push '[' onto the stack
                stack.Push(input[i]);
            }
            else if (input[i] == ']')
            {
                // Check if the stack is not empty and the top of the stack is '['
                if (stack.Count > 0 && stack.Peek() == '[')
                {
                    // Pop the stack, indicating a successful match
                    stack.Pop();
                }
                else
                {
                    // If the stack is empty or the top is not '[', it indicates a mismatch
                    return false;
                }
            }
        }

        // If the stack is empty, all '[' and ']' are correctly paired
        if (stack.Count != 0)
        {
            return false;
        }

        // Check if the content is in the containList02
        if (input[index] == '[')
        {
            // Extract the content between '[' and ']'
            int startIndex = index;
            int endIndex = input.IndexOf("]", startIndex);
            if (endIndex == -1)
            {
                // If no corresponding ']' is found
                return false;
            }

            string contentBetweenBrackets = input.Substring(startIndex + 1, endIndex - startIndex - 1);

            // Check if the content is in the containList02
            if (containList02.Any(content => contentBetweenBrackets.Contains(content.IgnoreText, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            // Check if the content contains only letters
            if (Regex.IsMatch(contentBetweenBrackets, @"^[A-Za-z]+$"))
            {
                return true;
            }
        }

        return true;
    }

    private bool AreAngleBracketsPaired(string input)
    {
        // use the stack to keep track of '<' and '>'
        Stack<char> stack = new Stack<char>();

        foreach (char c in input)
        {
            if (c == '<')
            {
                // Encounter '<', press to stack
                stack.Push(c);
            }
            else if (c == '>')
            {
                // When '>' is encountered, check if the stack is empty.
                if (stack.Count > 0 && stack.Peek() == '<')
                {
                    // If the top of the stack is '<', pop it, indicating a successful match
                    stack.Pop();
                }
                else
                {
                    // If the stack is empty or the top of the stack is not a '<', then it is a mismatch
                    return false;
                }
            }
        }

        // If the stack is empty, all '<' and '>' pairs match.
        return stack.Count == 0;
    }

    private async Task<string> GetHtmlContent(IPage page)
    {
        var contentElements = await page.QuerySelectorAllAsync(".content");

        var combinedText = "";
        foreach (var contentElement in contentElements)
        {
            await contentElement.EvaluateAsync(@"(element) => {
                const codeElements = element.querySelectorAll('code');
                codeElements.forEach(code => code.remove());
            }");
            var text = await contentElement.InnerTextAsync();
            combinedText += text + "\n";
        }
        return combinedText;
    }

    public bool IsValidInterval(string line)
    {
        foreach (var pattern in regularList)
        {
            if (Regex.IsMatch(line, pattern.IgnoreText))
            {
                return true;
            }
        }
        return false;
    }
}