# Azure SDK Java Sample Generation

### AI Text Analytics

```sh
npx genaiscript run generate --apply-edits --vars \
  client-api=.artifacts/azure-sdk-for-java/sdk/textanalytics/azure-ai-textanalytics/README.md \
  language=Java \
  user-prompt=example-java-ai-textanalytics-prompt.md \
  out=.artifacts/generated-samples
```

### Key Vault

```sh
npx genaiscript run generate --apply-edits --vars \
  client-api=.artifacts/azure-sdk-for-java/sdk/keyvault/azure-security-keyvault-secrets/README.md \
  language=Java \
  user-prompt=example-java-keyvault-prompt.md \
  out=.artifacts/generated-samples
```

### Blob Storage

```sh
npx genaiscript run generate --apply-edits --vars \
  client-api=.artifacts/azure-sdk-for-java/sdk/storage/azure-storage-blob/README.md \
  language=Java \
  user-prompt=example-java-blob-prompt.md \
  out=.artifacts/generated-samples
```

### Cosmos DB

```sh
npx genaiscript run generate --apply-edits --vars \
  client-api=.artifacts/azure-sdk-for-java/sdk/cosmos/azure-cosmos/README.md \
  language=Java \
  user-prompt=example-java-cosmos-prompt.md \
  out=.artifacts/generated-samples
```

### Event Hubs

```sh
npx genaiscript run generate --apply-edits --vars \
  client-api=.artifacts/azure-sdk-for-java/sdk/eventhubs/azure-messaging-eventhubs/README.md \
  language=Java \
  user-prompt=example-java-eventhubs-prompt.md \
  out=.artifacts/generated-samples
```

### Service Bus

```sh
npx genaiscript run generate --apply-edits --vars \
  client-api=.artifacts/azure-sdk-for-java/sdk/servicebus/azure-messaging-servicebus/README.md \
  language=Java \
  user-prompt=example-java-servicebus-prompt.md \
  out=.artifacts/generated-samples
```
