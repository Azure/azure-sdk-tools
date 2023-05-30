// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Immutable;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Azure.SDK.ChangelogGen.Utilities
{
    internal class CompileHelper
    {
        private static string ProcessSource(string src)
        {
            // The compiler has bug when handling property with name 'System' with StructLayoutAttribute and EditorBrowsableAttribute defined.
            // so remove these Attribute which we dont care
            string[] lines = src.Split('\r', '\n');
            return string.Join("\n", 
                lines.Where(l => !l.Contains("[System.ComponentModel.EditorBrowsableAttribute") && !l.Contains("[System.Runtime.InteropServices.StructLayoutAttribute")));
        }

        public static Assembly Compile(string assemblyFileName, string src, List<string> refPaths)
        {
            src = ProcessSource(src);
            string netRuntimePath = System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory();
            CSharpCompilation compilation = CSharpCompilation.Create(assemblyFileName,
                new[] { CSharpSyntaxTree.ParseText(src) },
                refPaths.Select(p => MetadataReference.CreateFromFile(p)),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            using (var dllStream = new MemoryStream())
            {
                var emitResult = compilation.Emit(dllStream);
                if (!emitResult.Success)
                {
                    // When possible, try to fix the compiler error of missing reference automatically when it can be found in runtime folder.
                    List<string> missingRefs = new List<string>();
                    Regex regex = new Regex(@"'(?<name>[^\s\\\/\:\*\?\""]+?), Version=(?<version>[\d\.]+?), Culture=(?<culture>[\w]+?), PublicKeyToken=(?<publickey>[\w]+?)'");
                    foreach (var diag in emitResult.Diagnostics)
                    {
                        Match m = regex.Match(diag.GetMessage());
                        if (m.Success)
                        {
                            string assemblyName = m.Groups["name"].Value;
                            string path = Path.Combine(netRuntimePath, $"{assemblyName}.dll");
                            if (File.Exists(path))
                            {
                                missingRefs.Add(path);
                            }
                        }
                    }
                    List<string> newRefs = refPaths.Concat(missingRefs).Distinct().ToList();
                    if (newRefs.Count > refPaths.Count)
                    {
                        return Compile(assemblyFileName, src, newRefs);
                    }
                    else
                    {
                        throw new CompileErrorException(emitResult);
                    }
                }
                Assembly assembly = Assembly.Load(dllStream.GetBuffer());
                return assembly;
            }
        }
    }
}
