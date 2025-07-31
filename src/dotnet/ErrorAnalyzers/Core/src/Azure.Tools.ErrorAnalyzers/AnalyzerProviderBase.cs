// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Azure.Tools.ErrorAnalyzers
{
    /// <summary>
    /// Base class for analyzer providers that provides automatic analyzer discovery.
    /// </summary>
    public abstract class AnalyzerProviderBase : IAnalyzerProvider
    {
        private readonly Lazy<IReadOnlyList<AgentRuleAnalyzer>> LazyAnalyzers;

        protected AnalyzerProviderBase()
        {
            LazyAnalyzers = new Lazy<IReadOnlyList<AgentRuleAnalyzer>>(DiscoverAnalyzers);
        }

        public IEnumerable<AgentRuleAnalyzer> GetAnalyzers() => LazyAnalyzers.Value;

        protected virtual Assembly GetAnalyzerAssembly() => GetType().Assembly;

        private IReadOnlyList<AgentRuleAnalyzer> DiscoverAnalyzers()
        {
            List<AgentRuleAnalyzer> analyzers = new List<AgentRuleAnalyzer>();
            Assembly assembly = GetAnalyzerAssembly();

            List<Type> analyzerTypes = assembly.GetTypes()
                .Where(type => 
                    !type.IsAbstract && 
                    !type.IsInterface && 
                    type.IsSubclassOf(typeof(AgentRuleAnalyzer)) &&
                    type.GetCustomAttribute<DiscoverableAnalyzerAttribute>() != null)
                .ToList();

            foreach (Type analyzerType in analyzerTypes)
            {
                try
                {
                    AgentRuleAnalyzer instance = (AgentRuleAnalyzer)Activator.CreateInstance(analyzerType)!;
                    analyzers.Add(instance);
                }
                catch (Exception)
                {
                    continue;
                }
            }

            return analyzers.AsReadOnly();
        }
    }
}
