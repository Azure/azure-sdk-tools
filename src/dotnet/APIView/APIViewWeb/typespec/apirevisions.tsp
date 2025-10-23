import "@typespec/http";
import "./models.tsp";

using TypeSpec.Http;

namespace APIView;

/**
 * API Revisions endpoints
 * These endpoints require Azure AD token authentication via Bearer token.
 */
@route("/api/apirevisions")
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
