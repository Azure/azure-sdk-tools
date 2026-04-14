# APIView — Copilot Comments REST API

This document describes the REST API and database schema for the Azure SDK Guidelines Copilot's semantic search system. The Copilot uses vector embeddings to find known bad code patterns and suggest fixes during API reviews. For how Copilot fits into the overall architecture, see the background services section in [overview.md](overview.md#e-background-services-hostedservices). For configuring which languages require Copilot review, see [operations.md](operations.md#copilot-review-required).

---

## Overview

The Copilot uses **semantic search** to:
1. Narrow the set of guidelines sent to the GPT model
2. Include examples (e.g., from architects) that may not be attributable to specific guidelines

The mechanism is: calculate vector embeddings for known "bad code" snippets, then compare them against the embedding of a target code snippet using cosine similarity.

---

## Database Schema

The database uses **Cosmos DB**. Each document has the following schema:

| Property | Type | Description |
|----------|------|-------------|
| `id` | string | GUID for each document |
| `bad_code` | string | Code snippet reflecting a "bad" pattern. The vector embedding is calculated from this. If changed, the embedding must be recalculated. |
| `good_code` | string? | How the bad code should be rewritten. These bad/good pairs are fed to GPT as examples. Optional. |
| `embedding` | float[] | Vector embedding of `bad_code`, calculated by Azure OpenAI's embedding service. This is the field against which semantic search compares. |
| `language` | string | Language the document is relevant to. Acts as a hard filter to avoid cross-language false positives. |
| `comment` | string? | Plain-text explanation around the bad/good pairing. Fed into the GPT request for context. Optional. |
| `guideline_ids` | string[]? | IDs of relevant Azure SDK guidelines. If present, the linked guideline tokens are fed into the GPT request. Null for architect rulings not tied to specific guidelines. Optional. |
| `is_deleted` | boolean | Soft-delete flag. Starts as `false`, set to `true` on delete. Periodic cleanouts remove old soft-deleted entries. |
| `modifiedOn` | DateTime | When the document was last modified. Used for cleanout scheduling. |
| `modifiedBy` | string | Name of the user who last modified the entry. |

---

## Document CRUD API

Manages documents in the database. When a document is created or updated, the `bad_code` snippet has its vector embedding calculated by Azure OpenAI's embeddings service.

### CREATE — `POST`

| Property | Type | Required |
|----------|------|----------|
| `language` | string | Yes |
| `bad_code` | string | Yes |
| `good_code` | string? | No |
| `comment` | string? | No |
| `guideline_ids` | string? | No |

### UPDATE — `PUT` (idempotent)

| Property | Type | Required |
|----------|------|----------|
| `id` | string | Yes |
| `language` | string | Yes |
| `bad_code` | string? | No |
| `good_code` | string? | No |
| `comment` | string? | No |
| `guideline_ids` | string? | No |

### DELETE — `PUT` (idempotent, soft delete)

| Property | Type | Required |
|----------|------|----------|
| `id` | string | Yes |
| `language` | string | Yes |

### GET

| Property | Type | Required |
|----------|------|----------|
| `id` | string | Yes |
| `language` | string | Yes |

---

## Document Search API

Queries the database using vector cosine similarity.

### Request

| Property | Type | Description |
|----------|------|-------------|
| `language` | string | Hard filter — only documents matching this language are considered |
| `code` | string | Code snippet to search against. The API calculates the embedding using Azure OpenAI before searching. |
| `threshold` | float | Confidence score cutoff. Matches below this threshold are excluded. |
| `limit` | int? | Return only the top X matches. Default: 5. |

### Response

| Property | Type | Description |
|----------|------|-------------|
| `results` | dictionary[] | Array of results, sorted by decreasing confidence |
| `results[].confidence` | float | Match quality score |
| `results[].document` | CopilotSearchModel | The document object (see schema above). The `embedding` field is set to null in the response. |

---

## Notes

- `language` should be treated as an enum to standardize the field and align with the Cosmos DB partition key
- The `embedding` field is never returned in API responses — it is only used internally for search
- Documents are soft-deleted (`is_deleted = true`) rather than removed, with periodic cleanouts for entries deleted more than 3 months ago
