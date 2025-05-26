using System.Text.Json.Serialization;

public class TResult4Json
{
    [JsonPropertyName("NO.")]
    public int? Number { get; set; }
    public string? ErrorInfo { get; set; }
    public int? NumberOfOccurrences { get; set; }
    public List<string>? LocationsOfErrors { get; set; }
    public string? ErrorLink { get; set; }
    public string? TestCase { get; set; }
    public object? AdditionalNotes { get; set; }
    public string? Note { get; set; }
}

public class TPackage4Json
{
    public string? PackageName { get; set; }
    public List<TResult4Json>? ResultList { get; set; }
    public string? Note { get; set; }
}