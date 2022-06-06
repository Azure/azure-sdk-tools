using APIView;

namespace SwaggerApiParser;

public interface ITokenSerializable
{
    public CodeFileToken[] TokenSerialize(int intent = 0);
}
