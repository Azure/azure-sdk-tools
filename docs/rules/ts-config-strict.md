# Require `compilerOptions.strict` to be `true` (ts-config-strict)

`tsconfig.json` should look as follows:
 ```json
{
    "compilerOptions": {
        "strict: true
    }
}
```

The strict flag serves two purposes: it’s a best practice for developers as it provides the best TypeScript experience, and also, strict ensures that your type definitions are maximally pedantic so strict TypeScript consumers get their best experience as well.

