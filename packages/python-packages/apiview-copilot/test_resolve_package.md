# Testing the resolve-package Command

## Overview
The `apiview resolve-package` command resolves package information from a package description and language, returning the actual package name, review ID, and revision ID.

The command uses a smart multi-stage matching approach:
1. **Exact Match**: Tries to find an exact match for the package name
2. **Partial Match**: Searches for packages containing the description
3. **LLM-Powered Matching**: If no match found, retrieves all packages for the language and uses an LLM to find the best semantic match

## Usage Examples

### Basic Usage - Get Latest Revision
```bash
avc apiview resolve-package --package azure-core --language python
```

Expected Output:
```json
{
  "package_name": "azure-core",
  "review_id": "<review_id>",
  "revision_id": null,
  "language": "Python",
  "version": "<package_version>",
  "revision_label": "Latest"
}
```

Note: The `revision_id` is not directly available from the database. Use the `review_id` with `ApiViewClient` to get the actual revision.

### Get Specific Version
```bash
avc apiview resolve-package --package azure-storage-blob --language python --version 12.19.0
```

### Partial Package Name Search
```bash
avc apiview resolve-package --package storage --language python
```
This will search for any package containing "storage" in its name and return the first match (or exact match if available).

### Natural Language Descriptions (LLM-Powered)
The command can understand natural language descriptions and find the best matching package:

```bash
# Find storage blob package using description
avc apiview resolve-package --package "storage blobs" --language python

# Find cosmos DB package
avc apiview resolve-package --package "cosmos database" --language python

# Find service bus package
avc apiview resolve-package --package "message bus" --language python
```

The LLM will analyze the description and match it to the most appropriate package from all available packages in that language.

### Using Different Environments
```bash
# Production (default)
avc apiview resolve-package --package azure-core --language python

# Staging
avc apiview resolve-package --package azure-core --language python --environment staging
```

### Supported Languages
- python
- java
- typescript
- dotnet
- golang
- rust
- etc. (all languages supported by APIView)

## Return Values

The command returns a JSON object with the following fields:
- **package_name**: The actual package name from APIView
- **review_id**: The unique review identifier (use this with ApiViewClient to get revisions)
- **revision_id**: Always null (revisions must be retrieved via the APIView API)
- **language**: The normalized language name
- **version**: The package version (if available)
- **revision_label**: The label of the assumed latest revision ("Latest")

## Getting Revision Content

To get the actual revision content after resolving the package:

```python
from src._apiview import ApiViewClient
import asyncio

# Get the review_id from resolve_package
client = ApiViewClient(environment="production")
revision_text = asyncio.run(client.get_revision_text(review_id="<review_id>", label="Latest"))
```

## Error Handling

If no package is found:
```
No package found matching '<package_description>' for language '<language>'
```

If there's a database error:
```
Error resolving package: <error_message>
```

## Notes

1. Package name matching is case-insensitive
2. Partial matches are supported (searches for packages containing the description)
3. Exact matches are preferred over partial matches
4. If version is specified but not found, returns the latest revision
5. Requires proper Azure authentication and database permissions
