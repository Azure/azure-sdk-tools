# Implementation of features that vary across SDK languages

One key goal of the Azure SDK CLI project is to provide a universal interface for common tasks that can function in all of the `azure-sdk-for-<language>` repositories that we support. A consequence of this is that many tools that we develop in the CLI project will have parts which vary in implementation between languages. As an example, a Azure SDK CLI tool to format code for an SDK package might call `prettier` in the JavaScript repository, but would instead call `black` in Python, `go fmt` in Go, or something different again for each other language.

Since this pattern of per-language variations in tool behavior is common, an abstraction is available to help simplify the process and keep it consistent. When language-specific behavior is necessary, you should define a method in `LanguageService` class with a default implementation.
You can override this default implementation in language specific subclass of `LanguageService` to make language specific implementation.

## Defining language-specific service methods

1. Add a virtual method in LanguageService as default implementation.
2. Implement a version of the interface for each language that needs to support the operation
3. Register the interface as a per-language service (see example below)

## Example: Formatting code

```csharp
public abstract class LanguageService
{
    public virtual async Task FormatCode()
    {
        return Task.FromResult(PackageOperationResponse.CreateSuccess("This is not an applicable operation for this language."));
    }
}
```

### Implement for each language:

```csharp
public class JavaScriptLanguageService : LanguageService
{
    public override async Task FormatCode()
    {
        // ...run prettier
    }
}
```

```csharp
public class PythonLanguageService : LanguageService
{
    public override async Task FormatCode()
    {
        // ...run black
    }
}
```