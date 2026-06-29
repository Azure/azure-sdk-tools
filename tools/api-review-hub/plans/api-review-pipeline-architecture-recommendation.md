# API Review Pipeline Architecture Recommendation

## Goal

Support API review PR creation and review-branch updates across multiple languages without duplicating pipelines or turning the API Review Hub App Service into a build worker.

Constraints:

- No PATs.
- No Git operations from the App Service runtime.
- Web service database is the source of workflow state.
- Language generation paths must be swappable programmatically.
- The web service should use the GitHub App for GitHub operations.

## Recommended Architecture

Use one Azure DevOps orchestrator pipeline with pluggable language-specific generation templates. The pipeline generates and publishes API artifacts. The web service consumes those artifacts and manages GitHub branches, PRs, reviewers, and workflow state through the GitHub App.

This intentionally splits the workflow by security boundary:

- **Azure DevOps pipeline:** untrusted/repo-dependent build and generation work.
- **API Review Hub web service:** durable orchestration state and GitHub API mutations through the GitHub App.
- **GitHub:** source of repository content, review branches, PRs, and webhook events.
- **Database:** source of workflow truth.

## Web Service Responsibilities

The web service should stay stateless with respect to Git working trees. It should not have Git installed and should not shell out to language generators.

The web service should:

- Receive API review requests.
- Validate request shape, repository allowlists, package identity, and requested language.
- Store request state in the service database.
- Trigger the orchestrator pipeline with parameters:
  - `operationId`
  - `packageName`
  - `baseRef`
  - `targetRef`
  - `language`
- Authenticate to Azure DevOps using the same scheme APIView uses: App Service managed identity obtains an AAD token for Azure DevOps, creates a `VssConnection`, and queues the build with `BuildHttpClient`.
- Track the pipeline run ID and map it to the service-owned `operationId`.
- Receive an authenticated Azure DevOps completion callback for the queued run.
- Verify the callback against the stored `operationId` and pipeline run ID.
- Fetch generated artifacts after the callback using the same managed-identity Azure DevOps credential boundary used for queueing.
- Compare `base/api.md` and `target/api.md`, unless the pipeline already reported a trusted comparison result.
- Create or update API review branches using the GitHub Git Data API and the GitHub App installation token.
- Create or reuse the API review PR using the GitHub API.
- Assign reviewers resolved from `.github/ARCHITECTS` using the GitHub API.
- Listen to GitHub PR events and update database state.

The web service should not:

- Run `git`.
- Generate `api.md`.
- Install package dependencies for generation.
- Manage local repo state.

## Pipeline Responsibilities

Use a single reusable request-handling pipeline with language-specific job and artifact templates. The current files are:

```text
eng/pipelines/handle-apireview-hub-request.yml
eng/pipelines/templates/jobs/apireview-hub-job-python.yml
eng/pipelines/templates/steps/create-apireview-hub-artifacts-python.yml
```

The file responsibilities are intentionally layered:

| File | Scope | Responsibility |
|---|---|---|
| `handle-apireview-hub-request.yml` | Language-agnostic request handler | Validates common request parameters, decides whether target-only or base-and-target artifacts are needed, compares artifacts for create requests, writes the result summary, and publishes build artifacts. |
| `apireview-hub-job-{language}.yml` | Language-specific job wrapper | Owns job-level settings that cannot live in a step template, such as pool, image, language tool setup, and shared language variables. |
| `create-apireview-hub-artifacts-{language}.yml` | Language-specific artifact generator | Generates one artifact bundle for one `ref`. It must satisfy the common artifact contract. |

The top-level pipeline derives language template paths from the validated `language` parameter:

```yaml
- template: ${{ format('/eng/pipelines/templates/jobs/apireview-hub-job-{0}.yml', parameters.language) }}

- template: ${{ format('/eng/pipelines/templates/steps/create-apireview-hub-artifacts-{0}.yml', parameters.language) }}
```

This means adding Java should not duplicate the create/update workflow. It should add `java` to the `language` parameter values and add these files:

```text
eng/pipelines/templates/jobs/apireview-hub-job-java.yml
eng/pipelines/templates/steps/create-apireview-hub-artifacts-java.yml
```

### Request Handling Flow

`handle-apireview-hub-request.yml` owns the request flow shared by all languages:

- Validate `operationId`, `packageName`, and `targetRef`.
- Require `baseRef` only when `requestMode` is `create`.
- Always invoke the language artifact template once for `targetRef` with `kind: target`.
- Invoke the language artifact template a second time for `baseRef` with `kind: base` only when `requestMode` is `create`.
- For create requests, compare `base/api.md` and `target/api.md` using file hashes.
- Write `result/result-summary.json` for the web service.
- Publish `apireview-target`, `apireview-result`, and, for create requests, `apireview-base`.
- Notify API Review Hub through the existing authenticated ADO-to-ARH callback path after artifacts are published.

