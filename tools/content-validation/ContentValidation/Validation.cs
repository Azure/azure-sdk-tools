namespace UtilityLibraries;

public interface IValidation
{
    Task<TResult> Validate(string testLink);
}

public struct TResult
{
    public bool Result { get; set; }
    public string? TestCase { get; set; }
    public string? ErrorLink { get; set; }
    public string? ErrorInfo { get; set; }
    public int NumberOfOccurrences { get; set; }
    public List<string> LocationsOfErrors { get; set; }
    public object? AdditionalNotes { get; set; }

    public TResult()
    {
        Result = true;
        TestCase = "";
        ErrorLink = "";
        ErrorInfo = "";
        NumberOfOccurrences = 0;
        LocationsOfErrors = new List<string>();
    }

    public string FormatErrorMessage()
    {
        return $@"Error Report:
{"\t"}Test Case: {TestCase}.
{"\t"}Error Link: {ErrorLink}.
{"\t"}Error Info: {ErrorInfo}.
{"\t"}Number of Occurrences: {NumberOfOccurrences}." + ((LocationsOfErrors.Count == 0) ? "\n" : $@"
{"\t"}Locations of Errors:
{"\t"}{string.Join("\n\t", LocationsOfErrors)}
");
    }
}
