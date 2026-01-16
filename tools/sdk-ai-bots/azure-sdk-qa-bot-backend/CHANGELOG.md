# Release History

## 0.7.0 (unreleased)

### Features Added

* Add new tenants General, API Review, JS, Java and .Net
* Add new knowledge sources from JS, Java and .Net
* Support routing tenant based on question domain

## Other changes

* Refine prompt for onboarding channel to improve answer accuracy
* Refactor post process of merging and sorting and complete chunk logic, to give more better context to LLM

## 0.6.0 (2025-12-16)

### Features Added

* Support access and analyze pipeline logs via azsdk-cli
* Enhance HTML content preprocessing to handle various encoding scenarios

## 0.5.0 (2025-12-10)

### Features Added

* Filter out invalid reference links in the response

## 0.4.0 (2025-12-09)

### Bugs Fixed

* Fix context completion logic for TypeSpec to Swagger Mapping source chunks

### Features Added

* Migrate Agentic Search to use adopt latest API version
* Add TypeSpec to Swagger mapping knowledge to help solve TypeSpec migration questions

## 0.3.0 (2025-11-27)

### Bugs Fixed

* Ehhance intent recognition for non-technical and technical message
  
### Features Added

* Add TypeSpec Validation category for intention prompt

### Other Changes

* Restructured prompt templates to enhance clarity and maintainability

## 0.2.0 (2025-11-13)

### Features Added

* Skip RAG workflow for non-technical questions

## 0.1.0 (2025-10-30)

* Initial release