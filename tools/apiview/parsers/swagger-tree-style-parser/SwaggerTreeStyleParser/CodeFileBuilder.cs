using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Xml.Linq;
using ApiView;
using APIView.Model.V2;
using Namotion.Reflection;
using NSwag;


namespace SwaggerTreeStyleParser
{
    public class CodeFileBuilder
    {
        public const string CurrentVersion = "0.1";
        
        public CodeFile Build(OpenApiDocument openApiDocument, string packageName)
        {
            var line = new ReviewLine();
            var codeFile = new CodeFile()
            {
                Language = "Swagger",
                ParserVersion = CurrentVersion,
                PackageName = packageName,
                Name = packageName,
                PackageVersion = openApiDocument.Info.Version
            };

            // Add the file name as the first review line for the swagger
            var fileName = Path.GetFileName(openApiDocument.DocumentPath);
            var rootFileLine = new ReviewLine(id: Path.GetFileName(fileName));
            rootFileLine.Tokens.AddRange(ReviewToken.CreateKeyValueToken(key: fileName));

            // Add the Open API Specification version
            var specVersionPropertyName = nameof(openApiDocument.Swagger);
            var specVersionLine = new ReviewLine(id: specVersionPropertyName);
            specVersionLine.AddTokenRange(ReviewToken.CreateKeyValueToken(specVersionPropertyName, openApiDocument.Swagger));
            rootFileLine.Children.Add(specVersionLine);

            // Add Section
            BuildOpenApiInfoObject(openApiDocument.Info, nameof(openApiDocument.Info), rootFileLine.Children);
            rootFileLine.Children.Add(CreateKeyValueLine(nameof(openApiDocument.Host), openApiDocument.Host));

            line = CreateKeyValueLine(nameof(openApiDocument.Schemes));
            BuildEnumerableTypeProperty(openApiDocument.Schemes, line.Children);
            rootFileLine.Children.Add(line);

            line = CreateKeyValueLine(nameof(openApiDocument.Consumes));
            BuildEnumerableTypeProperty(openApiDocument.Consumes, line.Children);
            rootFileLine.Children.Add(line);

            line = CreateKeyValueLine(nameof(openApiDocument.Produces));
            BuildEnumerableTypeProperty(openApiDocument.Produces, line.Children);
            rootFileLine.Children.Add(line);

            BuildOpenApiSecuritySchemeObject(openApiDocument.SecurityDefinitions, nameof(openApiDocument.SecurityDefinitions), rootFileLine.Children);

            BuildOpenApiSecurityRequirementObject(openApiDocument.Security, nameof(openApiDocument.Security), rootFileLine.Children);

            BuildOpenApiPathItemObject(openApiDocument.Paths, nameof(openApiDocument.Paths), rootFileLine.Children);


            codeFile.ReviewLines.Add(rootFileLine);
            return codeFile;
        }

        private void BuildOpenApiInfoObject(OpenApiInfo infoObject, string objectName, List<ReviewLine> reviewLines)
        {
            var rootLine = CreateKeyValueLine(objectName);
            BuildValueTypeProperties(infoObject, rootLine.Children);
            BuildOpenApiContactObject(infoObject.Contact, nameof(infoObject.Contact), rootLine.Children);
            BuildOpenApiLicenseObject(infoObject.License, nameof(infoObject.License), rootLine.Children);
            reviewLines.Add(rootLine);
        }
        private void BuildOpenApiContactObject(OpenApiContact contactObject, string objectName, List<ReviewLine> reviewLines)
        {
            var rootLine = CreateKeyValueLine(objectName);
            BuildValueTypeProperties(contactObject, rootLine.Children);
            reviewLines.Add(rootLine);
        }

        private void BuildOpenApiLicenseObject(OpenApiLicense licenseObject, string objectName, List<ReviewLine> reviewLines)
        {
            var rootLine = CreateKeyValueLine(objectName);
            BuildValueTypeProperties(licenseObject, rootLine.Children);
            reviewLines.Add(rootLine);
        }

        private void BuildOpenApiSecuritySchemeObject(IDictionary<string, OpenApiSecurityScheme> securityScheme, string objectName, List<ReviewLine> reviewLines)
        {
            var rootLine = CreateKeyValueLine(objectName);
            foreach (var kvp in securityScheme)
            {
                var line = CreateKeyValueLine(kvp.Key);
                BuildValueTypeProperties(kvp.Value, line.Children);
                var scopesLine = CreateKeyValueLine(nameof(kvp.Value.Scopes));
                BuildEnumerableTypeProperty(kvp.Value.Scopes, scopesLine.Children);
                line.Children.Add(scopesLine);
                rootLine.Children.Add(line);
            }
            reviewLines.Add(rootLine);
        }

        private void BuildOpenApiSecurityRequirementObject(ICollection<OpenApiSecurityRequirement> securityRequirement, string objectName, List<ReviewLine> reviewLines)
        {
            var rootLine = CreateKeyValueLine(objectName);
            foreach (var item in securityRequirement)
            {
                foreach (var kvp in item)
                {
                    var line = CreateKeyValueLine(kvp.Key);
                    BuildEnumerableTypeProperty(kvp.Value, line.Children);
                    rootLine.Children.Add(line);
                }
            }
            reviewLines.Add(rootLine);
        }

