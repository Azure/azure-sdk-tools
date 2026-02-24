# TypeSpec Definitions

Source: `testData\TypeSpecProcessor\interface.tsp`

---

# UserOperations

**Type:** Interface

This is the interface which contains different types of operation definition. all supported RP operations. You should have exactly one declaration for each Azure Resource Manager service. It implements GET "/providers/{provider-namespace}/operations"

```typespec

/**
 * This is the interface which contains different types of operation definition.
 * all supported RP operations. You should have exactly one declaration for each
 * Azure Resource Manager service. It implements
 *   GET "/providers/{provider-namespace}/operations"
 *
 */
interface UserOperations {
    getUser(): User;
    createUser(user: User): User;
    get is ArmResourceRead<User>;
    op list(): User[];
```

---

## getUser

**Type:** Operation

```typespec
    getUser(): User;
```

---

## createUser

**Type:** Operation

```typespec
    createUser(user: User): User;
```

---

## get

**Type:** Operation

```typespec
    get is ArmResourceRead<User>;
```

---

## list

**Type:** Operation

```typespec
    op list(): User[];
```

---

# list

**Type:** Operation

```typespec
}
```

---
