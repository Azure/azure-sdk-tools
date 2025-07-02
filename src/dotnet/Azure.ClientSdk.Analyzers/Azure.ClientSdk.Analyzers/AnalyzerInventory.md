# .NET Analyzer Inventory

This document provides a comprehensive inventory of all .NET analyzers in the Azure SDK tools, detailing their scenarios, rule codes, error messages, and actionability assessment.

## Overview

The Azure SDK for .NET includes a comprehensive set of analyzers that enforce coding standards, best practices, and design guidelines specific to Azure SDK development. These analyzers cover areas such as:

- Client method design and async patterns
- Type naming conventions
- Constructor patterns
- Assembly attributes and visibility
- Model naming conventions
- Threading and async/await patterns
- Type usage restrictions

## Analyzer Rules with Specific Context in Error Messages

The following table lists analyzer rules where the error message text itself contains enough specific context to be immediately actionable by developers:

| Rule Code | Analyzer | Scenario | Error Message | Actionability Assessment |
|-----------|----------|----------|---------------|-------------------------|
| AZC0008 | ClientOptionsAnalyzer | ServiceVersion enum requirement | "Client type should have a nested enum called ServiceVersion" | **Actionable** - Specific requirement to add ServiceVersion enum |
| AZC0013 | TaskCompletionSourceAnalyzer | TaskCreationOptions.RunContinuationsAsynchronously | "All the task's continuations are executed synchronously unless TaskCreationOptions.RunContinuationsAsynchronously option is specified. This may cause deadlocks and other threading issues if all \"async\" continuations have to run in the thread that sets the result of a task." | **Actionable** - Explains the problem and provides specific solution |
| AZC0030 | GeneralSuffixAnalyzer, OptionsSuffixAnalyzer | Model naming suffix issues | "Model name '{0}' ends with '{1}'. {2}" | **Actionable** - Identifies the specific model name, problematic suffix, and provides guidance |
| AZC0031 | DefinitionSuffixAnalyzer | Definition suffix naming | "Model name '{0}' ends with '{1}'. Suggest to rename it to an appropriate name." | **Actionable** - Identifies specific model and suffix |
| AZC0032 | DataSuffixAnalyzer | Data suffix naming | "Model name '{0}' ends with '{1}'. Suggest to rename it to an appropriate name." | **Actionable** - Identifies specific model and suffix |
| AZC0033 | OperationSuffixAnalyzer | Operation suffix naming | "Model name '{0}' ends with '{1}'. Suggest to rename it to '{2}' or '{3}', if an appropriate name could not be found." | **Actionable** - Provides specific model name, suffix, and alternative suggestions |
| AZC0034 | DuplicateTypeNameAnalyzer | Type name conflicts | "Type name '{0}' conflicts with '{1}'. Consider renaming to avoid confusion." | **Actionable** - Identifies the specific conflicting type names |
| AZC0035 | ModelFactoryAnalyzer | Missing model factory methods | "Output model type '{0}' should have a corresponding method in a model factory class. Add a static method that returns '{0}' to a class ending with 'ModelFactory'." | **Actionable** - Identifies specific type and provides exact instructions |
| AZC0101 | AsyncAnalyzer | ConfigureAwait parameter value | "Use ConfigureAwait(false) instead of ConfigureAwait(true)." | **Actionable** - Specific parameter correction |
| AZC0102 | AsyncAnalyzer | GetAwaiter().GetResult() usage | "Do not use GetAwaiter().GetResult(). Use the TaskExtensions.EnsureCompleted() extension method instead." | **Actionable** - Provides specific alternative method |
| AZC0103 | AsyncAnalyzer | Synchronous wait in async scope | "Do not use {0} in asynchronous scope. Use await keyword instead." | **Actionable** - Identifies specific method and provides solution |
| AZC0104 | AsyncAnalyzer | EnsureCompleted usage | "Don't use {0}. Call EnsureCompleted() extension method directly on the return value of the asynchronous method that has 'bool async' parameter." | **Actionable** - Identifies specific method and provides guidance |
| AZC0108 | AsyncAnalyzer | Incorrect async parameter value | "In {0} scope 'async' parameter for the '{1}' method call should {2}." | **Actionable** - Identifies specific scope, method, and required parameter value |
| AZC0112 | InternalsVisibleToAnalyzer | Internal type misuse | "{0} is defined in assembly {1} and is marked internal without a [Friend] attribute." | **Actionable** - Identifies specific type and assembly |

## Analyzer Rules That Rely on Location Context

The following table lists analyzer rules where the error message text alone lacks sufficient context to be immediately actionable without IDE location highlighting:

