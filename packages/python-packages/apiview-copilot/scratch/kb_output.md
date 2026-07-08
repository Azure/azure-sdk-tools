
> **guideline_id:** rust_introduction.html#rust-model-types-non-exhaustive<br>**score:** 58<br>
## Attribute response-only model structs with #[non_exhaustive]

DO attribute response-only model structs with #[non_exhaustive].

This forces all downstream crates, for example, to use the .. operator to match any remaining fields that may be added in the future for pattern binding:

// struct Example {
//     pub foo: Option<String>,
//     pub bar: Option<i32>,
// }

let { foo, bar, .. } = client.method().await?.try_into()?;


### GOOD Examples

```python
// struct Example {
//     pub foo: Option<String>,
//     pub bar: Option<i32>,
// }

let { foo, bar, .. } = client.method().await?.try_into()?;
```


> **guideline_id:** rust_introduction.html#rust-model-types-not-non-exhaustive<br>**score:** 58<br>
## Do not use #[non_exhaustive] on request or request-response model structs

DO NOT attribute request-only or request-response model structs with #[non_exhaustive].

This prevents downstream crates from creating types even when using the ..Default::default() expression, which means developers cannot construct models as plain data objects.

See [RFC 2008][rust-lang-rfc-2008] for more information.


> **guideline_id:** rust_implementation.html#rust-safety-debug-derive<br>**score:** 57<br>
## Derive or implement Debug if no PII is leaked; use finish_non_exhaustive to elide fields

YOU MAY derive or implement Debug on types as long as you guarantee no PII may be leaked.

To elide some fields from Debug output, you may use finish_non_exhaustive() like so:

### GOOD Examples

```python
use std::fmt;

impl fmt::Debug for MyModel {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        f.debug_struct("MyModel")
            .field("id", &self.id)
            .finish_non_exhaustive()
    }
}

```


> **guideline_id:** rust_introduction.html#rust-enums-derive-copy<br>**score:** 56<br>
## Derive Copy for all fixed enums

DO derive Copy for all fixed enums.


> **guideline_id:** rust_introduction.html#rust-enum-serialize<br>**score:** 54<br>
## Implement serde traits as appropriate for enums

YOU MAY implement serde::Deserialize, serde::Serialize, or both as appropriate depending on whether the enumeration is found only in responses, requests, or both, respectively.


> **guideline_id:** rust_introduction.html#rust-enums-serde<br>**score:** 54<br>
## Derive or implement serde traits for enums as needed

DO derive or implement serde::Serialize and/or serde::Deserialize as appropriate i.e., if the enum is used in input (serializable), output (deserializable), or both.


> **guideline_id:** rust_introduction.html#rust-enums-names<br>**score:** 53<br>
## Use PascalCase for all enum variants

DO implement all enumeration variations as PascalCase.


> **guideline_id:** rust_introduction.html#rust-enum-generated-attributes<br>**score:** 51<br>
## Define serde rename attribute for generated enum variants

YOU SHOULD define variant attribute #[serde(rename = "name")] for generated code for each variant.


> **guideline_id:** rust_introduction.html#rust-doc-samples-unwrap<br>**score:** 31<br>
## Use unwrap() or expect() in examples instead of the question mark operator

YOU SHOULD use unwrap() or expect(&str) in examples and not the [question mark operator ?][rust-lang-question-mark-operator], which requires additional setup.


> **guideline_id:** rust_introduction.html#rust-general-unwrap<br>**score:** 30<br>
## Avoid unwrap() and expect() to prevent panics

DO NOT call unwrap(), expect(), or other functions that may panic unless you are absolutely sure they never will. It's almost always better to use map(), unwrap_or_else(), or a myriad of related functions to remap errors, return suitable defaults, etc.

### GOOD Examples

```python
let value = some_option.unwrap_or(default_value); // Good: provides a default
let value = some_result.unwrap_or_else(|err| handle_error(err)); // Good: handles error gracefully

```

### BAD Examples

```python
let value = some_option.unwrap(); // Bad: may panic if None
let value = some_result.expect("Should not fail"); // Bad: may panic if Err

```


