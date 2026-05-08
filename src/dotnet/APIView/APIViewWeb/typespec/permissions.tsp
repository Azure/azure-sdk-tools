import "@typespec/http";
import "./models.tsp";

using TypeSpec.Http;

namespace APIView;

/**
 * Permissions endpoints for querying approvers and admins.
 * These endpoints require Azure AD token authentication via Bearer token.
 */
@route("/api/v1/permissions")
@useAuth(BearerAuth)
namespace Permissions {
    /**
     * Get the list of approvers or admins by language.
     * Pass a programming language (e.g., "Python", "C#", "Java") to get the
     * approvers for that language. Pass "apiview" to get APIView administrators.
     *
     * @param language The programming language, or "apiview" for admins.
     * @returns List of users with their roles for the requested scope.
     */
    @get
    @route("/{language}")
    op getApprovers(
        @path language: string
    ): {
        @statusCode statusCode: 200;
        @body members: RoleMember[];
    } | BadRequestErrorResponse | NotFoundErrorResponse | InternalServerErrorResponse;
}
