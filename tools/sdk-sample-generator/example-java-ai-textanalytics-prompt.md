# Azure AI Text Analytics Java Sample

Create a comprehensive Java sample that demonstrates Azure AI Text Analytics operations using the Azure SDK for Java.

## Requirements

- Use DefaultAzureCredential or API key authentication
- Show various text analysis capabilities
- Demonstrate batch processing of documents
- Include language detection and sentiment analysis
- Show entity recognition and key phrase extraction
- Implement proper error handling and logging
- Use environment variables for configuration

## Expected Operations

1. **Authentication and Client Setup**
   - Use DefaultAzureCredential or API key
   - Get endpoint from environment variable

2. **Language Detection**
   - Detect language for single document
   - Detect language for multiple documents
   - Handle confidence scores

3. **Sentiment Analysis**
   - Analyse sentiment for documents
   - Get sentiment scores (positive, negative, neutral)
   - Extract sentiment for sentences within document
   - Handle mixed sentiment scenarios

4. **Entity Recognition**
   - Named Entity Recognition (NER)
   - Recognise entities like person, location, organisation
   - Extract entity categories and subcategories
   - Get confidence scores for entities

5. **Key Phrase Extraction**
   - Extract key phrases from documents
   - Handle single and multiple documents
   - Get relevance scores

6. **Personally Identifiable Information (PII)**
   - Detect PII entities in text
   - Redact PII from documents
   - Handle different PII categories

7. **Advanced Features**
   - Custom entity recognition
   - Opinion mining in sentiment analysis
   - Healthcare entity recognition
   - Multiple language support

8. **Batch Operations**
   - Process multiple documents efficiently
   - Handle batch size limits
   - Error handling for individual documents in batch

9. **Cleanup**
   - Proper resource disposal
   - Handle rate limiting gracefully

## Sample Data

Use realistic text scenarios:

- Customer reviews and feedback
- Social media posts
- News articles
- Business documents
- Healthcare records (for healthcare entities)
- Various languages for detection testing
