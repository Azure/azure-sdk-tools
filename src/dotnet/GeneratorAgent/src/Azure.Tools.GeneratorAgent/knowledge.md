# Agent Knowledge: TypeSpec Customization for Azure SDK

## Purpose
This document provides detailed guidance for fixing Azure SDK TypeSpec projects.  
The agent uses this information to correctly generate **customizations** in `client.tsp` based on `main.tsp`.

---

## 1. General Principles

- **Never edit `main.tsp` directly.**
  All customizations belong in **`client.tsp`**, placed alongside `main.tsp`.
- **Always import required dependencies:**
  ```tsp
  import "./main.tsp";
  import "@azure-tools/typespec-client-generator-core";

  using Azure.ClientGenerator.Core;
  ```
- `client.tsp` is used to **customize client hierarchy, naming, parameters, and behavior**.
- Use **augment decorators** (`@@`) to modify existing TypeSpec symbols safely.

---

## 2. Core File Structure

### `main.tsp`
Defines:
- The `@service` decorator (only one per package).
- Namespaces, operations, and models.
- The logical service API surface.

### `client.tsp`
Defines:
- `@client`, `@operationGroup`, `@clientLocation`, `@clientName`, and other decorators to customize generated client SDKs.
- Namespace-level client structuring rules.
- Client initialization, accessibility, and model usage hints.

---

## 3. Common Customizations

### Move Operations Between Clients
```tsp
@@clientLocation(Pets.pet, PetStore);
@@clientLocation(Feeds.feed, PetStore);
```

### Rename a Client
```tsp
@@clientName(PetStore, "PetStoreGreatClient");
```

### Split Operations Into Separate Clients
```tsp
@client({ name: "FoodClient", service: PetStore })
interface FoodClient {
  feed is PetStore.feed;
}

@client({ name: "PetActionClient", service: PetStore })
interface PetActionClient {
  pet is PetStore.pet;
}
```

### Add Initialization Parameters
```tsp
model StorageClientOptions {
  blobName: string;
}

@@clientInitialization(Storage, { parameters: StorageClientOptions });
```

### Adjust Initialization Scope
```tsp
@@clientInitialization(Storage, { initializedBy: InitializedBy.individually });
```

---

## 4. Advanced Customizations

### Rename Operations
```tsp
@@clientName(Get, "Read");
@@clientName(Get, "GetComputed", "python");
```

### Control Visibility
```tsp
@@access(PetStoreNamespace.GetModel, "internal");
```

### Control Method Generation
```tsp
@@convenientAPI(PetStoreNamespace.GetModel, false);
```

### Force Model Usage
```tsp
@@usage(Azure.OpenAI.ImageGenerations, Usage.input | Usage.output);
```

### Override Method Implementation
```tsp
@@override(Widget.Service.scheduleRepairs, Widget.Client.scheduleRepairs);
```

### Use External Types
```tsp
@alternateType(
  { fullyQualifiedName: "externallib.ExternalModel", package: "externallib" },
  "python"
)
model MyModel { prop: string; }
```

---

## 5. Key Rules to Avoid Syntax Errors

- Use **`@@decorator`** for augmentations (not `@`).
- Every import path and namespace must be valid.
- Always include `using Azure.ClientGenerator.Core;`.
- Each statement ends with a semicolon.
- Don’t define new services in `client.tsp`.
- Avoid redefining existing operations — use augment decorators instead.
- Ensure each block (`namespace`, `interface`, `model`) has correct `{}` closure.

---

## 7. Output Requirements

When generating fixes:
- **Always modify or create `client.tsp`.**
- **Do not delete or alter `main.tsp`.**
- The output should be valid, compilable TypeSpec.
- Use the examples above as templates.
- When in doubt, prefer using `@@decorators` over redefinition.

---

## 8. Reference

Common decorators:
- `@@client`
- `@@operationGroup`
- `@@clientLocation`
- `@@clientName`
- `@@clientNamespace`
- `@@clientInitialization`
- `@@usage`
- `@@access`
- `@@convenientAPI`
- `@@override`
- `@@alternateType`

---

## ✅ Summary for the Agent

You are an expert in **TypeSpec SDK customization** for Azure clients.  
Your goal:
1. Fix analyzer errors by applying correct customizations in `client.tsp`.
2. Follow examples and rules in this document.
3. Never modify or remove `main.tsp` definitions.
4. Always ensure syntax correctness.

---
