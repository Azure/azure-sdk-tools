# Implementation of features that vary across SDK languages

One key goal of the Azure SDK CLI project is to provide a universal interface for common tasks that can function in all of the `azure-sdk-for-<language>` repositories that we support. A consequence of this is that many tools that we develop in the CLI project will have parts which vary in implementation between languages. As an example, a Azure SDK CLI tool to format code for an SDK package might call `prettier` in the JavaScript repository, but would instead call `black` in Python, `go fmt` in Go, or something different again for each other language.

Since this pattern of per-language variations in tool behavior is common, an abstraction is available to help simplify the process and keep it consistent. When language-specific behavior is necessary, you should define an interface for that behavior to be implemented for each SDK language.

## Defining language-specific services

1. Create your interface for language-specific operations
2. Implement a version of the interface for each language that needs to support the operation
3. Register the interface as a per-language service (see example below)

## Example: Formatting code

```csharp
interface IFormatter
{
    Task FormatCode();
}
```

### Implement for each language:

```csharp
public class JavaScriptFormatter(/* add service dependencies to constructor here */) : IFormatter
{
    public async Task FormatCode()
    {
        // ...run prettier
    }
}
```

```csharp
public class PythonFormatter(/* add service dependencies to constructor here */) : IFormatter
{
    public async Task FormatCode()
    {
        // ...run black
    }
}
```

### Register the services in `ServiceRegistrations.cs`:

```csharp
services.AddLanguageSpecific<IFormatter>(new LanguageSpecificImplementations
{
    JavaScript = typeof(JavaScriptFormatter),
    Python = typeof(PythonFormatter),
    // ...and so on for the other languages
});
```

### Consume the language-specific service by taking a dependency on `ILanguageSpecificResolver<T>`:

```csharp
public class MyService(ILanguageSpecificResolver<IFormatter> formatterResolver)
{
    public async Task RunFormat(string packageDirectory)
    {
        var formatter = await formatterResolver.Resolve(packageDirectory);

        // The resolver may return null if no appropriate service can be resolved for that path.
        // This may happen if the package path is outside of the SDK repository, or if the language
        // you are working in does not have an implementation of the interface.
        if(formatter == null)
        {
            // (insert error handling logic)
        }

        await formatter.FormatCode();
    }
}
```
