import "@typespec/http";
import "./models.tsp";

using TypeSpec.Http;

namespace APIView;

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
