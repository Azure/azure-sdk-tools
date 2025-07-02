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

## Analyzer Rules with Proper Context Details

The following table lists analyzer rules that provide sufficient context in their error messages to be actionable by developers:

| Rule Code | Analyzer | Scenario | Error Message | Actionability Assessment |
|-----------|----------|----------|---------------|-------------------------|
| AZC0002 | ClientMethodsAnalyzer | Service method cancellation parameters | "Client method should have an optional CancellationToken called cancellationToken (both name and it being optional matters) or a RequestContext called context as the last parameter." | **Actionable** - Clearly states the required parameter type, name, and position |
| AZC0003 | ClientMethodsAnalyzer | Service method virtuality | "DO make service methods virtual." | **Actionable** - Simple, direct instruction to add virtual keyword |
| AZC0004 | ClientMethodsAnalyzer | Async/sync method pairs | "DO provide both asynchronous and synchronous variants for all service methods." | **Actionable** - Clear requirement to provide both variants |
| AZC0005 | ClientConstructorAnalyzer, OperationConstructorAnalyzer | Protected parameterless constructor | "DO provide protected parameterless constructor for mocking." | **Actionable** - Specific instruction to add protected parameterless constructor |
| AZC0006 | ClientConstructorAnalyzer | Constructor with options parameter | "A client type should have a public constructor with equivalent parameters that takes a Azure.Core.ClientOptions-derived type as the last argument" | **Actionable** - Specifies exact constructor signature needed |
| AZC0007 | ClientConstructorAnalyzer | Minimal constructor | "A client type should have a public constructor with equivalent parameters that doesn't take a Azure.Core.ClientOptions-derived type as the last argument" | **Actionable** - Clear requirement for minimal constructor overload |
| AZC0008 | ClientOptionsAnalyzer | ServiceVersion enum requirement | "Client type should have a nested enum called ServiceVersion" | **Actionable** - Specific requirement to add ServiceVersion enum |
| AZC0009 | ClientOptionsAnalyzer | ServiceVersion constructor parameter | "ClientOptions constructors should take a ServiceVersion as their first parameter. Default constructor should be overloaded to provide ServiceVersion." | **Actionable** - Clear constructor signature requirements |
| AZC0010 | ClientOptionsAnalyzer | Default ServiceVersion value | "ClientOptions constructors should default ServiceVersion to latest supported service version" | **Actionable** - Specific guidance on default parameter value |
| AZC0011 | ClientAssemblyAttributesAnalyzer | InternalsVisibleTo restrictions | "Internal visible to product libraries effectively become public API and have to be versioned appropriately" | **Actionable** - Warns about inappropriate use with clear reasoning |
| AZC0012 | TypeNameAnalyzer | Single word type names | "Single word class names are too generic and have high chance of collision with BCL types or types from other libraries" | **Actionable** - Clear problem statement and reasoning |
| AZC0013 | TaskCompletionSourceAnalyzer | TaskCreationOptions.RunContinuationsAsynchronously | "All the task's continuations are executed synchronously unless TaskCreationOptions.RunContinuationsAsynchronously option is specified. This may cause deadlocks and other threading issues if all \"async\" continuations have to run in the thread that sets the result of a task." | **Actionable** - Explains the problem and provides specific solution |
| AZC0016 | ClientOptionsAnalyzer | ServiceVersion member naming | "All parts of ServiceVersion members' names must begin with a number or uppercase letter and cannot have consecutive underscores." | **Actionable** - Specific naming rules provided |
| AZC0017 | ClientMethodsAnalyzer | Convenience method signature validation | "Convenience methods shouldn't have parameters with the RequestContent type." | **Actionable** - Clear constraint on parameter types |
| AZC0018 | ClientMethodsAnalyzer | Protocol method signature validation | "Protocol methods should take a RequestContext parameter called `context` and not use a model type in a parameter or return type." | **Actionable** - Specific requirements for protocol methods |
| AZC0019 | ClientMethodsAnalyzer | Ambiguous method calls | "There will be an ambiguous call error when the user calls with only the required parameters. All parameters of the protocol method should be required." | **Actionable** - Explains the problem and solution |
| AZC0030 | GeneralSuffixAnalyzer, OptionsSuffixAnalyzer | Model naming suffix issues | "Model name '{0}' ends with '{1}'. {2}" | **Actionable** - Identifies the problematic suffix and provides guidance |
| AZC0031 | DefinitionSuffixAnalyzer | Definition suffix naming | "Model name '{0}' ends with '{1}'. Suggest to rename it to an appropriate name." | **Actionable** - Identifies issue and suggests solution approach |
| AZC0032 | DataSuffixAnalyzer | Data suffix naming | "Model name '{0}' ends with '{1}'. Suggest to rename it to an appropriate name." | **Actionable** - Clear identification of naming issue |
| AZC0033 | OperationSuffixAnalyzer | Operation suffix naming | "Model name '{0}' ends with '{1}'. Suggest to rename it to '{2}' or '{3}', if an appropriate name could not be found." | **Actionable** - Provides specific alternative naming suggestions |
| AZC0034 | DuplicateTypeNameAnalyzer | Type name conflicts | "Type name '{0}' conflicts with '{1}'. Consider renaming to avoid confusion." | **Actionable** - Identifies the conflict and suggests resolution |
| AZC0035 | ModelFactoryAnalyzer | Missing model factory methods | "Output model type '{0}' should have a corresponding method in a model factory class. Add a static method that returns '{0}' to a class ending with 'ModelFactory'." | **Actionable** - Specific instruction on how to create factory method |
| AZC0100 | AsyncAnalyzer | ConfigureAwait(false) requirement | "ConfigureAwait(false) must be used." | **Actionable** - Simple, direct instruction |
| AZC0101 | AsyncAnalyzer | ConfigureAwait parameter value | "Use ConfigureAwait(false) instead of ConfigureAwait(true)." | **Actionable** - Specific parameter correction |
| AZC0102 | AsyncAnalyzer | GetAwaiter().GetResult() usage | "Do not use GetAwaiter().GetResult(). Use the TaskExtensions.EnsureCompleted() extension method instead." | **Actionable** - Provides specific alternative method |
| AZC0104 | AsyncAnalyzer | EnsureCompleted usage | "Don't use {0}. Call EnsureCompleted() extension method directly on the return value of the asynchronous method that has 'bool async' parameter." | **Actionable** - Specific guidance on proper usage |
| AZC0105 | AsyncAnalyzer | Public async parameter restriction | "DO provide both asynchronous and synchronous variants for all service methods instead of one variant with 'async' parameter." | **Actionable** - Clear design requirement |
| AZC0106 | AsyncAnalyzer | Non-public async parameter requirement | "Non-public asynchronous method that is called in synchronous scope should have a boolean 'async' parameter." | **Actionable** - Specific parameter requirement |
| AZC0107 | AsyncAnalyzer | Public async method in sync scope | "Public asynchronous method shouldn't be called in synchronous scope. Use synchronous version of the method if it is available." | **Actionable** - Clear instruction to use sync version |
| AZC0150 | ModelReaderWriterAotAnalyzer | ModelReaderWriter AOT compatibility | "Use the overload of ModelReaderWriter.{0} that accepts ModelReaderWriterContext as the last parameter for AOT compatibility" | **Actionable** - Specific overload requirement for AOT |