Callback-related pipeline parameters:

```yaml
parameters:
  - name: apiReviewHubEndpoint
    type: string
    default: 'https://api-review-hub-staging.azurewebsites.net'
  - name: completionCallbackAzureSubscription
    type: string
    default: ''
  - name: completionCallbackResourceUrl
    type: string
    default: 'api://api-review-hub'
```

The callback step runs only when `completionCallbackAzureSubscription` is supplied. This lets local or experimental runs skip callback notification while production API Review Hub queueing can require that value. The pipeline posts to `{apiReviewHubEndpoint}/api/operations/{operationId}`. Staging defaults to `https://api-review-hub-staging.azurewebsites.net`; live can pass `https://api-review-hub.azurewebsites.net`.

Update requests intentionally generate only the target bundle. API Review Hub already knows it is updating an existing review workflow and can use the refreshed target artifact to update the review branch.

### Language Job Template Contract

Each language job template must expose a `steps` parameter:

```yaml
parameters:
  - name: steps
    type: stepList
    default: []
```

The job template must run those supplied steps after any language-specific setup. It may define job-level settings and setup that are not valid inside a step template, such as:

- agent pool and image selection
- language runtime selection
- parser/tool installation
- shared variables consumed by the artifact steps

For Python, `apireview-hub-job-python.yml` currently:

- uses `azsdk-pool` with `ubuntu-24.04` by default
- selects Python `3.12`
- clones `tjprescott/azure-sdk-for-python` by default
- installs `azpysdk` once from `toolRef`, defaulting to `main`
- shares `PythonApiReviewRepositoryFullName` with the artifact-generation steps

### Artifact Generation Template Contract

Each language artifact template generates exactly one bundle for one `ref`. It should not decide whether base and target are both needed.

Common parameters:

```yaml
parameters:
  - name: requestMode
    type: string
  - name: operationId
    type: string
  - name: packageName
    type: string
  - name: ref
    type: string
  - name: kind
    type: string
  - name: outputDir
    type: string
  - name: workingDir
    type: string
```

Standard generation contract:

```text
generate(packageName, ref) -> artifact folder
```

The current Python implementation:

- clones the selected Python SDK repository at `ref`
- finds the package under `sdk/*/<packageName>`
- runs the already-installed `azpysdk` parser
- copies generated review files into `outputDir`
- writes artifact provenance metadata

### Artifact Bundle Contract

Minimum artifact folder contents:

```text
api.md
api.metadata.yml
artifact-metadata.json
```

`api.md`, `api.metadata.yml`, and `artifact-metadata.json` are required. `artifact-metadata.json` should include at least:

```json
{
  "packageName": "azure-ai-projects",
  "packageRelativePath": "sdk/.../azure-ai-projects",
  "ref": "azure-ai-projects_2.1.0",
  "version": "2.1.0"
}
```

Each language generator may publish additional files that are useful for API review to the artifact folder. For example, Java may include `pom.xml` or other package metadata alongside `api.md`.

### Result Summary Contract

The request handler publishes `apireview-result/result-summary.json`. It should include:

- `operationId`
- `mode`
- `language`
- `repositoryFullName`
- `packageName`
- `baseRef`
- `targetRef`
- `status`
- `changed`
- `baseVersion`
- `targetVersion`
- `packageRelativePath`
- artifact identifiers

The pipeline should not create GitHub branches or PRs in the recommended security model. Branch and PR mutations belong to the web service through the GitHub App.

### Pipeline Completion Callback

The pipeline should notify API Review Hub after the result artifact has been published. This should reuse the existing authenticated ADO-to-ARH callback pattern used for other API Review Hub status updates, such as release status updates. The current pipeline uses an `AzurePowerShell@5` step with a configured service connection, obtains an Entra token for `completionCallbackResourceUrl`, and posts to `{apiReviewHubEndpoint}/api/operations/{operationId}`.

The callback should be a completion signal, not the artifact payload. It should include enough information for API Review Hub to locate and verify the run:

- `operationId`
- pipeline run/build ID
- ADO project name
- pipeline result/status
- artifact names or identifiers, if available

After receiving the callback, API Review Hub should:

1. Confirm the `operationId` exists and is mapped to the reported ADO run/build ID.
2. Fetch `apireview-result/result-summary.json` from Azure DevOps using managed identity/AAD auth.
3. Verify the result summary matches the stored operation request.
4. Fetch `apireview-target` and, for create requests, `apireview-base`.
5. Continue with GitHub branch and PR operations only after artifact verification succeeds.

The callback endpoint must be idempotent. Duplicate callbacks for the same run should not create duplicate branches, PRs, or reviewer requests.

### Pipeline Restrictions

