---
name: add-endpoint
description: "Add a new FastAPI endpoint to APIView Copilot. Use for: add endpoint, new endpoint, new API route, add route, create endpoint, add API, new POST endpoint, new GET endpoint."
---

# Add a FastAPI Endpoint

## Checklist

When adding a new endpoint, follow every step below.

### 1. Define Pydantic request/response models

- Place models near where they're used — in `app.py` for endpoint-specific models, or in `src/_models.py` for shared/reusable models.
- **All multi-word field names MUST use camelCase aliases.** Never expose snake_case in the JSON API.
- Add `class Config` with `populate_by_name = True` on any model that has aliases so it can be constructed with either the Python name or the alias.
- Use `Field(...)` for required fields, `Field(None, ...)` or `Field(default=..., ...)` for optional ones.
- Every model must have a triple-double-quote docstring.

#### Example

```python
class MyFeatureRequest(BaseModel):
    """Request model for my feature."""

    review_id: str = Field(..., alias="reviewId")
    language: str
    include_deleted: bool = Field(False, alias="includeDeleted")
    max_results: Optional[int] = Field(None, alias="maxResults")

    class Config:
        """Configuration for Pydantic model."""

        populate_by_name = True


class MyFeatureResponse(BaseModel):
    """Response model for my feature."""

    job_id: str = Field(..., alias="jobId")
    result_count: int = Field(..., alias="resultCount")

    class Config:
        """Configuration for Pydantic model."""

        populate_by_name = True
```

#### Rules

| Rule | Correct | Wrong |
|---|---|---|
| JSON field casing | `"reviewId"` | `"review_id"` |
| Alias declaration | `Field(..., alias="reviewId")` | bare `review_id: str` for multi-word names |
| Config on aliased models | `class Config: populate_by_name = True` | missing Config |
| Single-word fields | `language: str` (no alias needed) | `language: str = Field(..., alias="language")` |

### 2. Define the endpoint function

- Use `@app.post(...)` or `@app.get(...)` etc. with `response_model=` pointing to the response model.
- Set `status_code=` when it's not the default 200 (e.g., `202` for async jobs).
- Add `Depends(require_roles(...))` for authentication. Use `AppRole.READER` / `AppRole.APP_READER` for read-only, `AppRole.WRITER` / `AppRole.APP_WRITER` for mutations.
- Add a docstring describing the endpoint.
- Wrap business logic in try/except and raise `HTTPException` with appropriate status codes.
- For long-running work, use `asyncio.to_thread(...)` or background tasks.

#### Example

```python
@app.post("/my-feature", response_model=MyFeatureResponse)
async def my_feature(
    request: MyFeatureRequest,
    _claims=Depends(require_roles(AppRole.READER, AppRole.APP_READER)),
):
    """Handle my feature requests."""
    try:
        result = await asyncio.to_thread(do_work, review_id=request.review_id)
        return MyFeatureResponse(job_id=result.id, result_count=result.count)
    except Exception as e:
        logger.error("Error in /my-feature: %s", e, exc_info=True)
        raise HTTPException(status_code=500, detail="Internal server error") from e
```

### 3. Serialization

FastAPI automatically serializes response models **by alias** when `response_model` is set. This means:

- The JSON response will use the alias names (`jobId`, `resultCount`), not the Python names.
- No extra `by_alias=True` call is needed — FastAPI handles this via the `response_model`.
- When constructing a response object in code, use the **Python field names**: `MyFeatureResponse(job_id=..., result_count=...)`.

### 4. Add a corresponding CLI command

Every endpoint must have a CLI command in `cli.py` with a `--remote` flag. The **core logic must be shared** between remote and local paths to the maximum extent practical.

#### Architecture: shared core function

Extract the business logic into a standalone function (in `src/` or at module level in `cli.py`) that both the endpoint and the CLI's local path call. The CLI's `--remote` path sends an HTTP request to the endpoint instead.

```
                ┌─────────────┐
                │ core logic  │  ← shared function in src/
                │ (do_work)   │
                └──────┬──────┘
                       │
          ┌────────────┴────────────┐
          │                         │
   ┌──────┴──────┐          ┌──────┴──────┐
   │  app.py     │          │  cli.py     │
   │  endpoint   │          │  (local)    │
   └─────────────┘          └─────────────┘
                                   │
                            if --remote:
                            HTTP POST → endpoint
```

#### CLI handler pattern

```python
def my_feature(language: str, review_id: str, include_deleted: bool = False, remote: bool = False):
    """Describe the command."""
    if remote:
        # Remote: HTTP call to the deployed endpoint
        settings = SettingsManager()
        base_url = settings.get("WEBAPP_ENDPOINT")
        payload = {"language": language, "reviewId": review_id, "includeDeleted": include_deleted}
        resp = requests.post(
            f"{base_url}/my-feature", json=payload, headers=_build_auth_header(), timeout=60
        )
        if resp.status_code == 200:
            print(json.dumps(resp.json(), indent=2))
        else:
            print(f"Error: {resp.status_code} - {resp.text}")
    else:
        # Local: call shared core logic directly
        result = do_work(language=language, review_id=review_id, include_deleted=include_deleted)
        print(json.dumps(result, indent=2))
```

Key rules:
- The `--remote` payload must use **camelCase** keys matching the endpoint's request model aliases.
- Local mode calls the **same core function** that the endpoint calls.
- Use `_build_auth_header()` for remote authentication.
- Use `SettingsManager().get("WEBAPP_ENDPOINT")` for the base URL.

#### Register the command

In `CliCommandsLoader.load_command_table`, add the command to the appropriate `CommandGroup`:

```python
with CommandGroup(self, "review", "__main__#{}") as g:
    # ... existing commands ...
    g.command("my-feature", "my_feature")
```

Register any command-specific arguments in `load_arguments`:

```python
with ArgumentsContext(self, "review my-feature") as ac:
    ac.argument("review_id", options_list=["--review-id", "-r"], help="The review ID.")
    ac.argument("include_deleted", action="store_true", help="Include deleted items.")
```

Notes:
- `--remote` and `--language` are already registered globally — don't re-register them.
- Knack maps function parameter names to CLI flags automatically (e.g., `review_id` → `--review-id`).
- Use `type=resolve_language_to_canonical` for language params (already global).

### 5. Common pitfalls to avoid

- **Never return raw dicts with snake_case keys** from an endpoint. Always use a typed response model.
- **Never omit `alias=`** on multi-word field names. The API contract is camelCase.
- **Never use `model_config = ConfigDict(alias_generator=to_camel)`** — this project uses explicit `alias=` per field, not automatic generators.
- **Never forget `populate_by_name = True`** on models with aliases — without it, the model can't be constructed using Python field names.
- **Never duplicate core logic** between the endpoint and the CLI local path. Extract it into a shared function in `src/`.
- **Never use snake_case keys in the remote payload** — the `--remote` path must send camelCase keys matching the request model aliases.
- **Never re-register `--remote` or `--language`** in command-specific `ArgumentsContext` — they are global.
