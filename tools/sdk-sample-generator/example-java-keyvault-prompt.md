# Azure Key Vault Secrets Java Sample

Create a comprehensive Java sample that demonstrates Azure Key Vault Secrets operations using the Azure SDK for Java.

## Requirements

- Use DefaultAzureCredential for authentication
- Show secret management operations
- Demonstrate secret versioning
- Include backup/restore operations
- Handle soft-delete scenarios
- Implement proper error handling and logging
- Use environment variables for configuration

## Expected Operations

1. **Authentication and Client Setup**
   - Use DefaultAzureCredential
   - Get Key Vault URL from environment variable

2. **Secret Operations**
   - Create/set secrets with metadata
   - Get secret values
   - List all secrets
   - Update secret metadata
   - Delete secrets (soft delete)

3. **Secret Versioning**
   - Create multiple versions of same secret
   - Get specific version of secret
   - List secret versions
   - Update secret to create new version

4. **Advanced Operations**
   - Backup secret
   - Restore secret from backup
   - Recover deleted secret
   - Purge deleted secret permanently

5. **Secret Properties**
   - Set expiry dates
   - Enable/disable secrets
   - Add custom tags and metadata
   - Set content type

6. **Bulk Operations**
   - List all secrets with properties
   - Batch operations for multiple secrets

7. **Cleanup**
   - Delete test secrets
   - Purge deleted secrets if needed

## Sample Data

Use meaningful secret names and values like:

- "database-connection-string"
- "api-key-service-a"
- "certificate-password"
- Custom metadata and tags