- The top-level request pipeline must remain language-agnostic.
- The top-level request pipeline must not duplicate create/update flow per language.
- Supported languages must be listed in the `language` parameter values before their template paths can be selected.
- Each supported language must provide both expected template files using the naming convention above.
- Job-level concerns such as `pool` cannot be placed in step templates. They belong in `apireview-hub-job-{language}.yml`.
- Language parser installation should happen once in the job template when possible, not once per base/target artifact step.
- Artifact-generation templates should generate exactly one bundle for one ref.
- Pipeline code must not create GitHub branches, create PRs, request reviewers, or mutate workflow state outside the build artifacts.

## Azure DevOps Authentication Model

API Review Hub should use the same Azure DevOps authentication scheme that APIView uses for sandboxed review generation:

1. The deployed App Service uses its managed identity through the normal Azure credential chain.
2. The service requests an AAD token for Azure DevOps using the Visual Studio Services default scopes.
3. The service creates a `VssConnection` to `https://dev.azure.com/azure-sdk/` with a `VssAadCredential`.
4. The service uses `BuildHttpClient` to resolve and queue the orchestrator pipeline.
5. The service uses the same credential boundary to read pipeline run status and build artifacts.

This matches APIView's `DevopsArtifactRepository` pattern and avoids PATs. Local development can use developer Azure CLI credentials through the same Azure credential chain, but production should rely on managed identity.

Required Azure DevOps authorization should be scoped to the smallest practical permissions for the API Review Hub identity:

- Queue builds for the orchestrator pipeline.
- Read the queued run status.
- Read artifacts for queued runs.
- Read pipeline definitions only as needed to resolve the configured orchestrator pipeline.

The implementation should resolve the pipeline by a configured project and pipeline name or ID, then store the returned run/build ID with the API Review Hub operation record.

## Web Service Branch and PR Publishing

After artifact generation completes, the web service should publish branches with the GitHub Git Data API rather than local Git.

Branch naming example:

```text
apireview/base_<package>_<baseVersion>
apireview/review_<package>_<targetVersion>
```

Expected GitHub API flow:

1. Resolve the repository default branch commit SHA.
2. Create blobs for `api.md` and `api.metadata.yml`.
3. Create a tree rooted at the desired package path.
4. Create a commit for the baseline branch from the default branch commit.
5. Create or update the baseline ref.
6. Create a commit for the review branch from the baseline commit.
7. Create or update the review ref.
8. Create or reuse a draft PR from review branch to baseline branch.
9. Request architect reviewers.
10. Persist branch, PR, and approval workflow state in the service database.

This keeps GitHub write authority in one place: the GitHub App installation token used by the web service.

## Security Boundaries By Step

| Step | Owner | Credential Boundary | Security Notes |
|---|---|---|---|
| Receive API review request | Web service | Caller auth to API Review Hub | Validate caller, repository, language, package name, and refs before queuing work. |
| Store operation state | Web service | Managed identity to database | Database is source of truth for operation state; do not depend on PR body metadata. |
| Trigger pipeline | Web service to ADO | App Service managed identity / AAD token to Azure DevOps | Use APIView's `VssConnection` + `BuildHttpClient.QueueBuildAsync` pattern. No PATs. |
| Checkout source refs | ADO pipeline | Pipeline service identity / repository checkout auth | Pipeline owns Git and language tooling. It should receive only validated refs and package inputs. |
| Generate API artifacts | ADO pipeline | Pipeline agent environment | Language tooling, dependency installation, and generated files stay outside App Service. |
| Publish artifacts | ADO pipeline | ADO artifact storage | Artifacts should be scoped to the run and operation ID. Retention and access control need confirmation. |
| Notify artifact completion | ADO pipeline to web service | Existing authenticated ADO-to-ARH callback model | Callback is only a completion signal. ARH must verify operation/run identity and fetch artifacts itself before GitHub writes. |
| Fetch pipeline result/artifacts | Web service to ADO | App Service managed identity / AAD token to Azure DevOps | Use the same credential boundary as pipeline queueing to read run status and artifacts. |
| Compare artifacts | Pipeline or web service | No GitHub write credential required | If pipeline reports comparison, web service should still be able to verify by comparing `api.md` bytes from artifacts. |
| Create/update review branches | Web service to GitHub | GitHub App installation token | Use GitHub Git Data API; no local Git process in App Service. |
| Create/reuse PR | Web service to GitHub | GitHub App installation token | Draft PR creation and reuse should be idempotent based on persisted operation state and branch names. |
| Assign architects | Web service to GitHub | GitHub App installation token | Resolve `.github/ARCHITECTS` through repository content APIs; reviewer request failures should be non-fatal. |
| Process PR webhooks | Web service | GitHub webhook secret in Key Vault | Validate webhook signatures and update database state idempotently. |

## Open Security Questions

