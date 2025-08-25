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

        /// <summary>
        /// Initializes a new instance of the <see cref="AnalyzerProviderBase"/> class.
        /// </summary>
        protected AnalyzerProviderBase()
        {
            LazyAnalyzers = new Lazy<IReadOnlyList<AgentRuleAnalyzer>>(DiscoverAnalyzers);
        }

        /// <summary>
        /// Gets the collection of analyzers provided by this provider.
        /// </summary>
        public virtual IEnumerable<AgentRuleAnalyzer> GetAnalyzers() => LazyAnalyzers.Value;

        /// <summary>
        /// Gets the assembly containing the analyzers. Override this method to specify a different assembly.
        /// </summary>
        protected virtual Assembly GetAnalyzerAssembly() => GetType().Assembly;

        /// <summary>
        /// Discovers analyzers in the assembly. Override this method to customize the analyzer discovery process.
        /// </summary>
        protected virtual IReadOnlyList<AgentRuleAnalyzer> DiscoverAnalyzers()
        {
            ArgumentNullException.ThrowIfNull(GetAnalyzerAssembly());

            var analyzers = new List<AgentRuleAnalyzer>();
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
                    if (Activator.CreateInstance(analyzerType) is AgentRuleAnalyzer instance)
                    {
                        analyzers.Add(instance);
                    }
                }
                catch (Exception ex) when (
                    ex is MemberAccessException ||
                    ex is InvalidCastException ||
                    ex is ArgumentException ||
                    ex is MissingMethodException)
                {
                    continue;
                }
            }

            return analyzers.AsReadOnly();
        }
    }
}
