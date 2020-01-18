// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
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
            AsyncDisposableSymbol = compilation.GetTypeByMetadataName("System.IAsyncDisposable");
            AsyncEnumerableOfTTypeSymbol = compilation.GetTypeByMetadataName("System.Collections.Generic.IAsyncEnumerable`1");
            TaskAsyncEnumerableExtensionsSymbol = compilation.GetTypeByMetadataName("System.Threading.Tasks.TaskAsyncEnumerableExtensions");
        }

        public bool IsAwaitForEach(SyntaxNode node) =>
            node is ForEachStatementSyntax forEach && forEach.AwaitKeyword.Kind() == SyntaxKind.AwaitKeyword;

        public bool IsAwaitUsingStatement(SyntaxNode node) =>
            node is UsingStatementSyntax usingStatement && usingStatement.AwaitKeyword.Kind() == SyntaxKind.AwaitKeyword;

        public bool IsAwaitLocalDeclaration(SyntaxNode node) =>
            node is LocalDeclarationStatementSyntax declaration && declaration.AwaitKeyword.Kind() == SyntaxKind.AwaitKeyword;

        public bool IsConfigureAwaitInvocation(SyntaxNode node) =>
            node is InvocationExpressionSyntax invocation &&
            invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
            memberAccess.Name.Identifier.Text == nameof(Task.ConfigureAwait);

        public bool IsConfigureAwait(IMethodSymbol method)
        {
            if (method.Name != nameof(Task.ConfigureAwait)) 
            {
                return false;
            }

            if (method.Parameters.Length == 1 && IsTaskType(method.ReceiverType))
            {
                return true;
            }

            if (method.Parameters.Length == 2 && Equals(method.ReceiverType, TaskAsyncEnumerableExtensionsSymbol))
            {
                return true;
            }

            return false;
        }

        public bool IsAsyncDisposableType(ITypeSymbol type) 
            => ImplementsInterface(type as INamedTypeSymbol, AsyncDisposableSymbol);

        public bool IsAsyncEnumerableType(ITypeSymbol type) 
            => ImplementsInterface(type as INamedTypeSymbol, AsyncEnumerableOfTTypeSymbol);

        private static bool ImplementsInterface(INamedTypeSymbol type, INamedTypeSymbol candidate) 
        {
            if (type == null)
            {
                return false;
            }

            if (Equals(candidate, type.ConstructedFrom))
            {
                return true;
            }

            return type.AllInterfaces.Any(i => Equals(candidate, i.ConstructedFrom));
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

            if (type is INamedTypeSymbol namedType && namedType.IsGenericType) 
            {
                var genericType = namedType.ConstructedFrom;
                if (Equals(genericType, TaskOfTTypeSymbol) || Equals(genericType, ValueTaskOfTTypeSymbol)) 
                {
                    return true;
                }
            }

            if (type.TypeKind == TypeKind.Error)
            {
                return type.Name.Equals("Task") || type.Name.Equals("ValueTask");
            }

            return false;
        }
    }
}