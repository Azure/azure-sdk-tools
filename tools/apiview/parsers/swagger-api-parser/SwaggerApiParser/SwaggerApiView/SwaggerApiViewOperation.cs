using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using SwaggerApiParser.Specs;

namespace SwaggerApiParser.SwaggerApiView
{
    public class SwaggerApiViewOperation : ITokenSerializable
    {
        public List<string> tags { get; set; }
        public string summary { get; set; }
        public string description { get; set; }
        public ExternalDocs externalDocs { get; set; }
        public string operationId { get; set; }
        public List<string> consumes { get; set; }
        public List<string> produces { get; set; }
        public SwaggerApiViewOperationParameters PathParameters { get; set; }
        public SwaggerApiViewOperationParameters QueryParameters { get; set; }
        public SwaggerApiViewOperationParameters BodyParameters { get; set; }
        public SwaggerApiViewOperationParameters HeaderParameters { get; set; }
        public List<SwaggerApiViewResponse> Responses { get; set; }
        public List<string> schemes { get; set; }
        public bool deprecated { get; set; }
        public List<Security> security { get; set; }
        public IDictionary<string, JsonElement> patternedObjects { get; set; }


        public string operationIdPrefix;
        public string operationIdAction { get; set; }
        public string method { get; set; }
        public string path { get; set; }
        public Operation operation { get; set; }

        public string _iteratorPath;

        public CodeFileToken[] TokenSerialize(SerializeContext context)
        {
            List<CodeFileToken> ret = new List<CodeFileToken>();

            ret.Add(TokenSerializer.FoldableContentStart());
            if (tags != null && tags.Count > 0)
            {
                ret.Add(TokenSerializer.NavigableToken("tags", CodeFileTokenKind.Keyword, context.IteratorPath.CurrentNextPath("tags")));
                ret.Add(TokenSerializer.Colon());
                ret.Add(new CodeFileToken(string.Join(", ", tags), CodeFileTokenKind.Literal));
                ret.Add(TokenSerializer.NewLine());
            }

            if (!string.IsNullOrEmpty(this.summary))
            {
                ret.Add(TokenSerializer.NavigableToken("summary", CodeFileTokenKind.Keyword, context.IteratorPath.CurrentNextPath("summary")));
                ret.Add(TokenSerializer.Colon());
                ret.Add(new CodeFileToken(this.summary, CodeFileTokenKind.Literal));
                ret.Add(TokenSerializer.NewLine());
            }

            if (!string.IsNullOrEmpty(this.description))
            {
                ret.Add(TokenSerializer.NavigableToken("description", CodeFileTokenKind.Keyword, context.IteratorPath.CurrentNextPath("description")));
                ret.Add(TokenSerializer.Colon());
                ret.Add(new CodeFileToken(this.description, CodeFileTokenKind.Literal));
                ret.Add(TokenSerializer.NewLine());
            }

            if (externalDocs != null)
            {
                ret.Add(new CodeFileToken("externalDocs", CodeFileTokenKind.FoldableSectionHeading));
                ret.Add(TokenSerializer.Colon());
                ret.Add(TokenSerializer.NewLine());
                ret.Add(TokenSerializer.FoldableContentStart());
                ret.AddRange(externalDocs.TokenSerialize(context));
                ret.Add(TokenSerializer.FoldableContentEnd());
            }

            if (!string.IsNullOrEmpty(this.operationId)) 
            {
                ret.Add(TokenSerializer.NavigableToken("operationId", CodeFileTokenKind.Keyword, context.IteratorPath.CurrentNextPath("operationId")));
                ret.Add(TokenSerializer.Colon());
                ret.Add(new CodeFileToken(this.operationId, CodeFileTokenKind.TypeName));
                ret.Add(TokenSerializer.NewLine());
            }

            if (this.consumes != null && consumes.Count > 0)
            {
                ret.AddRange(TokenSerializer.KeyValueTokens("consumes", string.Join(", ", this.consumes)));
            }

            if (this.produces != null && produces.Count > 0)
            {
                ret.AddRange(TokenSerializer.KeyValueTokens("produces", string.Join(", ", this.produces)));
            }

            if (this.schemes != null && schemes.Count > 0)
            {
                ret.AddRange(TokenSerializer.KeyValueTokens("schemes", string.Join(", ", this.schemes)));
            }

            if (this.deprecated)
            {
                ret.Add(TokenSerializer.NavigableToken("deprecated", CodeFileTokenKind.Keyword, context.IteratorPath.CurrentNextPath("deprecated")));
                ret.Add(TokenSerializer.NewLine());
            }

            if (this.security != null && security.Count > 0)
            {
                ret.AddRange(TokenSerializer.KeyValueTokens("security", string.Join(", ", this.security)));
            }

            Utils.SerializePatternedObjects(patternedObjects, ret);

            // new line for `Parameters` section.
            ret.Add(TokenSerializer.NewLine());

            ret.AddRange(PathParameters.TokenSerialize(new SerializeContext(context.indent + 1, context.IteratorPath, context.definitionsNames)));
            ret.AddRange(QueryParameters.TokenSerialize(new SerializeContext(context.indent + 1, context.IteratorPath, context.definitionsNames)));
            ret.AddRange(BodyParameters.TokenSerialize(new SerializeContext(context.indent + 1, context.IteratorPath, context.definitionsNames)));
            ret.AddRange(HeaderParameters.TokenSerialize(new SerializeContext(context.indent + 1, context.IteratorPath, context.definitionsNames)));

            // new line for `Response` section.
            ret.Add(TokenSerializer.NewLine());

            ret.Add(TokenSerializer.NavigableToken("Responses", CodeFileTokenKind.Keyword, context.IteratorPath.CurrentNextPath("Responses")));
            ret.Add(TokenSerializer.Colon());
            ret.Add(TokenSerializer.NewLine());

            context.IteratorPath.Add("Responses");
            foreach (var response in Responses)
            {
                ret.AddRange(response.TokenSerialize(new SerializeContext(context.indent + 1, context.IteratorPath, context.definitionsNames)));
            }
            context.IteratorPath.Pop();

            ret.Add(TokenSerializer.NewLine());

            ret.Add(TokenSerializer.FoldableContentEnd());

            return ret.ToArray();
        }
    }

    public class SwaggerApiViewOperationComp : IComparer<SwaggerApiViewOperation>
    {
        public int Compare(SwaggerApiViewOperation a, SwaggerApiViewOperation b)
        {
            Dictionary<string, int> priority = new Dictionary<string, int>()
        {
            {"post", 1},
            {"put", 2},
            {"patch", 3},
            {"get", 4},
            {"get-action", 5},
            {"delete", 6},
            {"post-action", 7}
        };


            priority.TryGetValue(GetMethodType(a), out var priorityA);
            priority.TryGetValue(GetMethodType(b), out var priorityB);
            if (priorityA == priorityB)
            {
                return 0;
            }

            if (priorityA < priorityB)
            {
                return -1;
            }

            return 1;
        }

        /*
         * Post:  /<service>/<resource-collection>/<resource-id>. CreateOrUpdate
         * Post action:  /<service>/<resource-collection>/<resource-id>:<action>. PostAction
         */
        private static string GetMethodType(SwaggerApiViewOperation operation)
        {
            return operation.method switch
            {
                "post" => operation.path == null || operation.path.Split("/").Length % 2 == 1 ? "post" : "post-action",
                "get" => operation.path == null || operation.path.Split("/").Length % 2 == 1 ? "get" : "get-action",
                _ => operation.method
            };
        }
    }
}
