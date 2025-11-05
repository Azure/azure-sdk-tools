using System.Reflection;
using ApiView;
using APIView.Model.V2;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Reader;

namespace SwaggerTreeStyleParser
{
    public class CodeFileBuilder
    {
        public const string CurrentVersion = "0.1";
        public CodeFile Build(OpenApiDocument openApiDocument, OpenApiDiagnostic openApiDiagnostic, string packageName)
        {
            var codeFile = new CodeFile()
            {
                Language = "Swagger",
                ParserVersion = CurrentVersion,
                PackageName = packageName,
                Name = packageName,
                PackageVersion = openApiDocument.Info.Version
            };

            // Add the file name as the first review line for the swagger
            var fileName = Path.GetFileName(openApiDocument.BaseUri.AbsolutePath);
            var rootFileLine = new ReviewLine(id: Path.GetFileName(fileName));
            rootFileLine.Tokens.AddRange(ReviewToken.CreateKeyValueToken(key: fileName));

            // Add the Open API Specification version
            var specVersionPropertyName = nameof(openApiDiagnostic.SpecificationVersion);
            var specVersionLine = new ReviewLine(id: specVersionPropertyName);
            specVersionLine.AddTokenRange(ReviewToken.CreateKeyValueToken(specVersionPropertyName, openApiDiagnostic.SpecificationVersion.ToString()));
            rootFileLine.Children.Add(specVersionLine);

            // Add the base URI
            var baseUriPropertyName = nameof(openApiDocument.BaseUri);
            var baseUriLine = new ReviewLine(id: baseUriPropertyName);
            baseUriLine.AddTokenRange(ReviewToken.CreateKeyValueToken(baseUriPropertyName, openApiDocument.BaseUri.ToString()));
            rootFileLine.Children.Add(baseUriLine);

            // Add info Section
            BuildProperties(openApiDocument.Info, rootFileLine.Children);

            codeFile.ReviewLines.Add(rootFileLine);
            return codeFile;
        }

        private void BuildProperties(object? obj, List<ReviewLine> reviewLines)
        {
            if (obj == null) return;

            ReviewLine line = new ReviewLine();

            var objName = nameof(obj);
            var rootLine = new ReviewLine(id: objName);
            rootLine.AddTokenRange(ReviewToken.CreateKeyValueToken(key: objName));
            

            foreach (var prop in obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!prop.CanRead) continue;

                var propValue = prop.GetValue(obj);
                if (propValue == null) continue;
                var propType = prop.PropertyType;

                if (propType == typeof(string) || propType.IsPrimitive || propType.IsEnum)
                {
                    line = new ReviewLine(id: prop.Name);
                    line.AddTokenRange(ReviewToken.CreateKeyValueToken(key: prop.Name, value: propValue.ToString()));
                    rootLine.Children.Add(line);
                }
                else if (propType == typeof(Uri))
                {
                    line = new ReviewLine(id: prop.Name);
                    line.AddTokenRange(ReviewToken.CreateKeyValueToken(key: prop.Name, value: ((Uri)propValue)?.AbsolutePath));
                    rootLine.Children.Add(line);
                }
                else 
                {
                    BuildProperties(propValue, rootLine.Children);
                }
            }
            reviewLines.Add(rootLine);
        }
    }
}
