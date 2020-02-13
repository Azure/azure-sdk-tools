// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;

namespace Azure.ClientSdk.Analyzers 
{
    internal readonly struct MethodBodyAnalyzer 
    {
        private readonly AsyncAnalyzerUtilities _asyncUtilities;
        private readonly INamedTypeSymbol _boolType;
        private readonly INamedTypeSymbol _azureTaskExtensionsType;
        private readonly Stack<(IEnumerator<IOperation>, MethodAnalysisContext)> _symbolIteratorsStack;
        private readonly Action<Diagnostic> _reportDiagnostic;

        public MethodBodyAnalyzer(Action<Diagnostic> reportDiagnostic, Compilation compilation, AsyncAnalyzerUtilities utilities) 
        {
            _reportDiagnostic = reportDiagnostic;
            _asyncUtilities = utilities;
            
            _boolType = compilation.GetSpecialType(SpecialType.System_Boolean);
            _azureTaskExtensionsType = compilation.GetTypeByMetadataName("Azure.Core.Pipeline.TaskExtensions");
            _symbolIteratorsStack = new Stack<(IEnumerator<IOperation>, MethodAnalysisContext)>();
        }

        public void Run(IMethodSymbol method, IBlockOperation methodBody) 
        {
            var asyncParameter = GetAsyncParameter(method);
            if (IsPublicMethod(method) && asyncParameter != null) 
            {
                ReportPublicMethodWithIsAsyncDiagnostic(method);
            }

            _symbolIteratorsStack.Push((methodBody.Children.GetEnumerator(), new MethodAnalysisContext(method, asyncParameter, method.IsAsync)));

            while (_symbolIteratorsStack.Any())
            {
                var (enumerator, context) = _symbolIteratorsStack.Peek();
                if (!enumerator.MoveNext())
                {
                    _symbolIteratorsStack.Pop();
                    continue;
                }

                var current = enumerator.Current;
                if (current == null) 
                {
                    continue;
                }

                switch (current)
                {
                    case IParameterReferenceOperation reference when reference.Parameter == context.AsyncParameter:
                        AnalyzeAsyncParameterReference(context, reference);
                        continue;
                    case IAnonymousFunctionOperation function when function.Symbol != null:
                        context = context.WithNewMethod(function.Symbol, context.AsyncParameter ?? GetAsyncParameter(function.Symbol));
                        break;
                    case ILocalFunctionOperation function when function.Symbol != null:
                        context = context.WithNewMethod(function.Symbol, context.AsyncParameter ?? GetAsyncParameter(function.Symbol));
                        break;
                    case IInvocationOperation invocation:
                        AnalyzeInvocation(context, invocation);
                        break;
                }

                _symbolIteratorsStack.Push((current.Children.GetEnumerator(), context));
            }
        }
        
        private void AnalyzeAsyncParameterReference(in MethodAnalysisContext context, IOperation reference) 
        {
            switch (reference.Parent) 
            {
                case IConditionalOperation conditional:
                    _symbolIteratorsStack.Pop(); // Remove condition from stack
                    TryPushOperationToStack(context, conditional.WhenFalse, false);
                    TryPushOperationToStack(context, conditional.WhenTrue, true);
                    return;
                case IUnaryOperation unary when unary.OperatorKind == UnaryOperatorKind.Not && unary.Parent is IConditionalOperation conditional:
                    _symbolIteratorsStack.Pop();
                    _symbolIteratorsStack.Pop();
                    TryPushOperationToStack(context, conditional.WhenFalse, true);
                    TryPushOperationToStack(context, conditional.WhenTrue, false);
                    return;
                default:
                    ReportDiagnosticOnOperation(reference.Parent, Descriptors.AZC0109);
                    return;
            }
        }
        
        private void AnalyzeInvocation(in MethodAnalysisContext context, IInvocationOperation invocation) 
        {
            var targetMethod = invocation.TargetMethod;

            if (_asyncUtilities.IsConfigureAwait(targetMethod)) 
            {
                AnalyzeConfigureAwait(context, invocation);
            }
            else if (_asyncUtilities.IsGetAwaiter(targetMethod)) 
            {
                AnalyzeGetAwaiter(context, invocation);
            } 
            else if (IsEnsureCompleted(targetMethod)) 
            {
                AnalyzeEnsureCompleted(context, invocation);
            }
        }

        private void AnalyzeConfigureAwait(in MethodAnalysisContext context, IInvocationOperation operation)
        {
            // There is no reason to call ConfigureAwait in sync method
            if (!context.Method.IsAsync) 
            {
                return;
            }
            
            // ConfigureAwait is either an instance method with one parameter or a static extension method with two.
            // We need to check if the last argument is a bool and if it is 'true'
            var constantValue = operation.Arguments.Last().Value?.ConstantValue;
            if (constantValue != null && constantValue.Value.Value is bool value && value)
            {
                ReportDiagnosticOnMember(operation, Descriptors.AZC0101);
            }

            if (operation.Instance is IInvocationOperation invocation) 
            {
                // In async scope, pass 'async: true' to the async method
                AnalyzeAsyncParameterValue(invocation, true);
            }
        }
        
        private void AnalyzeGetAwaiter(in MethodAnalysisContext context, IOperation operation) 
        {
            // In async scope in async method, use await keyword instead of GetAwaiter().GetResult()
            if (context.Method.IsAsync && context.AsyncScope) 
            {
                ReportDiagnosticOnMember(operation, Descriptors.AZC0103, "GetAwaiter().GetResult()");
                return;
            }

            // Only checking for the most common GetAwaiter().GetResult() combination
            if (operation.Parent is IInvocationOperation invocation && _asyncUtilities.IsAwaiterGetResultMethod(invocation.TargetMethod)) 
            {
                ReportDiagnosticOnMember(operation, Descriptors.AZC0102);
            }
        }
        
        private void AnalyzeEnsureCompleted(in MethodAnalysisContext context, IInvocationOperation ensureCompletedInvocation) 
        {
            // TaskExtensions.EnsureCompleted is an extension method, so use its first argument to find out how it is used.
            var argumentValue = ensureCompletedInvocation.Arguments[0].Value;

            switch (argumentValue) 
            {
                case IFieldReferenceOperation fieldReference:
                    ReportDiagnosticOnOperation(fieldReference, Descriptors.AZC0104, "field");
                    return;
                case ILocalReferenceOperation localReference:
                    ReportDiagnosticOnOperation(localReference, Descriptors.AZC0104, "variable");
                    return;
                case IParameterReferenceOperation parameterReference:
                    ReportDiagnosticOnOperation(parameterReference, Descriptors.AZC0104, "parameter");
                    return;
                case IPropertyReferenceOperation propertyReference:
                    ReportDiagnosticOnOperation(propertyReference, Descriptors.AZC0104, "property");
                    return;
                case IInvocationOperation invocation:
                    if (!context.AsyncScope) 
                    {
                        // In sync scope, pass 'async: false' to the async method
                        AnalyzeAsyncParameterValue(invocation, false);
                    } 
                    else if (context.Method.IsAsync)
                    {
                        // In async scope in async method, use await keyword instead of EnsureCompleted
                        ReportDiagnosticOnMember(ensureCompletedInvocation, Descriptors.AZC0103, "EnsureCompleted()");
                    }

                    return;
            }
        }

        private void AnalyzeAsyncParameterValue(IInvocationOperation invocation, bool asyncValue) 
        {
            var asyncParameterIndex = GetAsyncParameterIndex(invocation.TargetMethod);
            if (asyncParameterIndex == -1) 
            {
                if (!asyncValue) {
                    var descriptor = IsPublicMethod(invocation.TargetMethod)
                        ? Descriptors.AZC0107
                        : Descriptors.AZC0106;
                    ReportDiagnosticOnMember(invocation, descriptor);
                }
            }
            else if (invocation.Arguments[asyncParameterIndex].Value.ConstantValue.Value is bool value && value != asyncValue) 
            {
                var messageArgs = asyncValue
                    ? new object[] { "asynchronous", invocation.TargetMethod.Name, "true" }
                    : new object[] { "synchronous", invocation.TargetMethod.Name, "false" };
                ReportDiagnosticOnOperation(invocation.Arguments[asyncParameterIndex], Descriptors.AZC0108, messageArgs);
            }
        }
        
        private IParameterSymbol GetAsyncParameter(IMethodSymbol method) 
        {
            var index = GetAsyncParameterIndex(method);
            return index == -1 ? null : method.Parameters[index];
        }

        private int GetAsyncParameterIndex(IMethodSymbol method) 
        {
            for (var i = 0; i < method.Parameters.Length; i++) 
            {
                var parameter = method.Parameters[i];
                if (parameter.Name == "async" && parameter.Type.Equals(_boolType)) 
                {
                    return i;
                }
            }

            return -1;
        }
                
        private bool IsEnsureCompleted(IMethodSymbol method) 
            => _azureTaskExtensionsType != null && method.Name == "EnsureCompleted" && method.IsExtensionMethod && method.Parameters.Length == 1 && Equals(method.ReceiverType, _azureTaskExtensionsType);
        
        private void TryPushOperationToStack(in MethodAnalysisContext context, IOperation operation, bool isAsync) 
        {
            if (operation == null) 
            {
                return;
            }

            IEnumerable<IOperation> enumerable = new[] {operation};
            _symbolIteratorsStack.Push((enumerable.GetEnumerator(), context.WithScope(isAsync)));
        }

        private void ReportPublicMethodWithIsAsyncDiagnostic(ISymbol symbol) 
        {
            var diagnostics = symbol.Locations
                .Where(location => location.IsInSource)
                .Select(location => Diagnostic.Create(Descriptors.AZC0105, location));

            foreach (var diagnostic in diagnostics)
            {
                _reportDiagnostic(diagnostic);
            }
        }

        private void ReportDiagnosticOnOperation(IOperation operation, DiagnosticDescriptor diagnosticDescriptor, params object[] messageArgs) 
        {
            var location = operation.Syntax.GetLocation();
            var diagnostic = Diagnostic.Create(diagnosticDescriptor, location, messageArgs);
            _reportDiagnostic(diagnostic);
        }

        private void ReportDiagnosticOnMember(IOperation operation, DiagnosticDescriptor diagnosticDescriptor, params object[] messageArgs) 
        {
            var invocation = (InvocationExpressionSyntax)operation.Syntax;
            var name = invocation.Expression is MemberAccessExpressionSyntax memberAccess ? memberAccess.Name : invocation.Expression;
            var start = name.Span.Start;
            var end = invocation.Span.End;
            var diagnostic = Diagnostic.Create(diagnosticDescriptor, Location.Create(invocation.SyntaxTree, new TextSpan(start, end - start)), messageArgs);
            _reportDiagnostic(diagnostic);
        }
        
        private static bool IsPublicMethod(IMethodSymbol method) 
        {
            if (method.DeclaredAccessibility != Accessibility.Public) 
            {
                return false;
            }

            if (method.AssociatedSymbol != null && method.AssociatedSymbol.DeclaredAccessibility != Accessibility.Public) 
            {
                return false;
            }

            var type = method.ContainingType;
            while (type != null) 
            {
                if (type.DeclaredAccessibility != Accessibility.Public) 
                {
                    return false;
                }

                type = type.ContainingType;
            }

            return true;
        }

        private readonly struct MethodAnalysisContext 
        {
            public IMethodSymbol Method { get; }
            public IParameterSymbol AsyncParameter { get; }
            public bool AsyncScope { get; }

            public MethodAnalysisContext(IMethodSymbol method, IParameterSymbol asyncParameter, bool asyncScope) 
            {
                Method = method;
                AsyncParameter = asyncParameter;
                AsyncScope = asyncScope;
            }

            public MethodAnalysisContext WithNewMethod(IMethodSymbol method, IParameterSymbol asyncParameter) 
                => new MethodAnalysisContext(method, AsyncParameter ?? asyncParameter, method.IsAsync);

            public MethodAnalysisContext WithScope(bool async) 
                => new MethodAnalysisContext(Method, AsyncParameter, async);
        }
    }
}