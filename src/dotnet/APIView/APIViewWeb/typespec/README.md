# APIView External API TypeSpec Documentation

This directory contains TypeSpec documentation for APIView's external-facing APIs that support API Key Authentication or Azure AD Token Authentication.

## Overview

These APIs are designed for external teams to integrate with APIView programmatically. Internal APIs that only support GitHub OAuth (Cookie authentication) are not documented here.

## Documented APIs

### AutoReview APIs (API Key Authentication)

APIs for automated API review creation and status checks, used in CI/CD pipelines:

- `POST /AutoReview/UploadAutoReview` - Upload automatic API review file
- `GET /AutoReview/GetReviewStatus` - Get review approval status
- `GET /AutoReview/CreateApiReview` - Create API review from Azure DevOps artifacts

**Authentication:** API Key via `ApiKey` header

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
npm run build
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

## Additional Resources

- [TypeSpec Documentation](https://typespec.io/)
- [TypeSpec HTTP Library](https://typespec.io/docs/libraries/http/reference)
- [OpenAPI Specification](https://swagger.io/specification/)
