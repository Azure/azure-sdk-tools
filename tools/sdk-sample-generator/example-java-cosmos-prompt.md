
# Azure Cosmos DB Java Sample

Create a comprehensive Java sample that demonstrates Azure Cosmos DB operations using the Azure SDK for Java.

## Requirements

- Use DefaultAzureCredential for authentication
- Show database and container management
- Demonstrate document CRUD operations
- Include querying with SQL API
- Show partition key usage
- Handle common scenarios like upsert and bulk operations
- Implement proper error handling and logging
- Use environment variables for configuration

## Expected Operations

1. **Authentication and Client Setup**
   - Use DefaultAzureCredential
   - Get endpoint from environment variable

2. **Database Operations**
   - Create database if not exists
   - List databases

3. **Container Operations**
   - Create container with partition key
   - Configure throughput settings

4. **Document Operations**
   - Create/insert documents
   - Read documents by ID and partition key
   - Update documents
   - Upsert operations
   - Delete documents

5. **Query Operations**
   - SQL queries with parameters
   - Cross-partition queries
   - Pagination handling

6. **Advanced Features**
   - Bulk operations
   - Transaction support
   - Change feed processing

7. **Cleanup**
   - Delete test documents
   - Clean up resources

## Sample Data

Use a simple "User" document model with fields like:
- id (string)
- userId (partition key)
- name
- email
- createdDate