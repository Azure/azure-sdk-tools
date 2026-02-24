# TypeSpec Definitions

Source: `testData\TypeSpecProcessor\blocklessNamespace.tsp`

---

# Azure.ResourceManager

**Type:** Namespace

```typespec

namespace Azure.ResourceManager;
```

---

## subNameSpace

**Type:** Namespace

```typespec

namespace subNameSpace {
    model User {
        name: string;
    }
}
```

---

### User

**Type:** Model

```typespec
    model User {
        name: string;
    }
```

---

## manager

**Type:** Model

```typespec

model manager {
    id: string;
}
```

---
