namespace SwaggerApiParser.SwaggerApiView
{
    public interface ITokenSerializable
    {
        public CodeFileToken[] TokenSerialize(SerializeContext context);
    }
}
