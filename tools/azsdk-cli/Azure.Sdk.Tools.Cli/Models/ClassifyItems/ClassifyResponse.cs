namespace Azure.Sdk.Tools.Cli.Models.ClassifyItems
{
    public class ClassifyResponse
    {
        public ClassifyType ClassifyType { get; set; }
        public object? ClassifiedResult { get; set; }
        public ClassifyResponse(ClassifyType classifyType, object? classifiedResult)
        {
            ClassifyType = classifyType;
            ClassifiedResult = classifiedResult;
        }
    }
}
