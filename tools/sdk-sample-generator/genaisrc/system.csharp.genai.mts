system({
  title: "C# language support",
  description:
    "Provides basic C# language support and guidelines for code generation",
});

export default function (ctx: ChatGenerationContext) {
  const { $ } = ctx;

  $`
## C# Code Generation Guidelines

When generating C# code:
- Use proper using statements for Azure SDK packages
- Follow C# naming conventions (PascalCase for public members, camelCase for local variables)
- Use async/await patterns for asynchronous operations
- Include proper error handling with try-catch blocks
- Use the latest C# language features (nullable reference types, etc.)
- For Azure SDK code, use Azure.Identity for authentication
- Include appropriate using statements like:
  - using Azure.Identity;
  - using Azure.Storage.Blobs;
  - using Azure.Messaging.ServiceBus;
  - etc.

## Common Azure SDK Patterns

### Authentication
\`\`\`csharp
var credential = new DefaultAzureCredential();
\`\`\`

### Storage Blobs
\`\`\`csharp
var blobServiceClient = new BlobServiceClient(connectionString);
var containerClient = blobServiceClient.GetBlobContainerClient("container-name");
\`\`\`

### Key Vault
\`\`\`csharp
var client = new SecretClient(new Uri(keyVaultUrl), credential);
\`\`\`
  `;
}
