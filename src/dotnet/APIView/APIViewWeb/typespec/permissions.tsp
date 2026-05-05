import "@typespec/http";
import "./models.tsp";

using TypeSpec.Http;

namespace APIView;

/**
 * Permissions endpoints accessible via Azure AD token or cookie authentication.
 * These endpoints are intended for use by agents and MCP clients via Bearer token.
 */
@route("/api/permissions")
@useAuth(BearerAuth)
namespace Permissions {
    /**
     * Get the list of GitHub usernames who can approve API reviews for a given language.
     * @param language The language to get approvers for (e.g. "Python", "Java"). Use "apiview" to retrieve APIView admins.
     * @returns Sorted list of GitHub usernames who are approvers for the requested language
     */
    @get
    @route("/approvers/{language}")
    op GetApproversForLanguage(
        @path language: string
    ): {
        @statusCode statusCode: 200;
        @body approvers: string[];
    } | UnauthorizedErrorResponse | InternalServerErrorResponse;

    /**
     * Get the list of GitHub usernames who have APIView admin permissions.
     * @returns Sorted list of GitHub usernames with admin permissions
     */
    @get
    @route("/admins")
    op GetAdminUsernames(): {
        @statusCode statusCode: 200;
        @body admins: string[];
    } | UnauthorizedErrorResponse | InternalServerErrorResponse;
}
