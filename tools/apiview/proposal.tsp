import "@typespec/http";

using TypeSpec.Http;

@service(#{ title: "API Review Hub" })
namespace ApiReviewHub {
  /**
   * Proposed service contract for the standalone TypeScript API Review Hub service.
   *
   * This file describes the HTTP API shape only. Azure resource stamps, App Service deployment
   * slots, Cosmos DB containers, App Configuration, Key Vault, and Application Insights are
   * deployment concerns covered by the adjacent proposal document.
   */

  namespace Models {
    @format("uuid")
    scalar Guid extends string;

    /** Product or service grouping for related client packages. */
    model Service {
      /** Stable service identifier. */
      id: Guid;

      /** Human-readable service name, such as Azure Storage Blobs. */
      name: string;

      /** Packages grouped under this service. */
      packages?: Package[];

      /** Path to the TypeSpec project in the azure-rest-api-specs repository that groups these packages. */
      typespecPath?: string;
    }

    /** Language-specific package grouped under a service. */
    model Package {
      /** Stable package identifier. */
      id: Guid;

      /** Service that owns this package. */
      service?: Service;

      /** Package language or ecosystem. */
      language: string;

      /** Package name within the language ecosystem. */
      packageName: string;

      /** Versioned releases of this package. */
      versions?: PackageVersion[];
    }

    /** Specific versioned release of a package. */
    model PackageVersion {
      /** Stable package version identifier. */
      id: Guid;

      /** Package associated with this version. */
      package: Package;

      /** Package version string. */
      version: string;

      /** Whether this package version is stable or preview. */
      kind: PackageVersionKind;

      /** Time this package version was released. */
      releasedOn?: offsetDateTime;

      /** GitHub review pull requests associated with this package version. */
      reviewPullRequests?: ReviewPullRequest[];
    }

    /** Approval decision associated with a package version and API hash. */
    model ApprovalRecord {
      /** Package name copied from the associated package for release gate verification. */
      packageName: string;

      /** Package version copied from the associated package version for release gate verification. */
      version: string;

      /** The API hash that was approved. */
      apiHash: string;

      /** Approval outcome. */
      status: ApprovalStatus;

      /** The GitHub identity that granted, changed, or revoked the approval. */
      lastUpdatedBy: string;

      /** Time the approval state changed. */
      lastUpdatedOn?: offsetDateTime;
    }

    /** GitHub pull request associated with API review for a package version. */
    model ReviewPullRequest {
      /** Stable review pull request identifier. */
      id: Guid;

      /** Package version associated with this review pull request. */
      packageVersion: PackageVersion;

      /** GitHub pull request identity for the API review. */
      ...GitHubPullRequest;

      /** Baseline branch used to compare the API surface. */
      baseBranch: string;

      /** Baseline tag used when the review pull request was created. */
      baseTag: string;

      /** Branch containing the API review changes. */
      reviewBranch: string;

      /** Working branch used to prepare the review pull request. */
      workingBranch: string;

      /** Pull request for the working branch, when one exists. */
      workingPR?: GitHubPullRequest;

      /** Most recent approval decision associated with this review pull request. */
      approval: ApprovalRecord;
    }

    /** GitHub pull request reference. */
    model GitHubPullRequest {
      /** Repository containing the pull request. */
      repository: GitHubRepository;

      /** Pull request number. */
      number: int32;

      /** Pull request URL. */
      url: string;

      /** GitHub-derived pull request lifecycle status. */
      status: GitHubPullRequestStatus;
    }

    /** Request to create a GitHub review pull request for a package API change. */
    model ReviewPullRequestCreationRequest {
      /** Package language or ecosystem. */
      language: string;

      /** Package name within the language ecosystem. */
      packageName: string;

      /** Baseline tag used to compare the target branch API surface. */
      baseTag: string;

      /** Branch containing the target API surface to review. */
      targetBranch: GitBranchReference;
    }

    /** Request to resolve either package coordinates or a review pull request URL. */
    model ReviewLookupRequest {
      /** Review pull request URL. */
      reviewPRUrl?: string;

      /** Package language or ecosystem. */
      language?: string;

      /** Package name within the language ecosystem. */
      packageName?: string;

      /** Package version string. */
      version?: string;
    }

    /** Git branch reference that identifies the repository and branch containing the target API surface. */
    model GitBranchReference {
      /** Repository owner or organization. */
      owner: string;

      /** Repository name. */
      repo: string;

      /** Branch name. */
      name: string;
    }

    /** Response returned after API Review Hub accepts a review pull request creation request. */
    model ReviewPullRequestCreationAcceptedResponse {
      /** Service-owned operation identifier. */
      operationId: string;

      /** Durable request status. */
      status: "accepted";
    }

    /** Status of an asynchronous review pull request creation operation. */
    model ReviewPullRequestCreationOperationStatus {
      /** Service-owned operation identifier. */
      operationId: string;

      /** Current operation status. */
      status: "accepted" | "running" | "succeeded" | "failed";

      /** Created review pull request when the operation succeeds. */
      reviewPullRequest?: ReviewPullRequest;

      /** Failure reason when the operation fails. */
      failureReason?: string;
    }

    /** Release-gating decision returned for a package or API artifact. */
    model ReleaseGateDecision {
      /** Whether release should be allowed. */
      allowed: boolean;

      /** Machine-readable decision reason. */
      reason: "approved" | "missingApproval" | "rejected" | "unknownPackage" | "staleArtifact";

      /** Most recent approval record. */
      approval: ApprovalRecord;
    }

    /** Request to mark a package version as released. */
    model MarkPackageVersionReleasedRequest {
      /** Package language or ecosystem. */
      language: string;

      /** Package name within the language ecosystem. */
      packageName: string;

      /** Package version string. */
      version: string;

      /** Time this package version was released. */
      releasedOn: offsetDateTime;
    }

    /** Response returned when package lookup inputs resolve to a package or package version. */
    model PackageResolveResponse {
      /** Kind of resource resolved from the lookup inputs. */
      kind: "package" | "packageVersion";

      /** Package resolved from the lookup inputs when no package version was provided. */
      package?: Package;

      /** Package version resolved from the lookup inputs when a package version was provided. */
      packageVersion?: PackageVersion;
    }

    /** Repository linked to API Review Hub for GitHub webhook and automation workflows. */
    model RepositoryRegistration {
      /** Repository linked to API Review Hub. */
      repository: GitHubRepository;

      /** Stable Key Vault key whose secret value is the currently accepted webhook secret. */
      webhookSecretKey: string;

      /** Stable Key Vault key whose secret value is the previous webhook secret accepted during a rotation overlap window. */
      lastWebhookSecretKey: string;

      /** Time this repository registration was last updated. */
      lastUpdated: offsetDateTime;

      /** Time API Review Hub should rotate the current webhook secret before it expires. */
      rotationDate: offsetDateTime;
    }

    /** GitHub repository identity associated with a review, webhook, or automation request. */
    model GitHubRepository {
      /** Repository owner or organization. */
      owner: string;

      /** Repository name. */
      name: string;
    }

    /** Health probe response. */
    model HealthStatus {
      status: "ok";
    }

    /** Error response returned when API Review Hub cannot complete a request. */
    model ErrorResponse {
      /** Machine-readable error code. */
      code: string;

      /** Human-readable error message. */
      message: string;

      /** Request field or resource associated with the error. */
      target?: string;
    }

    /** Error response for invalid requests. */
    model BadRequestErrorResponse {
      @statusCode statusCode: 400;
      @body error: ErrorResponse;
    }

    /** Error response for missing resources. */
    model NotFoundErrorResponse {
      @statusCode statusCode: 404;
      @body error: ErrorResponse;
    }

    /** Error response for unexpected service failures. */
    model InternalServerErrorResponse {
      @statusCode statusCode: 500;
      @body error: ErrorResponse;
    }

    /** Common API error responses. */
    alias ApiErrorResponse = BadRequestErrorResponse | NotFoundErrorResponse | InternalServerErrorResponse;

    /** The approval status of a review. */
    alias ApprovalStatus = "approved" | "rejected" | "revoked" | "pending";

    /** The kind of package version. */
    alias PackageVersionKind = "stable" | "preview";

    /** GitHub pull request lifecycle status derived from state, draft, and merged state. */
    alias GitHubPullRequestStatus = "draft" | "open" | "closed" | "merged";
  }

  @route("/api")
  namespace Api {
    using ApiReviewHub.Models;

    /** Accepts GitHub webhook events from repositories subscribed through the GitHub App or webhook configuration. */
    @route("/github/webhook-events")
    @post
    op acceptGitHubWebhookEvent(
      /** GitHub event name, such as pull_request, check_suite, issue_comment, or workflow_run. */
      @header("X-GitHub-Event") eventType: string,

      /** GitHub delivery identifier used for idempotency and diagnostics. */
      @header("X-GitHub-Delivery") deliveryId: string,

      /** GitHub SHA-256 webhook signature. The service must validate this before taking any other action. */
      @header("X-Hub-Signature-256") signatureSha256: string,

      /** Raw GitHub webhook payload. The implementation must validate the signature against the exact received bytes. */
      @body payload: bytes,
    ): {
      @statusCode statusCode: 202;
    } | ApiErrorResponse;

    /** Requests creation of a GitHub review pull request for a package API change. */
    @route("/review-prs")
    @post
    op requestReviewPullRequestCreation(
      @header("Authorization") authorization: string,
      @body request: ReviewPullRequestCreationRequest,
    ): {
      @statusCode statusCode: 202;
      @body body: ReviewPullRequestCreationAcceptedResponse;
    } | ApiErrorResponse;

    /** Lists GitHub review pull requests known to API Review Hub. */
    @route("/review-prs")
    @get
    op listReviewPullRequests(
      @header("Authorization") authorization: string,
      @query language?: string,
      @query workingBranch?: string,
    ): ReviewPullRequest[] | ApiErrorResponse;

    /** Resolves review pull request lookup inputs to a review pull request record. */
    @route("/review-prs/resolve")
    @post
    op resolveReviewPullRequest(
      @header("Authorization") authorization: string,
      @body request: ReviewLookupRequest,
    ): ReviewPullRequest | ApiErrorResponse;

    /** Gets a GitHub review pull request by identifier. */
    @route("/review-prs/{reviewPullRequestId}")
    @get
    op getReviewPullRequest(
      @path reviewPullRequestId: Guid,
      @header("Authorization") authorization: string,
    ): ReviewPullRequest | ApiErrorResponse;

    /** Gets the status of a review pull request creation operation. */
    @route("/review-prs/operations/{operationId}")
    @get
    op getReviewPullRequestCreationOperationStatus(
      @path operationId: string,
      @header("Authorization") authorization: string,
    ): ReviewPullRequestCreationOperationStatus | ApiErrorResponse;

    /** Lists all Services known to API Review Hub. */
    @route("/services")
    @get
    op listServices(
      @header("Authorization") authorization: string,
    ): Service[] | ApiErrorResponse;

    /** Gets Service metadata and associated Packages. */
    @route("/services/{serviceId}")
    @get
    op getService(
      @path serviceId: Guid,
      @header("Authorization") authorization: string,
    ): Service | ApiErrorResponse;

    /** Lists package records known to API Review Hub. */
    @route("/packages")
    @get
    op listPackages(
      @header("Authorization") authorization: string,
      @query language?: string,
      @query serviceId?: Guid,
    ): Package[] | ApiErrorResponse;

    /** Gets package metadata by package identifier. */
    @route("/packages/{packageId}")
    @get
    op getPackage(
      @path packageId: Guid,
      @header("Authorization") authorization: string,
    ): Package | ApiErrorResponse;

    /** Lists version records for a package. */
    @route("/packages/{packageId}/versions")
    @get
    op listPackageVersions(
      @path packageId: Guid,
      @header("Authorization") authorization: string,
    ): PackageVersion[] | ApiErrorResponse;

    /** Resolves package lookup inputs to a package or package version record. */
    @route("/packages/resolve")
    @post
    op resolvePackage(
      @header("Authorization") authorization: string,
      @body request: ReviewLookupRequest,
    ): PackageResolveResponse | ApiErrorResponse;

    /** Evaluates whether a package or API artifact has the approval needed for release. */
    @route("/releases/check-gate")
    @get
    op evaluateReleaseGate(
      @header("Authorization") authorization: string,
      @query language: string,
      @query packageName: string,
      @query version: string,
      @query apiHash: string,
    ): ReleaseGateDecision | ApiErrorResponse;

    /** Marks a package version as released after the release gate succeeds. */
    @route("/releases/mark-released")
    @post
    op markPackageVersionReleased(
      @header("Authorization") authorization: string,
      @body request: MarkPackageVersionReleasedRequest,
    ): {
      @statusCode statusCode: 204;
    } | ApiErrorResponse;

    /** Service health probe. */
    @route("/health")
    @get
    op getHealth(): HealthStatus | ApiErrorResponse;
  }

  namespace Web {
    /** Reserved for future use to display dashboards or pages as needed. */
  }
}