        private void BuildOpenApiPathItemObject(IDictionary<string, OpenApiPathItem> openApiPaths, string objectName, List<ReviewLine> reviewLines)
        {
            var rootLine = CreateKeyValueLine(objectName);
            foreach (var kvp in openApiPaths)
            {
                var line = CreateKeyValueLine(key: kvp.Key, addPuctuation: false);
                foreach (var opKvp in kvp.Value)
                {
                    var opLine = CreateKeyValueLine(key: opKvp.Key, addPuctuation: false);
                    var tagsLine = CreateKeyValueLine(key: nameof(opKvp.Value.Tags));
                    BuildEnumerableTypeProperty(opKvp.Value.Tags, tagsLine.Children);
                    opLine.Children.Add(tagsLine);
                    BuildValueTypeProperties(opKvp.Value, opLine.Children);
                    BuildOpenApiParameterObject(opKvp.Value.Parameters, opLine.Children);

                    line.Children.Add(opLine);
                }
                rootLine.Children.Add(line);
            }
            reviewLines.Add(rootLine);
        }

        private void BuildOpenApiParameterObject(IList<OpenApiParameter> openApiParameters, List<ReviewLine> reviewLines)
        {
            
        }

        private ReviewLine CreateKeyValueLine(string key, string? value = null, bool addPuctuation = true)
        {
            var rootLine = new ReviewLine(id: key);
            if (value == null)
            {
                rootLine.AddTokenRange(ReviewToken.CreateKeyValueToken(key: key, addPuctuation: addPuctuation));
            }
            else
            {
                rootLine.AddTokenRange(ReviewToken.CreateKeyValueToken(key: key, value: value));
            }
            return rootLine;
        }

        private void BuildValueTypeProperties(object obj, List<ReviewLine> reviewLines) 
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

                if (propType == typeof(string) || propType.IsPrimitive || propType.IsEnum)
                {
                    reviewLines.Add(CreateKeyValueLine(key: propName, value: propValue.ToString()));
                }
                else if (propType == typeof(Uri))
                {
                    reviewLines.Add(CreateKeyValueLine(key: propName, value: ((Uri)propValue)?.AbsolutePath));
                }
            }

        }

        private void BuildEnumerableTypeProperty(IEnumerable obj, List<ReviewLine> reviewLines)
        {
            var line = new ReviewLine();
            foreach (var item in obj)
            {
                var itemType = item.GetType();
                if (itemType.IsGenericType && itemType.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
                {
                    dynamic kvp = item;
                    if (kvp.Value is string)
                    {
                        line = CreateKeyValueLine(key: kvp.Key, value: kvp.Value);
                    }
                    else if (typeof(IEnumerable).IsAssignableFrom(kvp.Value.GetType()))
                    {
                        line = CreateKeyValueLine(kvp.Key);
                        BuildEnumerableTypeProperty(kvp.Value, line.Children);
                    }
                    else
                    {
                        line = CreateKeyValueLine(kvp.Key);
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
        private void BuildProperties(object? obj, List<ReviewLine> reviewLines, string objName = null, int depth = 0, int maxDepth = 5, ISet<object>? visited = null)
        {
            if (obj == null) return;

            visited ??= new HashSet<object>(ReferenceEqualityComparer.Instance);
            if (!obj.GetType().IsValueType)
            {
                if (visited.Contains(obj)) return;
                visited.Add(obj);
            }

            if (depth > maxDepth) return;

            ReviewLine line = new ReviewLine();
            ReviewLine rootLine = null;
            List<ReviewLine> childrenLines = reviewLines;

            var objType = obj.GetType();
            if (objName != null)
            {
                rootLine = new ReviewLine(id: objName);
                rootLine.AddTokenRange(ReviewToken.CreateKeyValueToken(key: objName));
                childrenLines = rootLine.Children;
            }

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

                if (propType == typeof(string) || propType.IsPrimitive || propType.IsEnum)
                {
                    line = new ReviewLine(id: propName);
                    line.AddTokenRange(ReviewToken.CreateKeyValueToken(key: propName, value: propValue.ToString()));
                    childrenLines.Add(line);
                }
                else if (propType == typeof(Uri))
                {
                    line = new ReviewLine(id: propName);
                    line.AddTokenRange(ReviewToken.CreateKeyValueToken(key: propName, value: ((Uri)propValue)?.AbsolutePath));
                    childrenLines.Add(line);
                }
                else if (typeof(IDictionary).IsAssignableFrom(objType) || objType.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>)))
                {
                    var dictionary = (IDictionary)obj;
                    foreach (DictionaryEntry entry in dictionary)
                    {
                        var key = entry.Key.ToString();
                        var value = entry.Value;
                        BuildProperties(value, childrenLines, key!);
                    }
                }
                else if (typeof(IEnumerable).IsAssignableFrom(objType) && objType != typeof(string))
                {
                    var enumerable = (IEnumerable)obj;
                    foreach (var item in enumerable)
                    {
                        BuildProperties(item, childrenLines);
                    }
                }
                else if (!propType.IsValueType)
                {
                    BuildProperties(propValue, childrenLines, propName);
                }
            }
            if (rootLine != null)
            {
                reviewLines.Add(rootLine);
            }
            else
            {
                reviewLines.AddRange(childrenLines);
            }
        }

    }
}
