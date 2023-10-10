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

namespace APIViewUnitTests
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
        public static string TestDependencyProjectName = "DependencyTestProject";

        private static readonly Dictionary<Assembly, Solution> _solutionCache = new Dictionary<Assembly, Solution>();

        public static Project Create(Assembly testAssembly, LanguageVersion languageVersion, string[] sources, string[] dependencySources = null)
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
                        .WithProjectParseOptions(projectId, new CSharpParseOptions(languageVersion));

                    if(dependencySources?.Length > 0)
                    {
                        var dependencyProjectId = ProjectId.CreateNewId(debugName: TestDependencyProjectName);
                        solution = solution.AddProject(dependencyProjectId, TestDependencyProjectName, TestDependencyProjectName, LanguageNames.CSharp)
                            .WithProjectParseOptions(dependencyProjectId, new CSharpParseOptions(languageVersion));

                        // Add the dependency project as a reference to the test project
                        solution = solution.AddProjectReference(projectId, new ProjectReference(dependencyProjectId));
                    }

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

            solution.Projects.Single(p => p.Name == TestProjectName);
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
