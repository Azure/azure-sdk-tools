# APIView External API TypeSpec Documentation

This directory contains TypeSpec documentation for APIView's external-facing APIs that support API Key Authentication, Github Token or Azure AD Token Authentication.

## Overview

These APIs are designed for external teams to integrate with APIView programmatically. Internal APIs that only support GitHub OAuth (Cookie authentication) are not documented here.

## Documented APIs

### AutoReview APIs (No Authentication)

API for review status checks, used in CI/CD pipelines:

- `GET /AutoReview/GetReviewStatus` - Get review approval status

**Authentication:** None required

### AutoReview APIs (Azure AD Token or GitHub Token Authentication)

APIs for automated API review creation, used in CI/CD pipelines:

- `POST /autoreview/upload` - Upload automatic API review file
- `POST /autoreview/create` - Create API review from Azure DevOps artifacts

**Authentication:** Azure AD or GitHub Bearer token

### API Revisions APIs (Azure AD Token Authentication)

APIs for retrieving API revision content and metadata:

- `GET /api/apirevisions/{apiRevisionId}/outline` - Get API revision outline
- `GET /api/apirevisions/getRevisionContent` - Get API revision content (text or CodeFile)

**Authentication:** Azure AD Bearer token

### Comments APIs (Azure AD Token Authentication)

APIs for retrieving comments on API revisions:

- `GET /api/comments/getRevisionComments` - Get revision comments

**Authentication:** Azure AD Bearer token

## Files

- `main.tsp` - Main TypeSpec entry point with service definition
- `models.tsp` - Common models, enums, and error responses
- `autoreview.tsp` - AutoReview API endpoints (API Key auth)
- `apirevisions.tsp` - API Revisions endpoints (Azure AD auth)
- `comments.tsp` - Comments endpoints (Azure AD auth)
- `tspconfig.yaml` - TypeSpec compiler configuration
- `package.json` - NPM package configuration

## Building

### Prerequisites

- Node.js (v16 or later)
- npm

### Install Dependencies

```bash
npm install
```

### Compile TypeSpec to OpenAPI

```bash
tsp compile .
```

This generates OpenAPI 3.0 specification in `tsp-output/openapi.yaml`.

### Watch Mode

For development, you can use watch mode to automatically recompile on changes:

```bash
npm run watch
```

## Output

The compiled OpenAPI specification is generated at:
- `tsp-output/openapi.yaml` - OpenAPI 3.0 YAML specification

This file can be used with tools like Swagger UI, Postman, or other OpenAPI-compatible tools.

## Authentication

### API Key Authentication

For AutoReview APIs, include the API key in the request header:

```
ApiKey: <your-api-key>
```

### Azure AD Token Authentication

For API Revisions and Comments APIs, include the Azure AD bearer token:

```
Authorization: Bearer <azure-ad-token>
```

### Github Token Authentication

For API Revisions and Comments APIs, include the Azure AD bearer token:

```
Authorization: Bearer <github-token>
```

## Reviewing the API Documentation

The compiled OpenAPI specification (`tsp-output/openapi.yaml`) can be:

1. **Viewed in Swagger UI**: Upload the YAML file to https://editor.swagger.io/ to visualize and test the APIs
2. **Reviewed in APIView**: Upload the TypeSpec files to APIView for API design review by the archboard
3. **Used for Code Generation**: Generate client libraries using OpenAPI generators
4. **Imported into Postman**: Create automated API tests and documentation

## Example Usage

### Using AutoReview API to Upload a Review

```bash
curl -X POST "https://apiview.dev/autoreview/upload?label=v1.0.0" \
  -H "Authorization: Bearer your-token-here" \
  -F "file=@path/to/api-review-file.json"
```

### Getting API Revision Content with Azure AD Token

```bash
curl -X GET "https://apiview.dev/api/apirevisions/getRevisionContent?apiRevisionId=abc123" \
  -H "Authorization: Bearer your-azure-ad-token"
```

### Getting Comments for a Revision

```bash
curl -X GET "https://apiview.dev/api/comments/getRevisionComments?apiRevisionId=abc123" \
  -H "Authorization: Bearer your-azure-ad-token"
```

## Design Principles

This TypeSpec documentation follows these principles:

1. **External-Only**: Only documents APIs that external teams should use (API Key or Azure AD auth)
2. **Complete**: All parameters, responses, and error cases are documented
3. **Standards-Based**: Uses OpenAPI 3.0 standard for maximum compatibility
4. **Reviewable**: Can be uploaded to APIView itself for API design review

## Additional Resources

- [TypeSpec Documentation](https://typespec.io/)
- [TypeSpec HTTP Library](https://typespec.io/docs/libraries/http/reference)
- [OpenAPI Specification](https://swagger.io/specification/)
- [Swagger Editor](https://editor.swagger.io/) - Online tool to view and test the generated OpenAPI spec
