# Azure Blob Storage Java Sample

Create a comprehensive Java sample that demonstrates common Azure Blob Storage operations using the Azure Storage SDK for Java.

The sample should:

## Core Operations
- Authenticate using DefaultAzureCredential with fallback to connection string
- Create a blob container with public read access if it doesn't exist
- Upload a local file to the blob container with metadata
- Download the blob to verify successful upload
- List all blobs in the container with their properties
- Delete the uploaded blob and container for cleanup

## Technical Requirements
- Use environment variables for storage account name and connection string
- Implement proper exception handling with specific Azure Storage exceptions
- Use try-with-resources for proper resource management
- Include comprehensive logging with SLF4J
- Handle both successful and error scenarios gracefully

## Code Quality Standards
- Follow Java naming conventions (camelCase for variables, PascalCase for classes)
- Include Javadoc comments for all public methods
- Use modern Java features (var declarations, switch expressions where appropriate)
- Implement proper null checking and validation
- Structure code in logical methods with single responsibilities

## Sample Data
- Use a test file named "sample-document.txt" with content "Hello Azure Blob Storage from Java!"
- Set blob metadata: `{"author": "java-sample", "purpose": "demo", "timestamp": "current-iso-datetime"}`
- Container name should be "java-demo-container"

## Error Scenarios to Handle
- Storage account not found or inaccessible
- Invalid credentials or insufficient permissions
- File not found during upload
- Network connectivity issues
- Blob already exists (demonstrate overwrite behaviour)

## Additional Features
- Show how to set blob content type based on file extension
- Demonstrate blob tier management (Hot/Cool/Archive)
- Include blob lease operations (acquire/release)
- Show conditional operations using ETag for concurrency control

The final sample should be a complete, runnable Java class that can be executed independently and serves as a comprehensive reference for Azure Blob Storage operations in Java.