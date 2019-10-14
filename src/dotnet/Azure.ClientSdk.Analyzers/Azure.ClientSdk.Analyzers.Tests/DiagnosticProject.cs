// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.DependencyModel;
using Microsoft.Extensions.DependencyModel.Resolution;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Azure.ClientSdk.Analyzers.Tests
{
    public class DiagnosticProject
    {
        /// <summary>
        /// File name prefix used to generate Documents instances from source.
        /// </summary>
        public static string DefaultFilePathPrefix = "Test";

        /// <summary>
        /// Project name.
        /// </summary>
        public static string TestProjectName = "TestProject";

        private static readonly Dictionary<Assembly, Solution> _solutionCache = new Dictionary<Assembly, Solution>();

        public static Project Create(Assembly testAssembly, string[] sources)
        {
            Solution solution;
            lock (_solutionCache)
            {
                if (!_solutionCache.TryGetValue(testAssembly, out solution))
                {
                    var projectId = ProjectId.CreateNewId(debugName: TestProjectName);
                    solution = new AdhocWorkspace()
                        .CurrentSolution
                        .AddProject(projectId, TestProjectName, TestProjectName, LanguageNames.CSharp)
                        .WithProjectParseOptions(projectId, new CSharpParseOptions(LanguageVersion.Latest));

                    foreach (var defaultCompileLibrary in DependencyContext.Load(testAssembly).CompileLibraries)
                    {
                        foreach (var resolveReferencePath in defaultCompileLibrary.ResolveReferencePaths(new AppLocalResolver()))
                        {
                            solution = solution.AddMetadataReference(projectId, MetadataReference.CreateFromFile(resolveReferencePath));
                        }
                    }

                    _solutionCache.Add(testAssembly, solution);
                }
            }

            var testProject = solution.ProjectIds.Single();
            var fileNamePrefix = DefaultFilePathPrefix;

            for (var i = 0; i < sources.Length; i++)
            {
                var newFileName = fileNamePrefix;
                if (sources.Length > 1)
                {
                    newFileName += i;
                }
                newFileName += ".cs";

                var documentId = DocumentId.CreateNewId(testProject, debugName: newFileName);
                solution = solution.AddDocument(documentId, newFileName, SourceText.From(sources[i]));
            }

            return solution.GetProject(testProject);
        }

        // Required to resolve compilation assemblies inside unit tests
        private class AppLocalResolver : ICompilationAssemblyResolver
        {
            public bool TryResolveAssemblyPaths(CompilationLibrary library, List<string> assemblies)
            {
                foreach (var assembly in library.Assemblies)
                {
                    var dll = Path.Combine(Directory.GetCurrentDirectory(), "refs", Path.GetFileName(assembly));
                    if (File.Exists(dll))
                    {
                        assemblies.Add(dll);
                        return true;
                    }

                    dll = Path.Combine(Directory.GetCurrentDirectory(), Path.GetFileName(assembly));
                    if (File.Exists(dll))
                    {
                        assemblies.Add(dll);
                        return true;
                    }
                }

                return false;
            }
        }
    }
}
