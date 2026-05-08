import "@typespec/http";
import "./models.tsp";

using TypeSpec.Http;

namespace APIView;

/**
 * Permissions endpoints accessible via Azure AD token.
 * These endpoints are intended for use by agents and MCP clients via Bearer token.
 */
@route("/api/permissions")
@useAuth(BearerAuth)
namespace Permissions {
    /**
     * Get the approvers for a given language with their roles.
     * @param language The language to get approvers for (e.g. "Python", "Java").
     * @returns List of approvers with their language-scoped role
     */
    @get
    @route("/approvers/{language}")
    op GetApproversForLanguage(
        @path language: string
    ): {
        @statusCode statusCode: 200;
        @body body: ApproversForLanguageResponse;
    } | UnauthorizedErrorResponse | InternalServerErrorResponse;

    /**
     * Get the list of GitHub usernames who have APIView admin permissions.
     * @returns Admins with role "admin"
     */
    @get
    @route("/admins")
    op GetAdminUsernames(): {
        @statusCode statusCode: 200;
        @body admins: ReviewerInfo[];
    } | UnauthorizedErrorResponse | InternalServerErrorResponse;
}
