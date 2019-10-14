﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.IO;

namespace ApiView
{
    public static class CompilationFactory
    {
        public static IAssemblySymbol GetCompilation(string file)
        {
            using (var stream = File.OpenRead(file))
            {
                return GetCompilation(stream);
            }
        }

        public static IAssemblySymbol GetCompilation(Stream stream)
        {
            PortableExecutableReference reference;

            using (var memoryStream = new MemoryStream())
            {
                stream.CopyTo(memoryStream);
                memoryStream.Position = 0;
                // MetadataReference.CreateFromStream closes the stream
                reference = MetadataReference.CreateFromStream(memoryStream);
            }
            var compilation = CSharpCompilation.Create(null).AddReferences(reference);
            var corlibLocation = typeof(object).Assembly.Location;
            var runtimeFolder = Path.GetDirectoryName(corlibLocation);

            compilation = compilation.AddReferences(MetadataReference.CreateFromFile(corlibLocation));

            var trustedAssemblies = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")).Split(Path.PathSeparator);
            foreach (var tpl in trustedAssemblies)
            {
                if (tpl.StartsWith(runtimeFolder))
                {
                    compilation = compilation.AddReferences(MetadataReference.CreateFromFile(tpl));
                }
            }

            return (IAssemblySymbol)compilation.GetAssemblyOrModuleSymbol(reference);
        }
    }
}