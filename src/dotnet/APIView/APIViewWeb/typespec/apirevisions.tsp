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
    } | UnauthorizedErrorResponse | NotFoundErrorResponse | InternalServerErrorResponse;

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

/**
 * CodeFile response model for API revision content
 */
model CodeFileResponse {
    /** Version string */
    VersionString?: string;

    /** Package name */
    PackageName: string;

    /** Name */
    Name: string;

    /** Language */
    Language: Language;

    /** Cross language package ID */
    CrossLanguagePackageId?: string;

    /** Package version */
    PackageVersion?: string;

    /** Parser version */
    ParserVersion?: string;

    /** Tokens representing the code structure */
    Tokens: CodeFileToken[];

    /** Navigation items */
    Navigation: NavigationItem[];

    /** Diagnostics */
    Diagnostics: CodeDiagnostic[];
}

/**
 * Code file token
 */
model CodeFileToken {
    /** Definition ID */
    DefinitionId?: string;

    /** Navigation display name */
    NavigationDisplayName?: string;

    /** Value */
    Value?: string;

    /** Kind */
    Kind: int32;

    /** Line ID */
    LineId?: string;

    /** Cross language definition ID */
    CrossLanguageDefinitionId?: string;
}

/**
 * Navigation item
 */
model NavigationItem {
    /** Text */
    Text?: string;

    /** Navigation ID */
    NavigationId?: string;

    /** Child items */
    ChildItems?: NavigationItem[];

    /** Tags */
    Tags?: Record<string>;

    /** Is hidden */
    IsHidden?: boolean;
}

/**
 * Code diagnostic
 */
model CodeDiagnostic {
    /** Diagnostic ID */
    DiagnosticId: string;

    /** Text */
    Text: string;

    /** Help link URI */
    HelpLinkUri?: string;

    /** Target ID */
    TargetId: string;

    /** Level (0=Info, 1=Warning, 2=Error) */
    Level: int32;
}
