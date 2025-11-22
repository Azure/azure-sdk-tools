using System.Collections;
using System.Reflection;
using APIView.Model.V2;
using Microsoft.CodeAnalysis;
using NJsonSchema;
using NSwag;


namespace SwaggerTreeStyleParser
{
    public class CodeFileBuilder
    {
        protected string? rootId { get; set; }
        protected OpenApiDocument? openApiDocument { get; set; }

        public List<ReviewLine> Build(OpenApiDocument? openApiDocument)
        {
            if (openApiDocument == null) throw new ArgumentNullException(nameof(openApiDocument));
            this.openApiDocument = openApiDocument;
            List<ReviewLine> result = new List<ReviewLine>();
            var line = new ReviewLine();

            // Add the file name as the first review line for the swagger
            var fileName = Path.GetFileName(openApiDocument.DocumentPath);
            rootId = fileName;
            var rootFileLine = new ReviewLine(id: fileName);
            rootFileLine.Tokens.AddRange(
                ReviewToken.CreateKeyValueToken(key: fileName, addPuctuation: false, keyTokenClass: "keyword", addKeyToNavigation: true)
            );

            // Add the Open API Specification version
            if (!string.IsNullOrEmpty(openApiDocument.Swagger))
            {
                rootFileLine.Children.Add(CreateKeyValueLine(nameof(openApiDocument.Swagger), openApiDocument.Swagger, keyTokenClass: "keyword"));
            }

            // Add Section
            BuildOpenApiInfoObject(openApiDocument.Info, nameof(openApiDocument.Info), rootFileLine.Children);

            if (!string.IsNullOrEmpty(openApiDocument.Host))
            {
                rootFileLine.Children.Add(CreateKeyValueLine(nameof(openApiDocument.Host), openApiDocument.Host, keyTokenClass: "keyword"));
            }

            if (openApiDocument.Schemes.Any())
            {
                rootFileLine.Children.Add(
                    CreateKeyValueLine(key: nameof(openApiDocument.Schemes), value: String.Join(", ", openApiDocument.Schemes), keyTokenClass: "keyword")
                );
            }

            if (openApiDocument.Consumes.Any())
            {
                rootFileLine.Children.Add(
                    CreateKeyValueLine(nameof(openApiDocument.Consumes), value: String.Join(", ", openApiDocument.Consumes), keyTokenClass: "keyword")
                );
            }

            if (openApiDocument.Produces.Any())
            {
                rootFileLine.Children.Add(
                    CreateKeyValueLine(nameof(openApiDocument.Produces), value: String.Join(", ", openApiDocument.Produces), keyTokenClass: "keyword")
                );
            }

            BuildOpenApiSecuritySchemeObject(openApiDocument.SecurityDefinitions, nameof(openApiDocument.SecurityDefinitions), rootFileLine.Children);
            BuildOpenApiSecurityRequirementObject(openApiDocument.Security, nameof(openApiDocument.Security), rootFileLine.Children);
            BuildOpenApiPathItemObject(openApiDocument.Paths, nameof(openApiDocument.Paths), rootFileLine.Children);
            BuildJsonSchemaDefinitions(openApiDocument.Definitions, nameof(openApiDocument.Definitions), rootFileLine.Children);
            BuildOpenApiParameterObject(openApiDocument.Parameters, nameof(openApiDocument.Parameters), rootFileLine.Children);
            result.Add(rootFileLine);
            result.Add(new ReviewLine());
            result.Add(new ReviewLine());
            return result;
        }
        public void BuildOpenApiInfoObject(OpenApiInfo? infoObject, string objectName, List<ReviewLine> reviewLines)
        {
            if (infoObject == null) return;
            var rootLine = CreateKeyValueLine(key: objectName, keyTokenClass: "header1");
            BuildValueTypeProperties(infoObject, rootLine.Children, keyTokenClass: "keyword");
            BuildOpenApiContactObject(infoObject.Contact, nameof(infoObject.Contact), rootLine.Children);
            BuildOpenApiLicenseObject(infoObject.License, nameof(infoObject.License), rootLine.Children);
            reviewLines.Add(rootLine);
        }
        public void BuildOpenApiContactObject(OpenApiContact? contactObject, string objectName, List<ReviewLine> reviewLines)
        {
            if (contactObject == null) return;
            var rootLine = CreateKeyValueLine(objectName, keyTokenClass: "header");
            BuildValueTypeProperties(contactObject, rootLine.Children, keyTokenClass: "keyword");
            reviewLines.Add(rootLine);
        }
        public void BuildOpenApiLicenseObject(OpenApiLicense? licenseObject, string objectName, List<ReviewLine> reviewLines)
        {
            if (licenseObject == null) return;
            var rootLine = CreateKeyValueLine(objectName, keyTokenClass: "header");
            BuildValueTypeProperties(licenseObject, rootLine.Children, keyTokenClass: "keyword");
            reviewLines.Add(rootLine);
        }
        public void BuildOpenApiSecuritySchemeObject(IDictionary<string, OpenApiSecurityScheme>? securityScheme, string objectName, List<ReviewLine> reviewLines)
        {
            if (securityScheme == null || securityScheme.Count == 0) return;
            var rootLine = CreateKeyValueLine(objectName, keyTokenClass: "header1");
            foreach (var kvp in securityScheme)
            {
                var line = CreateKeyValueLine(kvp.Key, keyTokenClass: "header2");
                BuildValueTypeProperties(kvp.Value, line.Children, keyTokenClass: "keyword");
                if (kvp.Value.Scopes.Any())
                {
                    var scopesLine = CreateKeyValueLine(nameof(kvp.Value.Scopes), keyTokenClass: "header");
                    BuildEnumerableTypeProperty(kvp.Value.Scopes, scopesLine.Children);
                    line.Children.Add(scopesLine);
                }
                rootLine.Children.Add(line);
            }
            reviewLines.Add(rootLine);
        }
        public void BuildOpenApiSecurityRequirementObject(ICollection<OpenApiSecurityRequirement>? securityRequirement, string objectName, List<ReviewLine> reviewLines)
        {
            if (securityRequirement == null || securityRequirement.Count() == 0) return;
            var rootLine = CreateKeyValueLine(objectName, keyTokenClass: "header1");
            foreach (var item in securityRequirement)
            {
                foreach (var kvp in item)
                {
                    rootLine.Children.Add(
                        CreateKeyValueLine(kvp.Key, value: String.Join(", ", kvp.Value), keyTokenClass: "keyword")
                    );
                }
            }
            reviewLines.Add(rootLine);
        }
        public void BuildOpenApiPathItemObject(IDictionary<string, OpenApiPathItem>? openApiPaths, string objectName, List<ReviewLine> reviewLines)
        {
            if (openApiPaths == null || openApiPaths.Count == 0) return;
            var rootLine = CreateKeyValueLine(objectName, keyTokenClass: "header1", addKeyToNavigation: true, collapseNavigation: true);
            foreach (var kvp in openApiPaths)
            {
                var line = CreateKeyValueLine(key: kvp.Key, keyTokenClass: "header2", addKeyToNavigation: true, collapseNavigation: true);
                foreach (var opKvp in kvp.Value)
                {
                    var opLine = CreateKeyValueLine(key: opKvp.Key.ToUpper(), keyTokenClass: "header", addKeyToNavigation: true, collapseNavigation: true);

                    if (opKvp.Value.Tags.Any())
                    {
                        opLine.Children.Add(
                            CreateKeyValueLine(key: nameof(opKvp.Value.Tags), value: String.Join(", ", opKvp.Value.Tags), keyTokenClass: "keyword")
                        );
                    }
                    BuildValueTypeProperties(opKvp.Value, opLine.Children, keyTokenClass: "keyword");
                    BuildOpenApiExternalDocumentation(opKvp.Value.ExternalDocumentation, opLine.Children);

                    if (opKvp.Value.Consumes != null && opKvp.Value.Consumes.Any())
                    {
                        opLine.Children.Add(
                            CreateKeyValueLine(key: nameof(opKvp.Value.Consumes), value: String.Join(", ", opKvp.Value.Consumes), keyTokenClass: "keyword")
                        );
                    }

                    if (opKvp.Value.Produces != null && opKvp.Value.Produces.Any())
                    {
                        opLine.Children.Add(
                            CreateKeyValueLine(key: nameof(opKvp.Value.Produces), value: String.Join(", ", opKvp.Value.Produces), keyTokenClass: "keyword")
                        );
                    }

                    if (opKvp.Value.Schemes != null && opKvp.Value.Schemes.Any())
                    {
                        opLine.Children.Add(
                            CreateKeyValueLine(key: nameof(opKvp.Value.Schemes), value: String.Join(", ", opKvp.Value.Schemes), keyTokenClass: "keyword")
                        );
                    }

                    if (opKvp.Value.ExtensionData != null && opKvp.Value.ExtensionData.Any())
                    {
                        BuildEnumerableTypeProperty(opKvp.Value.ExtensionData, opLine.Children);
                    }

                    BuildOpenApiParameterObject(opKvp.Value.Parameters, opLine.Children);

                    BuildOpenApiResponseObject(opKvp.Value.Responses, opLine.Children);
                    line.Children.Add(opLine);
                }
                rootLine.Children.Add(line);
            }
            reviewLines.Add(rootLine);
        }
        public void BuildJsonSchemaDefinitions(IDictionary<string, JsonSchema>? definitions, string objectName, List<ReviewLine> reviewLines)
        {
            if (definitions == null || definitions.Count == 0) return;
            var rootLine = CreateKeyValueLine(objectName, keyTokenClass: "header1", addKeyToNavigation: true, collapseNavigation: true);
            foreach (var kvp in definitions.OrderBy(d => d.Key))
            {
                var schemaProps = kvp.Value.ActualProperties;
                if (schemaProps.Count == 0) continue;
                var definitionRoot = CreateKeyValueLine(kvp.Key, keyTokenClass: "header2", addKeyToNavigation: true, collapseNavigation: true);
                definitionRoot.Children.Add(
                    CreateKeyValueLine(key: nameof(kvp.Value.Description), kvp.Value.Description, keyTokenClass: "keyword")
                );
                CreatePropertiesTableHeader(definitionRoot.Children, firstColumnName: "Field");
                
                foreach (var prop in schemaProps)
                {
                    var propRow = new ReviewLine();
                    propRow.AddToken(ReviewToken.CreateTextToken(value: prop.Key, hasSuffixSpace: false));
                    propRow.LineId = prop.Key;
                    if (prop.Value.Type != JsonObjectType.None && prop.Value.Type != JsonObjectType.Array)
                    {
                        propRow.AddToken(ReviewToken.CreateTextToken(value: prop.Value.Type.ToString(), hasSuffixSpace: false, tokenClass: "keyword"));
                    }
                    else if (prop.Value.ActualSchema != null)
                    {
                        BuildReferenceSchemaToken(schema: prop.Value.ActualSchema, propRow);
                    }
                    else 
                    {
                        propRow.AddToken(ReviewToken.CreateTextToken(value: prop.Value.Format, hasSuffixSpace: false, tokenClass: "keyword"));
                    }
                    var keywords = CollectKeywords(prop.Value);
                    propRow.AddToken(ReviewToken.CreateTextToken(value: String.Join(", ", keywords), hasSuffixSpace: false));
                    propRow.AddToken(ReviewToken.CreateTextToken(value: prop.Value.Description, hasSuffixSpace: false));
                    propRow.RenderTokensAsCells = true;
                    definitionRoot.Children.Add(propRow);
                }
                rootLine.Children.Add(definitionRoot);
            }
            reviewLines.Add(rootLine);
        }
        public void BuildOpenApiExternalDocumentation(OpenApiExternalDocumentation? openApiExternalDocumentation, List<ReviewLine> reviewLines)
        {
            if (openApiExternalDocumentation == null) return;
            var rootLine = CreateKeyValueLine(nameof(openApiExternalDocumentation));
            BuildValueTypeProperties(openApiExternalDocumentation, rootLine.Children, keyTokenClass: "keyword");
            reviewLines.Add(rootLine);
        }
        public void BuildOpenApiParameterObject(IDictionary<string, OpenApiParameter>? openApiParameters, string objectName, List<ReviewLine> reviewLines)
        {
            if (openApiParameters == null || openApiParameters.Count == 0) return;
            var rootLine = CreateKeyValueLine(objectName, keyTokenClass: "header1", addKeyToNavigation: true, collapseNavigation: true);
            CreatePropertiesTableHeader(rootLine.Children, firstColumnName: "Name", addInColumn: true);
            foreach (var kvp in openApiParameters.OrderBy(k => k.Key))
            {
                var paramRow = new ReviewLine();
                var nameToken = ReviewToken.CreateTextToken(value: kvp.Value.Name, hasSuffixSpace: false);
                nameToken.NavigationDisplayName = kvp.Value.Name;
                paramRow.AddToken(nameToken);
                paramRow.LineId = kvp.Value.Name;
                if (kvp.Value.Type != JsonObjectType.None && kvp.Value.Type != JsonObjectType.Array)
                {
                    paramRow.AddToken(ReviewToken.CreateTextToken(value: kvp.Value.Type.ToString(), hasSuffixSpace: false, tokenClass: "keyword"));
                }
                else if (kvp.Value.Schema != null)
                {
                    BuildReferenceSchemaToken(schema: kvp.Value.Schema, paramRow);
                }
                paramRow.AddToken(ReviewToken.CreateTextToken(value: kvp.Value.Kind.ToString(), hasSuffixSpace: false));
                var keywords = CollectKeywords(kvp.Value);
                paramRow.AddToken(ReviewToken.CreateTextToken(value: String.Join(", ", keywords), hasSuffixSpace: false));
                paramRow.AddToken(ReviewToken.CreateTextToken(value: kvp.Value.Description, hasSuffixSpace: false));
                paramRow.RenderTokensAsCells = true;
                rootLine.Children.Add(paramRow);
            }
            reviewLines.Add(rootLine);
        }
        public void BuildOpenApiParameterObject(IList<OpenApiParameter>? openApiParameters, List<ReviewLine> reviewLines)
        {
            if (openApiParameters == null || openApiParameters.Count == 0) return;
            foreach (var group in openApiParameters.GroupBy(x => x.ActualParameter.Kind))
            {
                var paramHeaderLine = CreateKeyValueLine(key: $"{group.Key} Parameters", keyTokenClass: "header");
                CreatePropertiesTableHeader(paramHeaderLine.Children, firstColumnName: "Name");
                
                foreach (var item in group)
                {
                    var paramRow = new ReviewLine();
                    var parameter = item.ActualParameter;
                    paramRow.AddToken(ReviewToken.CreateTextToken(value: parameter.Name, hasSuffixSpace: false));
                    paramRow.LineId = parameter.Name;

                    if (parameter.Type != JsonObjectType.None && parameter.Type != JsonObjectType.Array)
                    {
                        paramRow.AddToken(ReviewToken.CreateTextToken(value: parameter.Type.ToString(), hasSuffixSpace: false, tokenClass: "keyword"));
                    }
                    else if (parameter.Schema != null)
                    {
                        BuildReferenceSchemaToken(schema: parameter.Schema, paramRow);
                    }
                    var keywords = CollectKeywords(parameter);
                    paramRow.AddToken(ReviewToken.CreateTextToken(value: String.Join(", ", keywords), hasSuffixSpace: false));
                    paramRow.AddToken(ReviewToken.CreateTextToken(value: item.ActualParameter.Description, hasSuffixSpace: false));
                    paramRow.RenderTokensAsCells = true;
                    paramHeaderLine.Children.Add(paramRow); 
                }
                reviewLines.Add(paramHeaderLine);
            }
        }
        public void BuildOpenApiResponseObject(IDictionary<string, OpenApiResponse>? openApiResponses, List<ReviewLine> reviewLines)
        {
            if (openApiResponses == null || openApiResponses.Count() == 0) return;
            var respTitle = CreateKeyValueLine(key: $"Responses", keyTokenClass: "header");

            foreach (var kvp in openApiResponses)
            {
                var respHeader = CreateKeyValueLine(key: kvp.Key, keyTokenClass: "header");
                BuildValueTypeProperties(kvp.Value, respHeader.Children, keyTokenClass: "keyword");
                if (kvp.Value.Schema != null)
                {
                    var respModel = CreateKeyValueLine(key: "Model", addPunctuation: true,  keyTokenClass: "keyword");
                    BuildReferenceSchemaToken(kvp.Value.Schema, respModel);
                    respHeader.Children.Add(respModel);
                }
                respTitle.Children.Add(respHeader);
            }
            reviewLines.Add(respTitle);
        }
        public void BuildReferenceSchemaToken(JsonSchema? schema, ReviewLine reviewLine)
        {
            if (schema == null) return;
            string foundIn = string.Empty;
            var schemaInfo = TryGetSchemaName(schema);

            if (schemaInfo != null)
            {
                reviewLine.AddToken(ReviewToken.CreateTextToken(value: schemaInfo?.schemaName, navigateToId: schemaInfo?.id, hasSuffixSpace: false, tokenClass: "tname"));
            }
        }
        public (string? schemaName, string? id)? TryGetSchemaName(JsonSchema? schema)
        {
            if (schema == null) return null;

            string? schemaName = null;
            string? id = null;

            if (schema.Type == JsonObjectType.Array && schema.Item != null)
            {
                var result = TryGetSchemaName(schema.Item);
                if (result?.schemaName != null)
                {
                    id = result?.schemaName;
                    schemaName = $"array<{result?.schemaName}>";

                }
            }

            var defsProp = this.openApiDocument?.GetType().GetProperty("Definitions", BindingFlags.Public | BindingFlags.Instance);
            if (defsProp?.GetValue(this.openApiDocument) is IDictionary<string, JsonSchema> defs)
            {
                foreach (var kvp in defs)
                {
                    if (ReferenceEquals(kvp.Value.ActualSchema, schema.ActualSchema))
                        schemaName = id = kvp.Key.Trim();
                }
            }

            return (schemaName, id);
        }
        public ReviewLine CreateKeyValueLine(string key, string? value = null, bool addPunctuation = false, string? keyTokenClass = null, bool addKeyToNavigation = false, bool collapseNavigation = false)
        {
            var rootLine = new ReviewLine(id: key);
            if (value == null)
            {
                rootLine.AddTokenRange(ReviewToken.CreateKeyValueToken(key: key, addPuctuation: addPunctuation, keyTokenClass: keyTokenClass, addKeyToNavigation: addKeyToNavigation, collapseNavigation: collapseNavigation));
            }
            else
            {
                rootLine.AddTokenRange(ReviewToken.CreateKeyValueToken(key: key, value: value, keyTokenClass: keyTokenClass, addKeyToNavigation: addKeyToNavigation, collapseNavigation: collapseNavigation));
            }
            return rootLine;
        }
        public void CreatePropertiesTableHeader(List<ReviewLine> reviewLines, string firstColumnName, bool addInColumn = false)
        {
            var headerLine = new ReviewLine();
            headerLine.AddToken(ReviewToken.CreateTextToken(firstColumnName, hasSuffixSpace: false, tokenClass: "table-header"));
            headerLine.AddToken(ReviewToken.CreateTextToken("Type/Format", hasSuffixSpace: false, tokenClass: "table-header"));
            if (addInColumn)
            {
                headerLine.AddToken(ReviewToken.CreateTextToken("In", hasSuffixSpace: false, tokenClass: "table-header"));
            }
            headerLine.AddToken(ReviewToken.CreateTextToken("Keywords", hasSuffixSpace: false, tokenClass: "table-header"));
            headerLine.AddToken(ReviewToken.CreateTextToken("Description", hasSuffixSpace: false, tokenClass: "table-header"));
            headerLine.RenderTokensAsCells = true;
            reviewLines.Add(headerLine);
        }
        public HashSet<string> CollectKeywords<T>(T obj) where T : JsonSchema
        {
            var keywords = new HashSet<string>();
            var type = obj.GetType();
            var isRequiredProperty = type.GetProperty("IsRequired");
            if (isRequiredProperty != null && isRequiredProperty.GetValue(obj) is bool isRequired && isRequired) keywords.Add("required");
            var allowEmptyValue = type.GetProperty("AllowEmptyValue");
            if (allowEmptyValue != null && allowEmptyValue.GetValue(obj) is bool isAllowEmpty && isAllowEmpty) keywords.Add("allowEmpty");
            var collectionFormatProperty = type.GetProperty("CollectionFormat");
            if (collectionFormatProperty != null)
            {
                var value = collectionFormatProperty.GetValue(obj);
                if (value != null && value.ToString() != "Undefined")
                {
                    keywords.Add($"CollectionFormat: {value}");
                }
            }
            if (obj.Maximum != null) keywords.Add($"{nameof(obj.Maximum)}: {obj.Maximum.ToString()!}");
            var isReadOnlyProperty = type.GetProperty("IsReadOnly");
            if (isReadOnlyProperty != null && isReadOnlyProperty.GetValue(obj) is bool isReadOnly && isReadOnly) keywords.Add("readOnly");
            if (obj.MinProperties > 0) keywords.Add($"{nameof(obj.MinProperties)}: {obj.MinProperties.ToString()!}");
            if (obj.MaxProperties > 0) keywords.Add($"{nameof(obj.MaxProperties)}: {obj.MaxProperties.ToString()!}");
            if (obj.Discriminator != null) keywords.Add($"{nameof(obj.Discriminator)}: {obj.Discriminator!}");
            if (obj.ExclusiveMaximum != null) keywords.Add($"{nameof(obj.ExclusiveMaximum)}: {obj.ExclusiveMaximum.ToString()!}");
            if (obj.Minimum != null) keywords.Add($"{nameof(obj.Minimum)}: {obj.Minimum.ToString()!}");
            if (obj.ExclusiveMinimum != null) keywords.Add($"{nameof(obj.ExclusiveMinimum)}: {obj.ExclusiveMinimum.ToString()!}");
            if (obj.MaxLength != null) keywords.Add($"{nameof(obj.MaxLength)}: {obj.MaxLength.ToString()!}");
            if (obj.MinLength != null) keywords.Add($"{nameof(obj.MinLength)}: {obj.MinLength.ToString()!}");
            if (obj.Pattern != null) keywords.Add($"{nameof(obj.Pattern)}: {obj.Pattern!}");
            if (obj.MaxItems > 0) keywords.Add($"{nameof(obj.MaxItems)}: {obj.MaxItems!}");
            if (obj.UniqueItems) keywords.Add("unique");
            if (obj.Enumeration != null && obj.Enumeration.Count > 0)
            {
                keywords.Add($"Enum: [{string.Join(", ", obj.Enumeration)}]");
            }
            return keywords;
        }
        public void BuildValueTypeProperties(object obj, List<ReviewLine> reviewLines, string? keyTokenClass = null) 
        {
            if (obj == null) return;
            var objType = obj.GetType();
            ReviewLine line = new ReviewLine();
            foreach (var prop in objType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!prop.CanRead) continue;
                object? propValue;

                try
                {
                    propValue = prop.GetValue(obj);
                }
                catch
                {
                    continue;
                }
                if (propValue == null) continue;
                var propType = prop.PropertyType;
                var propName = prop.Name;

                if (IsSimpleType(propType: propType))
                {
                    reviewLines.Add(CreateKeyValueLine(key: propName, value: propValue.ToString(), keyTokenClass: keyTokenClass));
                }
                else if (propType == typeof(Uri))
                {
                    reviewLines.Add(CreateKeyValueLine(key: propName, value: ((Uri)propValue)?.AbsolutePath, keyTokenClass: keyTokenClass));
                }
            }

        }
        public void BuildEnumerableTypeProperty(IEnumerable obj, List<ReviewLine> reviewLines)
        {
            if (obj == null) return;
            var line = new ReviewLine();
            foreach (var item in obj)
            {
                if (item == null) continue;

                var itemType = item.GetType();
                if (itemType.IsGenericType && itemType.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
                {
                    dynamic kvp = item;
                    if (kvp.Key == null || kvp.Value == null) continue;
                    if (IsSimpleType(obj: kvp.Value))
                    {
                        line = CreateKeyValueLine(key: kvp.Key, value: kvp.Value.ToString(), keyTokenClass: "keyword");
                    }
                    else if (typeof(IEnumerable).IsAssignableFrom(kvp.Value.GetType()))
                    {
                        line = CreateKeyValueLine(kvp.Key, keyTokenClass: "header");
                        BuildEnumerableTypeProperty(kvp.Value, line.Children);
                    }
                    else
                    {
                        line = CreateKeyValueLine(kvp.Key, keyTokenClass: "header");
                        BuildValueTypeProperties(kvp.Value, line.Children);
                    }
                    reviewLines.Add(line);
                }
                else
                {
                    line = new ReviewLine();
                    line.AddToken(ReviewToken.CreateTextToken(value: item.ToString(), hasSuffixSpace: false));
                    reviewLines.Add(line);
                }
            }
        }
        public bool IsSimpleType(object? obj = null, Type? propType = null)
        {
            if (obj == null && propType == null) return false;
            propType ??= obj?.GetType();
            return propType == typeof(string) || propType?.IsValueType == true;
        }
    }
}
