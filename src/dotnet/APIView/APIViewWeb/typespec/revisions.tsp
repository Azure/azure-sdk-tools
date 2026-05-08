import "@typespec/http";
import "./models.tsp";

using TypeSpec.Http;

namespace APIView;

/**
 * API Revisions endpoints
 * These endpoints require Azure AD token authentication via Bearer token.
 */
@route("/api/v1/revisions")
@useAuth(BearerAuth)
namespace APIRevisions {
    /**
     * Get the outline for an API revision
     * @param revisionId The API revision ID to get the outline for
     * @returns Revision outline text
     */
    @get
    @route("/{revisionId}/outline")
    op GetRevisionOutline(
        @path revisionId: string
    ): {
        @statusCode statusCode: 200;
        @body outline: string;
    } | UnauthorizedErrorResponse | InternalServerErrorResponse;

    /**
     * Generate review text or CodeFile for an API revision
     * @param revisionId The specific API revision ID (required when there is no selectionType)
     * @param contentReturnType The content return type. Default is text, but CodeFile can also be selected
     * @returns Plain text representation or JSON CodeFile object of the API revision
     */
    @get
    @route("/{revisionId}")
    op GetRevisionContent(
        @path revisionId: string,
        @query contentReturnType?: RevisionContentReturnType = RevisionContentReturnType.Text
    ): {
        @statusCode statusCode: 200;
        @body content: string | CodeFileResponse;
    } | BadRequestErrorResponse | UnauthorizedErrorResponse | NotFoundErrorResponse | InternalServerErrorResponse;

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
     * @param version Optional package version.
     * @param selectionType Optional selector for common revision scenarios
     * @returns Resolved IDs for use in subsequent API calls.
     */
    @get
    @route("/resolve")
    op resolve(
        @query packageQuery?: string,
        @query language?: string,
        @query version?: string,
        @query selectionType?: RevisionSelectionType = RevisionSelectionType.Undefined,
    ): {
        @statusCode statusCode: 200;
        @body result: RevisionResolveResult;
    } | BadRequestErrorResponse | NotFoundErrorResponse | InternalServerErrorResponse;

    /**
     * Look up APIView revision associated with a pull request.
     *
     * @param prNumber The GitHub pull request number.
     * @param repoName The GitHub repository (e.g., "Azure/azure-sdk-for-python").
     * @returns Resolved revision results for each package in the PR, or 204 if none found.
     */
    @get
    @route("/resolve-pr")
    op resolvePullRequest(
        @query prNumber: int32,
        @query repoName: string,
    ): {
        @statusCode statusCode: 200;
        @body results: RevisionResolveResult[];
    } | {
        @statusCode statusCode: 204;
    } | InternalServerErrorResponse;

    /**
     * Check the approval status of a specific revision.
     * Returns approval state, list of approvers, change history, and
     * gate checks (e.g., copilot review, must-fix comments).
     *
     * @param revisionId The API revision ID.
     * @returns Approval status, approvers, history, and gate results.
     */
    @get
    @route("/{revisionId}/approval-status")
    op getApprovalStatus(
        @path revisionId: string
    ): {
        @statusCode statusCode: 200;
        @body result: ApprovalStatusResponse;
    } | BadRequestErrorResponse | NotFoundErrorResponse | InternalServerErrorResponse;

    /**
     * TODO: What is this for???
     * Get full metadata for a review and revision.
     * Requires a revisionId (obtained from the /resolve endpoint).
     * 
     * @param revisionId The API revision ID (from /resolve endpoint).
     * @returns Full review and revision metadata.
     */
    @get
    @route("/{revisionId}/metadata")
    op getMetadata(
        @path revisionId: string
    ): {
        @statusCode statusCode: 200;
        // TODO: It is strange that ReviewMetadata contains metadata for one specific revision and it's parent review.
        @body metadata: ReviewMetadata;
    } | BadRequestErrorResponse | NotFoundErrorResponse | InternalServerErrorResponse;

    /**
     * Retrieve visible comments for a revision.
     *
     * @param revisionId The API revision ID to get comments for.
     * @returns List of comments for the revision.
     */
    @get
    @route("/{revisionId}/comments")
    op getComments(
        @path revisionId: string
    ): {
        @statusCode statusCode: 200;
        @body comments: ApiViewComment[];
    } | UnauthorizedErrorResponse | NotFoundErrorResponse | InternalServerErrorResponse;
}
