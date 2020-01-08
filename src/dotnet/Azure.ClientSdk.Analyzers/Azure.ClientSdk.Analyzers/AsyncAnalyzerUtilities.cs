// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Azure.ClientSdk.Analyzers 
{
    internal sealed class AsyncAnalyzerUtilities 
    {
        private readonly Compilation _compilation;

        public INamedTypeSymbol TaskTypeSymbol { get; }
        public INamedTypeSymbol TaskOfTTypeSymbol { get; }
        public INamedTypeSymbol ValueTaskTypeSymbol { get; }
        public INamedTypeSymbol ValueTaskOfTTypeSymbol { get; }
        public INamedTypeSymbol AsyncDisposableSymbol { get; }
        public INamedTypeSymbol AsyncEnumerableOfTTypeSymbol { get; }
        public INamedTypeSymbol TaskAsyncEnumerableExtensionsSymbol { get; }

        public AsyncAnalyzerUtilities(Compilation compilation) 
        {
            _compilation = compilation;
            TaskTypeSymbol = compilation.GetTypeByMetadataName(typeof(Task).FullName);
            TaskOfTTypeSymbol = compilation.GetTypeByMetadataName(typeof(Task<>).FullName);
            ValueTaskTypeSymbol = compilation.GetTypeByMetadataName(typeof(ValueTask).FullName);
            ValueTaskOfTTypeSymbol = compilation.GetTypeByMetadataName(typeof(ValueTask<>).FullName);
            AsyncDisposableSymbol = compilation.GetTypeByMetadataName(typeof(IAsyncDisposable).FullName);
            AsyncEnumerableOfTTypeSymbol = compilation.GetTypeByMetadataName(typeof(IAsyncEnumerable<>).FullName);
            TaskAsyncEnumerableExtensionsSymbol = compilation.GetTypeByMetadataName(typeof(TaskAsyncEnumerableExtensions).FullName);
        }

        public bool IsConfigureAwait(SyntaxNode node) =>
            node is InvocationExpressionSyntax invocation &&
            invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
            memberAccess.Name.Identifier.Text == nameof(Task.ConfigureAwait);

        public bool IsConfigureAwait(IMethodSymbol method)
        {
            if (method.Name == nameof(Task.ConfigureAwait) && method.Parameters.Length == 1 && IsTaskType(method.ReceiverType))
            {
                return true;
            }

            if (method.Name == nameof(TaskAsyncEnumerableExtensions.ConfigureAwait) && method.Parameters.Length == 2 && Equals(method.ReceiverType, TaskAsyncEnumerableExtensionsSymbol))
            {
                return true;
            }

            return false;
        }

        public bool IsAsyncDisposableType(ITypeSymbol type)
        {
            if (type == null)
            {
                return false;
            }

            if (Equals(AsyncDisposableSymbol, type))
            {
                return true;
            }

            return type.AllInterfaces.Any(candidate => Equals(AsyncDisposableSymbol, candidate));
        }

        public bool IsAsyncEnumerableType(ITypeSymbol type)
        {
            if (type == null)
            {
                return false;
            }

            if (Equals(AsyncEnumerableOfTTypeSymbol, type.OriginalDefinition))
            {
                return true;
            }

            return type.AllInterfaces.Any(candidate => Equals(AsyncEnumerableOfTTypeSymbol, candidate.OriginalDefinition));
        }

        public bool IsTaskType(ITypeSymbol type)
        {
            if (type == null)
            {
                return false;
            }

            if (type.Equals(TaskTypeSymbol) || type.Equals(ValueTaskTypeSymbol))
            {
                return true;
            }

            var originalDefinition = type.OriginalDefinition;
            if (Equals(originalDefinition, TaskOfTTypeSymbol) || Equals(originalDefinition, ValueTaskOfTTypeSymbol))
            {
                return true;
            }

            if (type.TypeKind == TypeKind.Error)
            {
                return type.Name.Equals("Task") || type.Name.Equals("ValueTask");
            }

            return false;
        }
    }
}