## Analyzer Rules That Need More Context

The following table lists analyzer rules that may not provide sufficient context in their error messages alone to be immediately actionable:

| Rule Code | Analyzer | Scenario | Error Message | Context Issue |
|-----------|----------|----------|---------------|---------------|
| AZC0014 | BannedAssembliesAnalyzer | Banned assembly usage | "Types from {0} assemblies should not be exposed as part of public API surface." | **Missing specific guidance** - Doesn't explain what alternatives to use or why these assemblies are banned |
| AZC0015 | ClientMethodsAnalyzer | Unexpected return type | "Client methods should return Pageable&lt;T&gt;/AsyncPageable&lt;T&gt;/Operation&lt;T&gt;/Task&lt;Operation&lt;T&gt;&gt;/Response/Response&lt;T&gt;/Task&lt;Response&gt;/Task&lt;Response&lt;T&gt;&gt; or other client class found {0} instead." | **Too many options** - Lists many valid return types but doesn't specify which one is appropriate for the specific scenario |
| AZC0020 | BannedTypesAnalyzer | Banned internal types | "The Azure.Core internal shared source types {0} should not be used outside of the Azure.Core library." | **Missing alternatives** - Doesn't suggest what to use instead of banned types |
| AZC0103 | AsyncAnalyzer | Synchronous wait in async scope | "Do not use {0} in asynchronous scope. Use await keyword instead." | **Generic message** - Could be more specific about the async pattern to use |
| AZC0108 | AsyncAnalyzer | Incorrect async parameter value | "In {0} scope 'async' parameter for the '{1}' method call should {2}." | **Placeholder-heavy** - Message heavily relies on placeholders that may not always be clear |
| AZC0109 | AsyncAnalyzer | Misuse of async parameter | "'async' parameter in asynchronous method can't be changed and can only be used as an exclusive condition in '?:' operator or conditional statement." | **Complex constraint** - The rule is complex and may need examples |
| AZC0110 | AsyncAnalyzer | Await in possibly synchronous scope | "Asynchronous method with `async` parameter can be called from both synchronous and asynchronous scopes. 'await' keyword can be safely used either in guaranteed asynchronous scope (i.e. `if (async) {...}`) or if `async` parameter is passed into awaited method. Awaiting on variables, fields, properties, conditional operators or async methods that don't use `async` parameter isn't allowed outside of the guaranteed asynchronous scope." | **Very complex** - Long explanation that may be hard to parse and apply |
| AZC0111 | AsyncAnalyzer | EnsureCompleted in possibly async scope | "Asynchronous method with `async` parameter can be called from both synchronous and asynchronous scopes. 'EnsureCompleted' extension method can be safely used on in guaranteed synchronous scope (i.e. `if (!async) {...}`)." | **Complex scoping rules** - Requires understanding of async parameter patterns |
| AZC0112 | InternalsVisibleToAnalyzer | Internal type misuse | "{0} is defined in assembly {1} and is marked internal without a [Friend] attribute." | **Missing resolution** - Identifies the problem but doesn't explain how to fix it |

## Summary

### Well-Designed Rules (30 rules)
The majority of analyzer rules provide clear, actionable guidance with specific instructions on how to fix violations. These rules are well-designed and help developers quickly understand and resolve issues.

### Rules Needing Improvement (9 rules)
A smaller set of rules could benefit from enhanced error messages that provide:
- More specific guidance on alternatives
- Examples of correct usage patterns
- Clearer explanations of complex async patterns
- Better context about why certain restrictions exist

### Recommendations for Improvement

1. **For banned type/assembly rules**: Include suggested alternatives in error messages
2. **For complex async rules**: Consider providing code examples in documentation
3. **For return type validation**: Context-specific guidance based on method characteristics
4. **For internal type rules**: Include steps to resolve the issue in the error message

## Total Statistics

- **Total Analyzer Rules**: 39
- **Rules with Good Context**: 30 (77%)
- **Rules Needing Better Context**: 9 (23%)
- **Total Analyzer Classes**: 19
- **Coverage Areas**: Client design, async patterns, naming, type safety, AOT compatibility