import "@typespec/http";
import "./models.tsp";

using TypeSpec.Http;

namespace APIView;

@route("/api/reviews")
@useAuth(BearerAuth)
namespace Reviews {
    /**
     * Result of resolving a review/revision from various inputs.
     * Contains only IDs that should be cached for subsequent API calls.
     */
    model RevisionResolveResult {
        /** The unique identifier of the review */
        reviewId: string;
        /** The unique identifier of the resolved revision. Use this for subsequent API calls. */
        revisionId: string;
    }

    /**
     * Metadata about an API revision
     */
    model RevisionMetadata {
        /** The unique identifier of the API revision */
        revisionId: string;
        /** The version of the package for this revision */
        packageVersion?: string;
        /** Whether this specific revision is approved */
        isApproved: boolean;
        /** The pull request number associated with this revision, if any */
        pullRequestNo?: int32;
        /** The pull request repository associated with this revision, if any */
        pullRequestRepo?: string;
        /** Direct link to view this revision in APIView */
        revisionLink: string;
        /** The user who created this revision */
        createdBy: string;
        /** The date and time when this revision was created */
        createdOn: utcDateTime;
    }

    /**
     * Full metadata about a review and its revision
     */
    model ReviewMetadata {
        /** The unique identifier of the review */
        reviewId: string;
        /** The name of the package being reviewed */
        packageName: string;
        /** The programming language of the package */
        language: string;
        /** Whether the review (package name) is approved */
        isApproved: boolean;
        /** The user who created the review */
        createdBy: string;
        /** The date and time when the review was created */
        createdOn: utcDateTime;
        /** The date and time when the review was last updated */
        lastUpdatedOn: utcDateTime;
        /** Information about the API revision */
        revision: RevisionMetadata;
    }

    /**
     * Request body for starting a copilot review job.
     */
    model StartCopilotReviewJobRequest {
        /** The API surface text to review. Can be raw text or wrapped in a markdown code block (fences are stripped automatically). */
        target: string;
        /** The language of the API (e.g., "python", "dotnet", "java"). Inferred from the markdown fence tag if omitted. */
        language?: string;
        /** A previous version of the API surface text for diff-based review. Markdown fences are stripped if present. */
        base?: string;
        /** A high-level text description of the API for additional context. */
        outline?: string;
        /** A list of existing review comments to provide prior review context. */
        existingComments?: ApiViewAgentComment[];
    }

    /**
     * Response returned when a copilot review job is successfully started.
     */
    model CopilotReviewJobStartedResponse {
        /** The unique job identifier. Use this to poll for results via get-copilot-review-job. */
        @encodedName("application/json", "job_id")
        jobId: string;
    }

    /**
     * Resolve a review/revision from various input types.
     * Returns IDs that can be cached and used for subsequent API calls.
     * 
     * Supports:
     * - Package name + language (+ optional version)
     * - APIView URL (review or revision link)
     * 
     * @param packageQuery Package name query (e.g., "Azure.Storage.Blobs"). Requires language.
     * @param language Programming language (e.g., "C#", "Python"). Requires packageQuery.
     * @param version Optional package version. If not specified, resolves to latest revision.
     * @param link APIView URL (e.g., "https://spa.apiview.dev/review/{id}?activeApiRevisionId={revId}")
     * @returns Resolved IDs for use in subsequent API calls.
     */
    @get
    @route("/resolve")
    op resolve(
        @query packageQuery?: string,
        @query language?: string,
        @query version?: string,
        @query link?: string
    ): {
        @statusCode statusCode: 200;
        @body result: RevisionResolveResult;
    } | BadRequestErrorResponse | NotFoundErrorResponse | InternalServerErrorResponse;

    /**
     * Get full metadata for a review and revision.
     * Requires a revisionId (obtained from the /resolve endpoint).
     * 
     * @param revisionId The API revision ID (from /resolve endpoint).
     * @returns Full review and revision metadata.
     */
    @get
    @route("/metadata")
    op getMetadata(
        @query revisionId: string
    ): {
        @statusCode statusCode: 200;
        @body metadata: ReviewMetadata;
    } | BadRequestErrorResponse | NotFoundErrorResponse | InternalServerErrorResponse;

    /**
     * Start an automated API review job via the AVC (API View Copilot) service.
     * The target text can be raw API surface text or wrapped in a markdown code block
     * (fences are stripped automatically and the language tag is used if language is not provided).
     *
     * @param request The review job request body.
     * @returns A job ID that can be used to poll for results.
     */
    @post
    @route("/start-copilot-review-job")
    op startCopilotReviewJob(
        @body request: StartCopilotReviewJobRequest
    ): {
        @statusCode statusCode: 202;
        @body result: CopilotReviewJobStartedResponse;
    } | BadRequestErrorResponse | InternalServerErrorResponse;

    /**
     * Get the status or result of a copilot review job.
     * Poll this endpoint with the job ID returned from start-copilot-review-job.
     * When the job completes successfully, the response includes the generated review comments.
     *
     * The response is the raw JSON from the copilot service, passed through without deserialization.
     * The response shape is defined by `ApiReviewJobStatusResponse` in the copilot service TypeSpec:
     * packages/python-packages/apiview-copilot/tsp/main.tsp
     *
     * @param jobId The unique job identifier (obtained from start-copilot-review-job).
     * @returns The raw copilot service JSON response (ApiReviewJobStatusResponse).
     */
    @get
    @route("/get-copilot-review-job/{jobId}")
    op getCopilotReviewJob(
        @path jobId: string
    ): {
        @statusCode statusCode: 200;
        @header contentType: "application/json";
        @body result: string;
    } | BadRequestErrorResponse | InternalServerErrorResponse;
}
