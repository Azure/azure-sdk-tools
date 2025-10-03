# Enhanced File Search with Azure OpenAI-Powered Method Definition Detection

This enhanced version of `file_search.py` adds Azure OpenAI-powered capabilities to accurately find method definitions in large GitHub repositories, specifically designed for Azure SDK Python packages.

## 🚀 New Features

### 1. AI-Powered Method Definition Search

- **Function**: `semantic_kernel_method_search()`
- **Technology**: Azure OpenAI GPT-4.1
- **Purpose**: Analyze GitHub search results to identify actual method definitions vs. references/usages

### 2. Hybrid Search Approach  
- **Function**: `hybrid_method_search()`
- **Combines**: Traditional GitHub search + AI analysis
- **Benefit**: Higher accuracy with fallback options

### 3. Enhanced APIView Integration
- **Function**: `search_apiview_comments_with_ai()`
- **Purpose**: AI-enhanced search for APIView LineID symbols
- **Output**: Structured recommendations with confidence scores

### 4. Batch Processing
- **Function**: `batch_hybrid_search()`
- **Purpose**: Process multiple symbol searches concurrently
- **Performance**: Parallel execution with rate limiting

## 🔧 Setup Requirements

### Environment Variables

```bash
export AZURE_OPENAI_ENDPOINT="https://your-resource.openai.azure.com/"
export AZURE_OPENAI_API_KEY="your-azure-openai-api-key"
export AZURE_OPENAI_DEPLOYMENT="gpt-4.1"
export AZURE_OPENAI_API_VERSION="2024-02-15-preview"
export GITHUB_TOKEN="your-github-token"
```

### Dependencies
```bash
pip install openai requests
```

## 📊 How It Works

### Traditional Search Problems
- Returns all matches (definitions + references + usages)
- Hard to identify the actual definition file
- No context understanding

### AI-Enhanced Solution
1. **GitHub Search**: Get initial file candidates
2. **AI Analysis**: GPT-4.1 analyzes results to identify true definitions
3. **Confidence Scoring**: Provides confidence levels (0-100%)
4. **Smart Recommendations**: Combines both approaches for best results

### Definition Types Detected
- `function`: Function definitions (`def function_name`)
- `method`: Class method definitions
- `class`: Class definitions (`class ClassName`)
- `enum`: Enum value definitions
- `constant`: Constant assignments (`CONST = value`)
- `property`: Property definitions (`@property`)

## 🎯 Usage Examples

### Single Symbol Search
```python
from file_search import semantic_kernel_method_search

result = semantic_kernel_method_search(
    symbol="JSON",
    repo="Azure/azure-sdk-for-python",
    package_path="sdk/schemaregistry/azure-schemaregistry",
    line_id="azure.schemaregistry.models.SchemaContentTypeValues.JSON"
)

print(f"Best match: {result['ai_analysis']['best_match']['file_path']}")
print(f"Confidence: {result['ai_analysis']['best_match']['confidence_score']}%")
```

### Hybrid Search (Recommended)
```python
from file_search import hybrid_method_search

result = hybrid_method_search(
    symbol="decode",
    repo="Azure/azure-sdk-for-python",
    package_path="sdk/schemaregistry/azure-schemaregistry",
    use_ai=True
)

recommendation = result['recommendation']
print(f"File: {recommendation['file_path']}")
print(f"Confidence: {recommendation['confidence']}%")
print(f"Method: {recommendation['method']}")
```

### APIView Comments Integration
```python
from file_search import search_apiview_comments_with_ai

comments_data = {
    "package": "azure-schemaregistry",
    "comments": [
        {
            "LineID": "azure.schemaregistry.SchemaFormat.JSON",
            "Line": "JSON = \"Json\"",
            "Comment": "Should include PROTOBUF and APPLEPIE."
        }
    ]
}

results = search_apiview_comments_with_ai(comments_data, use_ai=True)
for result in results:
    rec = result['recommendation']
    print(f"{result['symbol']}: {rec['file_path']} ({rec['confidence']}%)")
```

