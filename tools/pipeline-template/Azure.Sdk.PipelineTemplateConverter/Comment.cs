namespace Azure.Sdk.PipelineTemplateConverter;

public class Comment
{
    public List<string> Value { get; set; } = new List<string>();
    // NOTE: this won't handle duplicate lines, but probably not a case that will be hit
    public string AppearsBeforeLine { get; set; } = string.Empty;
    public string AppearsOnLine { get; set; } = string.Empty;
    public int LineInstance { get; set; } = 0;
    public bool NewLineBefore { get; set; } = false;

    public Comment(List<string> value)
    {
        Value = value;
    }

    public Comment(string value)
    {
        Value = new List<string> { value };
    }
}
