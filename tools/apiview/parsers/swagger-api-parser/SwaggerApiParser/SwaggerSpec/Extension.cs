namespace SwaggerApiParser;

public class XMSEnum{
    public string name { get; set; }
    public bool modelAsString { get; set; }

    public string ToKeywords()
    {
        return $"x-ms-enum: name: {name}, modelAsString: {modelAsString}";
    }
}