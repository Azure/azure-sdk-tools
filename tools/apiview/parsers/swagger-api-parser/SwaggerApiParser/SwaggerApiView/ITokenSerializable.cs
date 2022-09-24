using APIView;

namespace SwaggerApiParser;

public interface ITokenSerializable
{
    public CodeFileToken[] TokenSerialize(SerializeContext context);
}