Azure DevOps authentication should follow APIView's managed-identity/AAD model. The remaining security review should focus on exact authorization scope and the existing ADO-to-ARH callback policy, not PAT-based access.

Questions to bring to security review:

1. What exact ADO permissions are required for the API Review Hub managed identity to queue the orchestrator pipeline and read run artifacts?
2. Should the service queue by pipeline definition ID, pipeline name, or a configured allowlisted mapping?
3. How should ADO artifact access be scoped so one operation can only read its own generated artifacts?
4. What final values should production use for `completionCallbackAzureSubscription` and `completionCallbackResourceUrl`?
5. What replay protection and idempotency requirements apply to artifact completion callbacks?
6. What audit events are required for pipeline trigger, callback receipt, artifact retrieval, branch update, PR creation, and reviewer assignment?

Until these are answered, the ADO provider should be implemented behind an abstraction with a stub provider for local API flow testing.

## Reuse Scenarios

### Initial API Review PR Creation

Input:

```text
packageName = azure-ai-projects
baseRef = azure-ai-projects_2.1.0
targetRef = main
language = python
```

Flow:

1. Web service validates request and stores an operation record.
2. Web service triggers the orchestrator pipeline.
3. Pipeline generates base artifacts.
4. Pipeline generates target artifacts.
5. Pipeline publishes artifacts and result summary.
6. Pipeline sends an authenticated completion callback to API Review Hub.
7. Web service verifies the callback, then retrieves and verifies artifacts from ADO.
8. Web service exits cleanly if `api.md` is unchanged.
9. Web service creates or updates baseline and review branches with GitHub APIs.
10. Web service creates or reuses the draft PR.
11. Web service assigns reviewers.
12. Web service stores state in DB.

### Updating Review Branch After Working Branch Push

Input:

```text
packageName = azure-ai-projects
targetRef = <new working branch commit/ref>
language = python
```

Flow:

1. Web service identifies the existing review workflow from database state and GitHub webhook data.
2. Web service queues the same orchestrator pipeline with the new target ref.
3. Pipeline regenerates the target artifact.
4. Pipeline publishes artifacts and sends an authenticated completion callback to API Review Hub.
5. Web service verifies the callback, then retrieves and verifies the target artifact from ADO.
6. Web service updates the existing review branch through the GitHub Git Data API.
7. Existing PR updates automatically.
8. Web service updates DB state.

No separate update pipeline is needed.

## Why This Split Works

The boundary is by capability, not by language or scenario.

| Capability | Owner |
|---|---|
| Request mode handling | Shared request pipeline |
| Language job setup | Language-specific job template |
| API artifact generation | Language-specific artifact template |
| Diffing generated artifacts | Shared request pipeline or web service verification |
| Branch creation/update | Web service using GitHub App and GitHub Git Data API |
| PR creation/reuse | Web service using GitHub App |
| Reviewer assignment | Web service using GitHub App |
| Workflow state | Web service database |

## What To Delete From The Existing Local Script Model

Remove these concepts from the web service flow:

- Local Git working tree management.
- `git pull`, `git checkout`, `git reset`, `git push` inside App Service.
- Local `azpysdk` or language-specific generation inside App Service.
- Custom App Service container solely to obtain Git/Python build tooling.
- Branch reuse heuristics based on local checkout state.
- PR body sync metadata block.
- ADO work item update path.

## Implementation Recommendation

Implement the web service around an artifact-generation provider seam:

```ts
interface ReviewArtifactGenerationProvider {
  queue(request: ReviewArtifactGenerationRequest): Promise<QueuedReviewArtifactGeneration>;
  getResult(operationId: string): Promise<ReviewArtifactGenerationResult>;
}
```

Initial providers:

- `StubReviewArtifactGenerationProvider` for local API flow testing.
- `AzureDevOpsReviewArtifactGenerationProvider` using the same managed-identity/AAD `VssConnection` auth model APIView uses.

The PR publishing code should consume a stable artifact result shape:

```ts
interface ReviewArtifacts {
  packageName: string;
  packageRelativePath: string;
  baseVersion: string;
  targetVersion: string;
  base: {
    apiMd: Buffer;
    metadata?: Buffer;
  };
  target: {
    apiMd: Buffer;
    metadata?: Buffer;
  };
}
```

## Final Recommendation

Use one orchestrator pipeline with parameterized language generator templates. The pipeline should generate and publish artifacts only. The web service should trigger the pipeline, consume artifacts, publish branches through the GitHub Git Data API, create or reuse the PR, assign reviewers, and store workflow state in the database.

This gives you:

- One pipeline definition.
- Programmatic Python / Java / C# swapping.
- Reusable artifact generation.
- Same flow for initial PR creation and review branch refresh.
- No runtime Git in App Service.
- No runtime Python or language tooling in App Service.
- No PATs.
- No PR metadata block.
- No ADO work item dependency.
