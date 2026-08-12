import "@typespec/http";
import "./models.tsp";

using TypeSpec.Http;

namespace APIView;

//
// ============================================================
// AutoReview (Legacy, no authentication)
// ============================================================
//

/**
 * Automatic API Review endpoints (Legacy)
 * These endpoints do not require authentication.
 */
@route("/AutoReview")
namespace AutoReviewLegacy {
    /**
     * Get the approval status of an API review revision
     * @param language Language of the package
     * @param packageName Name of the package
     * @param reviewId Optional review ID
     * @param firstReleaseStatusOnly Whether to check first release status only
     * @param packageVersion Optional package version
     * @returns Status code indicating approval state: 200 (APIs approved), 201 (package name approved), 202 (package name not approved)
     */
    @get
    @route("/GetReviewStatus")
    op GetReviewStatus(
        @query language: string,
        @query packageName: string,
        @query reviewId?: string,
        @query firstReleaseStatusOnly?: boolean,
        @query packageVersion?: string
    ): {
        @statusCode statusCode: 200 | 201 | 202;
    } | NotFoundErrorResponse | InternalServerErrorResponse;
}

//
// ============================================================
// AutoReview (Bearer auth)
// ============================================================
//

/**
 * Automatic API Review endpoints
 * These endpoints require Azure AD Token or GitHub Token authentication via the 'Authorization: Bearer' header.
 */
@route("/autoreview")
@useAuth(BearerAuth)
namespace AutoReview {
    /**
     * Upload an automatic API review file
     * @param file The API review file to upload
     * @param label Label for the revision
     * @param compareAllRevisions Whether to compare against all revisions
     * @param packageVersion Version of the package
     * @param setReleaseTag Whether to set release tag
     * @param packageType Type of the package
     * @returns URL of the created/updated review
     */
    @post
    @route("/upload")
    op UploadAutoReview(
        @header contentType: "multipart/form-data",
        @multipartBody body: {
            file: HttpPart<bytes>;
        },
        @query label: string,
        @query compareAllRevisions?: boolean = false,
        @query packageVersion?: string,
        @query setReleaseTag?: boolean = false,
        @query packageType?: string
    ): {
        @statusCode statusCode: 200 | 201 | 202;
        @body reviewUrl: string;
    } | UnauthorizedErrorResponse | InternalServerErrorResponse;

    /**
     * Create an API review from Azure DevOps pipeline artifacts
     * @param buildId Build ID in Azure DevOps
     * @param artifactName Name of the artifact
     * @param originalFilePath Path to original file
     * @param reviewFilePath Path to review file
     * @param label Label for the revision
     * @param repoName Repository name
     * @param packageName Package name
     * @param compareAllRevisions Whether to compare against all revisions
     * @param project Azure DevOps project name
     * @param packageVersion Version of the package
     * @param setReleaseTag Whether to set release tag
     * @param packageType Type of the package
     * @returns URL of the created/updated review
     */
    @post
    @route("/create")
    op CreateApiReview(
        @query buildId: string,
        @query artifactName: string,
        @query originalFilePath: string,
        @query reviewFilePath: string,
        @query label: string,
        @query repoName: string,
        @query packageName: string,
        @query compareAllRevisions: boolean,
        @query project: string,
        @query packageVersion?: string,
        @query setReleaseTag?: boolean = false,
        @query packageType?: string
    ): {
        @statusCode statusCode: 200 | 201 | 202;
        @body reviewUrl: string;
    } | {
        @statusCode statusCode: 204;
    } | UnauthorizedErrorResponse | InternalServerErrorResponse;
}

//
// ============================================================
// API Revisions
// ============================================================
//

/**
 * API Revisions endpoints
 * These endpoints require Azure AD token authentication via Bearer token.
 */
@route("/api/apirevisions")
@useAuth(BearerAuth)
namespace APIRevisions {
    /**
     * Get the outline for an API revision
     * @param apiRevisionId The API revision ID to get the outline for
     * @returns Revision outline text
     */
    @get
    @route("/{apiRevisionId}/outline")
    op GetOutlineRevision(
        @path apiRevisionId: string
    ): {
        @statusCode statusCode: 200;
        @body outline: string;
    } | UnauthorizedErrorResponse | InternalServerErrorResponse;

    /**
     * Generate review text or CodeFile for an API revision
     * @param apiRevisionId The specific API revision ID (required when there is no selectionType)
     * @param reviewId The review ID (required when using selectionType)
     * @param selectionType How to select the API revision
     * @param contentReturnType The content return type. Default is text, but CodeFile can also be selected
     * @returns Plain text representation or CodeFile object of the API review
     */
    @get
    @route("/getRevisionContent")
    op GetAPIRevisionContent(
        @query apiRevisionId?: string,
        @query reviewId?: string,
        @query selectionType?: APIRevisionSelectionType = APIRevisionSelectionType.Undefined,
        @query contentReturnType?: APIRevisionContentReturnType = APIRevisionContentReturnType.Text
    ): {
        @statusCode statusCode: 200;
        @body content: string | CodeFileResponse;
    } | BadRequestErrorResponse | UnauthorizedErrorResponse | NotFoundErrorResponse | InternalServerErrorResponse;
}

//
// ============================================================
// Comments
// ============================================================
//

/**
 * Comments endpoints
 * These endpoints require Azure AD token authentication via Bearer token.
 */
@route("/api/comments")
@useAuth(BearerAuth)
namespace Comments {
    /**
     * Retrieve visible comments for a revision
     * @param apiRevisionId The API revision ID to get comments for
     * @returns List of comments for the revision
     */
    @get
    @route("/getRevisionComments")
    op GetRevisionComments(
        @query apiRevisionId: string
    ): {
        @statusCode statusCode: 200;
        @body comments: ApiViewAgentComment[];
    } | UnauthorizedErrorResponse | NotFoundErrorResponse | InternalServerErrorResponse;
}

//
// ============================================================
// Reviews
// ============================================================
//

@route("/api/reviews")
@useAuth(BearerAuth)
namespace Reviews {
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
     * @param jobId The unique job identifier (obtained from start-copilot-review-job).
     * @returns The current job status and, when complete, the review comments.
     */
    @get
    @route("/get-copilot-review-job/{jobId}")
    op getCopilotReviewJob(
        @path jobId: string
    ): {
        @statusCode statusCode: 200;
        @body result: CopilotReviewJobResponse;
    } | BadRequestErrorResponse | InternalServerErrorResponse;
}
