# StructuredApiChangeContext Summary

## Real Test Data Example

From `apiview-diff.json` processing in our test suite:

```
Prepared structured context: 15 total changes, 14 method changes, 10 parameter changes, 3 type changes
```

## Key Structure Properties

```csharp
public class StructuredApiChangeContext
{
    public List<ApiChange> Changes { get; set; }           // All 15 changes
    public Dictionary<string, List<ApiChange>> ChangesByKind { get; set; }
    public List<ApiChange> MethodChanges { get; set; }     // 14 changes
    public List<ApiChange> ParameterChanges { get; set; }  // 10 changes  
    public List<ApiChange> TypeChanges { get; set; }       // 3 changes
}
```

## Categorization Logic

**MethodChanges (14)**: Any change with Symbol/Kind/Metadata containing "Method"
- AddedMethod, ModifiedMethodParameterNames, ModifiedMethodReturnType, RemovedMethod, AddedOverload, RemovedOverload

**ParameterChanges (10)**: Any change with Kind containing "Parameter" or Metadata with parameter info
- All ModifiedMethodParameterNames (4) + changes affecting method parameters (6 more)

**TypeChanges (3)**: Any change with Kind containing "Type/Class" or className metadata  
- AddedClass, ModifiedMethodReturnType changes

## Critical Change Examples

**Parameter Rename (High Impact)**:
```
beginAnalyzeDocument: analyzeDocumentOptions → analyzeDocumentRequest
```
💥 **Breaks**: `getParameterByName("analyzeDocumentOptions")` calls in customization

**Method Removal (High Impact)**:
```
Removed: listAnalyzeBatchResults(String, RequestOptions)
```
💥 **Breaks**: Direct method calls in customization code

**Return Type Change (Medium Impact)**:
```
listAnalyzeBatchResults: PagedFlux → Mono
```
⚠️ **May Break**: Type casting or method chaining in customization

## LLM Analysis Usage

The structured context enables targeted analysis:
- **Critical Changes**: Focus on parameter/method changes (most likely to break customization)
- **Reference Changes**: Show additions for context but don't suggest fixes
- **Dependency Tracking**: Follow chains like TSP changes → Generated code → Customization impact

This ensures LLM suggestions are **constrained to customization code only** ✅