| Rule Code | Analyzer | Scenario | Error Message | Context Issue |
|-----------|----------|----------|---------------|---------------|
| AZC0002 | ClientMethodsAnalyzer | Service method cancellation parameters | "Client method should have an optional CancellationToken called cancellationToken (both name and it being optional matters) or a RequestContext called context as the last parameter." | **Generic message** - Doesn't specify which method needs the parameter |
| AZC0003 | ClientMethodsAnalyzer | Service method virtuality | "DO make service methods virtual." | **Generic message** - Doesn't specify which method needs to be made virtual |
| AZC0004 | ClientMethodsAnalyzer | Async/sync method pairs | "DO provide both asynchronous and synchronous variants for all service methods." | **Generic message** - Doesn't specify which method is missing its pair |
| AZC0005 | ClientConstructorAnalyzer, OperationConstructorAnalyzer | Protected parameterless constructor | "DO provide protected parameterless constructor for mocking." | **Generic message** - Doesn't specify which class needs the constructor |
| AZC0006 | ClientConstructorAnalyzer | Constructor with options parameter | "A client type should have a public constructor with equivalent parameters that takes a Azure.Core.ClientOptions-derived type as the last argument" | **Missing type context** - Doesn't specify which client type needs the constructor |
| AZC0007 | ClientConstructorAnalyzer | Minimal constructor | "A client type should have a public constructor with equivalent parameters that doesn't take a Azure.Core.ClientOptions-derived type as the last argument" | **Missing type context** - Doesn't specify which client type needs the constructor |
| AZC0009 | ClientOptionsAnalyzer | ServiceVersion constructor parameter | "ClientOptions constructors should take a ServiceVersion as their first parameter. Default constructor should be overloaded to provide ServiceVersion." | **Missing type context** - Doesn't specify which ClientOptions type needs the constructor |
| AZC0010 | ClientOptionsAnalyzer | Default ServiceVersion value | "ClientOptions constructors should default ServiceVersion to latest supported service version" | **Missing type context** - Doesn't specify which ClientOptions type needs the default value |
| AZC0011 | ClientAssemblyAttributesAnalyzer | InternalsVisibleTo restrictions | "Internal visible to product libraries effectively become public API and have to be versioned appropriately" | **Generic warning** - Doesn't specify which specific assembly or attribute |
| AZC0012 | TypeNameAnalyzer | Single word type names | "Single word class names are too generic and have high chance of collision with BCL types or types from other libraries" | **Generic message** - Doesn't specify which specific type name is problematic |
| AZC0014 | BannedAssembliesAnalyzer | Banned assembly usage | "Types from {0} assemblies should not be exposed as part of public API surface." | **Missing alternatives** - Lists assemblies but doesn't suggest what to use instead |
| AZC0015 | ClientMethodsAnalyzer | Unexpected return type | "Client methods should return Pageable&lt;T&gt;/AsyncPageable&lt;T&gt;/Operation&lt;T&gt;/Task&lt;Operation&lt;T&gt;&gt;/Response/Response&lt;T&gt;/Task&lt;Response&gt;/Task&lt;Response&lt;T&gt;&gt; or other client class found {0} instead." | **Context-dependent** - Shows found type but doesn't indicate which return type is appropriate for the scenario |
| AZC0016 | ClientOptionsAnalyzer | ServiceVersion member naming | "All parts of ServiceVersion members' names must begin with a number or uppercase letter and cannot have consecutive underscores." | **Missing type context** - Doesn't specify which type's ServiceVersion members are problematic |
| AZC0017 | ClientMethodsAnalyzer | Convenience method signature validation | "Convenience methods shouldn't have parameters with the RequestContent type." | **Missing method context** - Doesn't specify which method and type have the problematic parameter |
| AZC0018 | ClientMethodsAnalyzer | Protocol method signature validation | "Protocol methods should take a RequestContext parameter called `context` and not use a model type in a parameter or return type." | **Missing method context** - Doesn't specify which method and type have the problematic signature |
| AZC0019 | ClientMethodsAnalyzer | Ambiguous method calls | "There will be an ambiguous call error when the user calls with only the required parameters. All parameters of the protocol method should be required." | **Missing method context** - Doesn't specify which method and type have the ambiguity issue |
| AZC0020 | BannedTypesAnalyzer | Banned internal types | "The Azure.Core internal shared source types {0} should not be used outside of the Azure.Core library." | **Missing alternatives** - Lists banned types but doesn't suggest what to use instead |
| AZC0100 | AsyncAnalyzer | ConfigureAwait(false) requirement | "ConfigureAwait(false) must be used." | **Generic message** - Doesn't specify which specific await call needs ConfigureAwait |
| AZC0105 | AsyncAnalyzer | Public async parameter restriction | "DO provide both asynchronous and synchronous variants for all service methods instead of one variant with 'async' parameter." | **Generic message** - Doesn't specify which method has the problematic async parameter |
| AZC0106 | AsyncAnalyzer | Non-public async parameter requirement | "Non-public asynchronous method that is called in synchronous scope should have a boolean 'async' parameter." | **Generic message** - Doesn't specify which method needs the parameter |
| AZC0107 | AsyncAnalyzer | Public async method in sync scope | "Public asynchronous method shouldn't be called in synchronous scope. Use synchronous version of the method if it is available." | **Generic message** - Doesn't specify which method call is problematic |
| AZC0109 | AsyncAnalyzer | Misuse of async parameter | "'async' parameter in asynchronous method can't be changed and can only be used as an exclusive condition in '?:' operator or conditional statement." | **Complex constraint** - Complex rule that needs examples and doesn't specify which usage is incorrect |
| AZC0110 | AsyncAnalyzer | Await in possibly synchronous scope | "Asynchronous method with `async` parameter can be called from both synchronous and asynchronous scopes. 'await' keyword can be safely used either in guaranteed asynchronous scope (i.e. `if (async) {...}`) or if `async` parameter is passed into awaited method. Awaiting on variables, fields, properties, conditional operators or async methods that don't use `async` parameter isn't allowed outside of the guaranteed asynchronous scope." | **Very complex** - Extremely long explanation that's difficult to parse and doesn't specify which await is problematic |
| AZC0111 | AsyncAnalyzer | EnsureCompleted in possibly async scope | "Asynchronous method with `async` parameter can be called from both synchronous and asynchronous scopes. 'EnsureCompleted' extension method can be safely used on in guaranteed synchronous scope (i.e. `if (!async) {...}`)." | **Complex scoping rules** - Complex rule requiring deep understanding of async patterns |
| AZC0150 | ModelReaderWriterAotAnalyzer | ModelReaderWriter AOT compatibility | "Use the overload of ModelReaderWriter.{0} that accepts ModelReaderWriterContext as the last parameter for AOT compatibility" | **Missing type context** - Identifies method via {0} but doesn't specify which type is making the call |

