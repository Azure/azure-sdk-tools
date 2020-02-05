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
        public INamedTypeSymbol BooleanTypeSymbol { get; }
        public INamedTypeSymbol TaskTypeSymbol { get; }
        public INamedTypeSymbol TaskOfTTypeSymbol { get; }
        public INamedTypeSymbol ValueTaskTypeSymbol { get; }
        public INamedTypeSymbol ValueTaskOfTTypeSymbol { get; }
        public INamedTypeSymbol NotifyCompletionTypeSymbol { get; }
        public INamedTypeSymbol AsyncDisposableSymbol { get; }
        public INamedTypeSymbol AsyncEnumerableOfTTypeSymbol { get; }
        public INamedTypeSymbol TaskAsyncEnumerableExtensionsSymbol { get; }

        public AsyncAnalyzerUtilities(Compilation compilation) 
        {
            BooleanTypeSymbol = compilation.GetTypeByMetadataName(typeof(bool).FullName);
            TaskTypeSymbol = compilation.GetTypeByMetadataName(typeof(Task).FullName);
            TaskOfTTypeSymbol = compilation.GetTypeByMetadataName(typeof(Task<>).FullName);
            ValueTaskTypeSymbol = compilation.GetTypeByMetadataName(typeof(ValueTask).FullName);
            ValueTaskOfTTypeSymbol = compilation.GetTypeByMetadataName(typeof(ValueTask<>).FullName);
            NotifyCompletionTypeSymbol = compilation.GetTypeByMetadataName(typeof(INotifyCompletion).FullName);
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

        public bool IsGetAwaiter(IMethodSymbol method)
        {
            if (method.Name != nameof(Task.GetAwaiter)) 
            {
                return false;
            }

            if (!(method.Parameters.Length == 0 || method.Parameters.Length == 1 && method.IsExtensionMethod)) 
            {
                return false;
            }

            if (method.ReturnsVoid || !(method.ReturnType is INamedTypeSymbol returnType)) 
            {
                return false;
            }

            if (!returnType.AllInterfaces.Contains(NotifyCompletionTypeSymbol)) 
            {
                return false;
            }

            var hasGetResult = false;
            var hasIsCompleted = false;
            foreach (var member in returnType.GetMembers()) 
            {
                hasGetResult |= IsAwaiterGetResultMethod(member);
                hasIsCompleted |= IsIsCompletedProperty(member);

                if (hasIsCompleted && hasGetResult) 
                {
                    return true;
                }
            }

            return false;
        }

        public bool IsAsyncDisposableType(ITypeSymbol type) 
            => ImplementsInterface(type as INamedTypeSymbol, AsyncDisposableSymbol);

        public bool IsAsyncEnumerableType(ITypeSymbol type) 
            => ImplementsInterface(type as INamedTypeSymbol, AsyncEnumerableOfTTypeSymbol);

        public bool IsAwaiterGetResultMethod(ISymbol symbol)
            => symbol.Name == nameof(TaskAwaiter.GetResult) &&
               IsAwaiterAccessibleMember(symbol) &&
               symbol is IMethodSymbol getResultCandidate &&
               getResultCandidate.Parameters.Length == 0;

        private bool IsIsCompletedProperty(ISymbol symbol)
            => symbol.Name == nameof(TaskAwaiter.IsCompleted) &&
               IsAwaiterAccessibleMember(symbol) &&
               symbol is IPropertySymbol property &&
               property.IsReadOnly &&
               Equals(property.GetMethod.ReturnType, BooleanTypeSymbol);

        private static bool IsAwaiterAccessibleMember(ISymbol symbol) =>
            symbol.DeclaredAccessibility switch {
                Accessibility.Public => true,
                Accessibility.ProtectedOrInternal => true,
                Accessibility.Internal => true,
                _ => false
            };

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