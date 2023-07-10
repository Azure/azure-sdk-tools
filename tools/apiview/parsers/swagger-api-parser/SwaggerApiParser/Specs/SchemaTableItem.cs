using System;
using System.Collections.Generic;
using System.Linq;
using SwaggerApiParser.SwaggerApiView;

namespace SwaggerApiParser.Specs
{
    public class SchemaTableItem
    {
        public string Model { get; set; }
        public string Field { get; set; }
        public string TypeFormat { get; set; }
        public string Keywords { get; set; }
        public string Description { get; set; }

        public CodeFileToken[] TokenSerialize(SerializeContext context)
        {
            List<CodeFileToken> ret = new List<CodeFileToken>();
            string[] serializedFields = new[] { "Model", "Field", "TypeFormat", "Keywords", "Description" };
            ret.AddRange(this.TokenSerializeWithOptions(serializedFields, context));
            return ret.ToArray();
        }

        public CodeFileToken[] TokenSerializeWithOptions(string[] serializedFields, SerializeContext context)
        {
            List<CodeFileToken> ret = new List<CodeFileToken>();
            foreach (var property in this.GetType().GetProperties())
            {
                if (serializedFields.Contains(property.Name))
                {
                    if (property.Name == "Model" && TypeFormat == "object" && context.definitionsNames.Contains(Model))
                    {
                        var navigateToId = $"{context.IteratorPath.rootPath()}-Definitions-{Model}";
                        ret.AddRange(TokenSerializer.TableCell(new[] { new CodeFileToken(Model, CodeFileTokenKind.TypeName) { NavigateToId = navigateToId } }));
                    }
                    else if (property.Name == "TypeFormat" && !String.IsNullOrEmpty(TypeFormat))
                    {
                        if (TypeFormat.StartsWith("array") || TypeFormat.StartsWith("enum"))
                        {
                            var arrayType = TypeFormat.Substring(TypeFormat.IndexOf('<') + 1).TrimEnd('>');
                            if (context.definitionsNames.Contains(arrayType))
                            {
                                var navigateToId = $"{context.IteratorPath.rootPath()}-Definitions-{arrayType}";
                                ret.AddRange(TokenSerializer.TableCell(new[] { new CodeFileToken(TypeFormat, CodeFileTokenKind.TypeName) { NavigateToId = navigateToId } }));
                            }
                            else
                            {
                                ret.AddRange(TokenSerializer.TableCell(new[] { new CodeFileToken(TypeFormat, CodeFileTokenKind.Literal) }));
                            }
                        }
                        else if (context.definitionsNames.Contains(TypeFormat))
                        {
                            var navigateToId = $"{context.IteratorPath.rootPath()}-Definitions-{TypeFormat}";
                            ret.AddRange(TokenSerializer.TableCell(new[] { new CodeFileToken(TypeFormat, CodeFileTokenKind.TypeName) { NavigateToId = navigateToId } }));
                        }
                        else 
                        {
                            ret.AddRange(TokenSerializer.TableCell(new[] { new CodeFileToken(TypeFormat, CodeFileTokenKind.Literal) }));
                        }
                    }
                    else if (property.Name == "Field")
                    {
                        ret.AddRange(TokenSerializer.TableCell(new[] { new CodeFileToken(Field, CodeFileTokenKind.MemberName) }));
                    }
                    else
                    {
                        ret.AddRange(TokenSerializer.TableCell(new[] { new CodeFileToken(property.GetValue(this, null)?.ToString(), CodeFileTokenKind.Literal) }));
                    }
                }
            }

            return ret.ToArray();
        }
    }
}