## Summary

### Messages with Specific Context (13 rules)
Analyzer rules that include specific context information (like type names, method names, or parameter details) in the error message text itself, making them immediately actionable without requiring IDE location highlighting.

### Messages Relying on Location Context (26 rules)
Analyzer rules with generic error messages that depend on IDE location highlighting to identify which specific code element needs attention. While these rules provide clear guidance on what needs to be done, the error message text alone lacks specificity.

### Key Insight on Actionability
True actionability requires that the error message text itself contains enough context to understand:
- **What** needs to be fixed (specific method, type, parameter, etc.)
- **How** to fix it (specific instructions or alternatives)
- **Why** it's a problem (when relevant)

Rules that rely solely on IDE location highlighting, while useful in development environments, are less actionable in contexts like build logs or command-line output where location information might not be immediately visible.

### Recommendations for Improvement

#### For Missing Type/Method Context
1. **AZC0006/AZC0007**: Include specific client type name in error messages when there are multiple clients in a library package
2. **AZC0009/AZC0010**: Include specific ClientOptions type name in error messages when there are multiple ClientOptions in a library package
3. **AZC0016**: Include specific type name when reporting ServiceVersion member naming violations
4. **AZC0017/AZC0018/AZC0019**: Include both method name/signature and containing type name in error messages
5. **AZC0150**: Include specific type name in addition to method name for ModelReaderWriter call violations

#### For Missing Alternatives
1. **AZC0014/AZC0020**: Include suggested alternative types/assemblies in error messages
2. **AZC0015**: Provide context-specific return type recommendations based on method characteristics
3. **AZC0112**: Include resolution steps (e.g., "Add [Friend] attribute or make type public")

#### For Generic Messages
1. **Include specific identifiers**: Add method names, type names, or parameter names to error messages for rules like AZC0002, AZC0003, AZC0004, AZC0005, etc.
2. **Provide context-aware guidance**: Tailor suggestions based on the specific violation
3. **Examples in complex rules**: For rules like AZC0110, consider providing code examples

## Total Statistics

- **Total Analyzer Rules**: 39
- **Rules with Specific Context**: 13 (33%)
- **Rules Relying on Location Context**: 26 (67%)
- **Total Analyzer Classes**: 19
- **Coverage Areas**: Client design, async patterns, naming, type safety, AOT compatibility