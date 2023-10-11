// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.IO;

namespace ApiView
{
    public static class CompilationFactory
    {
        private static HashSet<string> AllowedAssemblies = new HashSet<string>(new[]
        {
            "Microsoft.Bcl.AsyncInterfaces"
        }, StringComparer.InvariantCultureIgnoreCase);

        public static IAssemblySymbol GetCompilation(string file)
        {
            using (var stream = File.OpenRead(file))
            {
                return GetCompilation(stream, null);
            }
        }

        public static IAssemblySymbol GetCompilation(Stream stream, Stream documentationStream)
        {
            PortableExecutableReference reference;

            using (var memoryStream = new MemoryStream())
            {
                stream.CopyTo(memoryStream);
                memoryStream.Position = 0;

                DocumentationProvider documentation = null;
                if (documentationStream != null)
                {
                    using var docMemoryStream = new MemoryStream();
                    documentationStream.CopyTo(docMemoryStream);
                    docMemoryStream.Position = 0;
                    documentation = XmlDocumentationProvider.CreateFromBytes(docMemoryStream.ToArray());
                }
                // MetadataReference.CreateFromStream closes the stream
                reference = MetadataReference.CreateFromStream(memoryStream, documentation: documentation);
            }
            var compilation = CSharpCompilation.Create(null, options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, metadataImportOptions: MetadataImportOptions.Internal)).AddReferences(reference);
            var corlibLocation = typeof(object).Assembly.Location;

            var runtimeFolder = Path.GetDirectoryName(corlibLocation);

            compilation = compilation.AddReferences(MetadataReference.CreateFromFile(corlibLocation));

            var trustedAssemblies = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")).Split(Path.PathSeparator);
            foreach (var tpl in trustedAssemblies)
            {
                if (tpl.StartsWith(runtimeFolder) || AllowedAssemblies.Contains(Path.GetFileNameWithoutExtension(tpl)))
                {
                    compilation = compilation.AddReferences(MetadataReference.CreateFromFile(tpl));
                }
            }

            return (IAssemblySymbol)compilation.GetAssemblyOrModuleSymbol(reference);
        }
    }
}
