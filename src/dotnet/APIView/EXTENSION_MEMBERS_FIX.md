# C# 15 Extension Member Support in APIView

## Overview
This fix addresses the issue where C# 15 extension members were being displayed with compiler-generated implementation details instead of the clean extension syntax.

## The Problem
When C# 15 extension members are compiled, the compiler generates a complex structure with nested classes:

### Before Fix
```csharp
public static class ResponsesServerExtensions { 
    public sealed class <G>$F3F0025ADD8FA456F8E93354548ADC99 { 
        public static class <M>$C05C9E0FD38D230C68A8F70214143831 { 
            [CompilerGenerated] 
            public static void <Extension>$(ResponseItem item); 
        } 
        public static MessageResponseItem CreateAssistantMessageItem(string textContent); 
    }
}
```

### After Fix
```csharp
public static class ResponsesServerExtensions {
    extension (ResponseItem item) {
        public static MessageResponseItem CreateAssistantMessageItem(string textContent);
    }
}
```

## Implementation Details

### Compiler-Generated Structure
C# 15 extension members compile to the following structure:
1. A sealed class with a compiler-generated name starting with `<G>$` (Generic context)
2. Nested within it, a static class with name starting with `<M>$` (Method context)
3. A special method named `<Extension>$` that defines the extended type parameter
4. The actual extension methods as siblings in the `<G>$` class

### Detection Logic
The fix detects extension member containers by checking:
- Class name starts with `<G>$`
- Class is sealed
- Class has `[CompilerGenerated]` attribute
- Class contains nested types with names starting with `<M>$`

### Rendering Logic
When an extension member container is detected:
1. Extract the extended type from the `<Extension>$` method's first parameter
2. Find all actual extension methods (non-compiler-generated methods)
3. Render using `extension (Type param) { ... }` syntax
4. Exclude the container and its nested types from normal type rendering

## Files Modified
- `src/dotnet/APIView/APIView/Languages/CodeFileBuilder.cs`
  - Added `IsExtensionMemberContainer()` method
  - Added `BuildExtensionMemberBlock()` method
  - Modified `BuildType()` to handle extension members
  - Added `CompilerGeneratedAttribute` to skipped attributes

## Testing
To test this fix, you need an assembly compiled with C# 15 extension members. The easiest way is to:

1. Create a C# project using .NET 9 or later with C# 15 enabled
2. Write code using the extension member syntax:
   ```csharp
   public static class MyExtensions
   {
       extension (MyType item)
       {
           public static void DoSomething() { }
       }
   }
   ```
3. Compile the assembly
4. Upload it to APIView and verify the output shows the extension syntax

## Compatibility
This fix:
- ✅ Maintains backward compatibility with all existing code
- ✅ Passes all existing unit tests
- ✅ Only affects types with the specific compiler-generated structure
- ✅ Gracefully handles edge cases (empty extension blocks, missing metadata, etc.)
