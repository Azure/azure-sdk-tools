using System.Collections.Generic;
using APIView;

namespace SwaggerApiParser;

public class XMsParameterizedHost : ITokenSerializable
{
    public string hostTemplate { get; set; }
    public bool useSchemePrefix { get; set; }
    public string positionInOperation { get; set; }

    public List<Parameter> parameters { get; set; }

    public void ResolveParameters(SchemaCache schemaCache, string currentFilePath)
    {
        List<Parameter> ret = new List<Parameter>();
        if (this.parameters != null)
        {
            foreach (var parameter in this.parameters)
            {
                ret.Add(parameter.IsRefObject() ? schemaCache.GetResolvedParameter(parameter, currentFilePath) : parameter);
            }
        }

        this.parameters = ret;
    }

    public CodeFileToken[] TokenSerialize(SerializeContext context)
    {
        List<CodeFileToken> ret = new List<CodeFileToken>();
        if (hostTemplate != null)
        {
            ret.AddRange(TokenSerializer.KeyValueTokens("hostTemplate", hostTemplate, true, context.IteratorPath.CurrentNextPath("hostTemplate")));
        }

        ret.AddRange(TokenSerializer.KeyValueTokens("useSchemePrefix", useSchemePrefix.ToString(), true, context.IteratorPath.CurrentNextPath("useSchemePrefix")));

        if (positionInOperation != null)
        {
            ret.AddRange(TokenSerializer.KeyValueTokens("positionInOperation", positionInOperation, true, context.IteratorPath.CurrentNextPath("positionInOperation")));
        }

        if (parameters != null && parameters.Count > 0)
        {
            ret.Add(new CodeFileToken("Parameters", CodeFileTokenKind.FoldableSectionHeading));
            ret.Add(TokenSerializer.Colon());
            ret.Add(TokenSerializer.NewLine());
            ret.Add(TokenSerializer.FoldableContentStart());
            foreach (var parameter in parameters)
            {
                if (parameter != null)
                {
                    ret.AddRange(parameter.TokenSerialize(context));
                }
            }

            ret.Add(TokenSerializer.FoldableContentEnd());
        }

        return ret.ToArray();
    }
}