### Batch Processing
```python
from file_search import batch_hybrid_search

search_requests = [
    {
        'symbol': 'JSON',
        'repo': 'Azure/azure-sdk-for-python',
        'package_path': 'sdk/schemaregistry/azure-schemaregistry',
        'line_id': 'azure.schemaregistry.models.SchemaContentTypeValues.JSON'
    },
    {
        'symbol': 'decode',
        'repo': 'Azure/azure-sdk-for-python',
        'package_path': 'sdk/schemaregistry/azure-schemaregistry',
        'line_id': 'azure.schemaregistry.JsonSchemaEncoder.decode'
    }
]

results = batch_hybrid_search(search_requests, use_ai=True, max_workers=2)
```

## 🎨 Output Format

### AI Analysis Structure
```json
{
    "best_match": {
        "file_path": "sdk/schemaregistry/azure-schemaregistry/azure/schemaregistry/models/_enums.py",
        "confidence_score": 95,
        "definition_type": "enum",
        "reasoning": "Contains the actual enum definition with JSON = 'application/json'"
    },
    "alternative_matches": [
        {
            "file_path": "sdk/schemaregistry/azure-schemaregistry/azure/schemaregistry/_client.py",
            "confidence_score": 30,
            "definition_type": "reference",
            "reasoning": "Only imports and uses the JSON constant"
        }
    ],
    "analysis_summary": "Found clear enum definition in _enums.py with high confidence"
}
```

### Recommendation Structure
```json
{
    "method": "ai_high_confidence",
    "confidence": 95,
    "file_path": "sdk/schemaregistry/azure-schemaregistry/azure/schemaregistry/models/_enums.py",
    "reasoning": "AI analysis with 95% confidence: Contains the actual enum definition"
}
```

## 🔄 Migration from Legacy Search

### Before (Traditional)
```python
from file_search import github_batch_search

queries = [{"query": "SchemaContentTypeValues.JSON", "repo": "Azure/azure-sdk-for-python"}]
results = github_batch_search(queries)
# Returns all matches - need manual analysis
```

### After (AI-Enhanced)
```python
from file_search import hybrid_method_search

result = hybrid_method_search(
    symbol="JSON",
    repo="Azure/azure-sdk-for-python",
    package_path="sdk/schemaregistry/azure-schemaregistry"
)
# Returns smart recommendation with confidence score
```

## 🧪 Testing

Run the test examples:
```bash
python test_ai_search.py
```

## 🎯 Confidence Levels

- **90-100%**: Very high confidence - likely the actual definition
- **70-89%**: High confidence - probable definition file  
- **50-69%**: Medium confidence - possible definition, verify manually
- **30-49%**: Low confidence - likely a reference or usage
- **0-29%**: Very low confidence - probably not the definition

## ⚡ Performance Notes

- **AI calls**: ~2-5 seconds per symbol (GPT-4.1)
- **GitHub API**: Rate limited (5000 requests/hour)
- **Batch processing**: Parallel execution for efficiency
- **Fallback**: Traditional search if AI fails

## 🔐 Security & Rate Limits

- OpenAI API: Standard rate limits apply
- GitHub API: 5000 requests/hour for authenticated users
- Environment variables: Never commit API keys to repositories
- Error handling: Graceful degradation if AI services unavailable

## 🐛 Troubleshooting

### Common Issues
1. **Missing API Keys**: Set `OPENAI_API_KEY` and `GITHUB_TOKEN`
2. **Rate Limits**: Increase delays between requests
3. **JSON Parse Errors**: AI response format issues (fallback implemented)
4. **Network Timeouts**: Check internet connection and API status

### Debug Mode
Enable logging for detailed debugging:
```python
import logging
logging.basicConfig(level=logging.DEBUG)
```
