import "@typespec/http";

using TypeSpec.Http;

namespace APIView;

/**
 * API Revision type
 */
enum APIRevisionType {
    Manual: "Manual",
    Automatic: "Automatic",
    PullRequest: "PullRequest",
    All: "All"
}

/**
 * Selection type for API revision
 */
enum RevisionSelectionType {
    Undefined: "Undefined",
    Latest: "Latest",
    LatestApproved: "LatestApproved",
    LatestAutomatic: "LatestAutomatic",
    LatestManual: "LatestManual"
}

/**
 * Content return type for API revision
 */
enum RevisionContentReturnType {
    Text: "Text",
    CodeFile: "CodeFile"
}

/**
 * Language of the API
 */
enum Language {
    C: "C",
    CSharp: "C#",
    Cpp: "C++",
    Go: "Go",
    Java: "Java",
    JavaScript: "JavaScript",
    Json: "Json",
    Kotlin: "Kotlin",
    Python: "Python",
    Swagger: "Swagger",
    Swift: "Swift",
    TypeScript: "TypeScript",
    Xml: "Xml",
    TypeSpec: "TypeSpec",
    /** Pseudo-language representing APIView platform-level scope */
    APIView: "APIView"
}

/**
 * Role within APIView
 */
enum Role {
    Admin: "Admin",
    Architect: "Architect",
    DeputyArchitect: "DeputyArchitect"
}

/**
 * Comment type
 */
enum CommentType {
    APIRevision: "APIRevision",
    SampleRevision: "SampleRevision"
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
 * File information in an API revision
 */
model APICodeFileModel {
    /** File ID */
    FileId: string;

    /** File name */
    FileName: string;

    /** Package name */
    PackageName: string;

    /** Version string */
    VersionString?: string;

    /** Language */
    Language: Language;

    /** Cross language package ID */
    CrossLanguagePackageId?: string;
}

/**
 * Comment for API view agents
 */
model ApiViewComment {
    /** Line number where comment is attached */
    LineNo: int32;

    /** 
     * (Preview) Element ID where comment is attached.
     * This field is experimental and may change or be removed in future versions.
     */
    @encodedName("application/json", "_lineId")
    LineId: string;

    /** 
     * (Preview) Text of the line where comment is attached.
     * This field is experimental and may change or be removed in future versions.
     */
    @encodedName("application/json", "_lineText")
    LineText: string;

    /** Comment text */
    CommentText: string;

    /** Created by */
    CreatedBy: string;

    /** Created on */
    CreatedOn: utcDateTime;

    /** Is resolved */
    IsResolved: boolean;

    /** Comment type */
    CommentType: CommentType;

    /** Target lines for the comment */
    Lines: string[];

    /** Thread ID for the comment */
    ThreadId?: string;
}


/**
 * Comment severity
 */
enum CommentSeverity {
    Question: "Question",
    Suggestion: "Suggestion",
    ShouldFix: "ShouldFix",
    MustFix: "MustFix"
}

/**
 * Approval status
 */
enum ApprovalStatus {
    Pending: "Pending",
    Approved: "Approved"
}

/**
 * Summary item for a review (package) in list results
 */
model ReviewListItem {
    /** The unique identifier of the review */
    reviewId: string;

    /** Package name */
    packageName: string;

    /** Programming language */
    language: string;

    /** Whether the review is approved */
    isApproved: boolean;

    /** Whether the review is closed */
    isClosed: boolean;

    /** The user who created the review */
    createdBy: string;

    /** When the review was last updated */
    lastUpdatedOn: utcDateTime;
}

/**
 * Paginated list response for reviews
 */
model ReviewListResponse {
    /** The reviews on this page */
    reviews: ReviewListItem[];

    /** Number of items already read (for continuation) */
    noOfItemsRead: int32;

    /** Page size */
    pageSize: int32;

    /** Total count of matching reviews */
    totalCount: int32;
}

/**
 * Approval gate check result
 */
model ApprovalGate {
    /** Human-readable name of the gate */
    name: string;

    /** Whether this gate is satisfied */
    passed: boolean;

    /** Description or reason if the gate is not satisfied */
    message?: string;
}

/**
 * Approval status response for a review/revision
 */
model ApprovalStatusResponse {
    /** The review ID */
    reviewId: string;

    /** The revision ID */
    revisionId: string;

    /** Status of the approval */
    status: ApprovalStatus;

    /** List of users who approved this revision */
    approvers: string[];

    history: ApprovalHistoryEntry[];

    /** Approval gates and their current status */
    gates: ApprovalGate[];
}

/**
 * An entry in the approval change history
 */
model ApprovalHistoryEntry {
    /** The user who made the change */
    changedBy: string;

    /** When the change was made */
    changedOn: utcDateTime;

    /** The action taken (e.g., "Approved", "ApprovalReverted") */
    changeAction: string;

    /** Additional notes */
    notes?: string;
}

/**
 * A user with a role assignment for a given language or scope.
 */
model RoleMember {
    /** GitHub username */
    name: string;

    /** The role */
    role: Role;

    /** The language scope, or "APIView" for platform-level roles */
    language: Language;
}

/**
 * Result of resolving a review/revision from various inputs.
 * Contains only IDs that should be cached for subsequent API calls.
 */
model RevisionResolveResult {
    /** The unique identifier of the review. */
    reviewId: string;
    /** The unique identifier of the resolved revision. */
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
    existingComments?: ApiViewComment[];
}

/**
 * Response returned when a copilot review job is successfully started.
 */
model CopilotReviewJobStartedResponse {
    /** The unique job identifier. Use this to poll for results via get-copilot-review-job. */
    jobId: string;
}

/**
 * Response returned when polling a copilot review job.
 * This model mirrors the `ApiReviewJobStatusResponse` from the copilot service TypeSpec:
 * packages/python-packages/apiview-copilot/tsp/main.tsp
 */
model CopilotReviewJobResponse {
    /** The job status (e.g., "InProgress", "Success", "Error"). */
    status: string;
    /** Additional details about the job status. */
    details?: string;
    /** The review comments generated by the copilot, present when status is "Success". */
    comments?: CopilotReviewComment[];
}

/**
 * A review comment from the copilot service.
 * This model mirrors the `ReviewComment` from the copilot service TypeSpec:
 * packages/python-packages/apiview-copilot/tsp/main.tsp
 */
model CopilotReviewComment {
    /** Guideline IDs referenced by the comment */
    @encodedName("application/json", "guideline_ids")
    guidelineIds?: string[];
    /** Memory IDs referenced by the comment */
    @encodedName("application/json", "memory_ids")
    memoryIds?: string[];
    /** Line number the comment refers to */
    @encodedName("application/json", "line_no")
    lineNo: int32;
    /** The code snippet that triggered the comment */
    @encodedName("application/json", "bad_code")
    code?: string;
    /** Suggested replacement or improvement */
    suggestion?: string;
    /** The comment text */
    comment: string;
    /** Source of the comment (e.g. analyzer, summary) */
    source?: string;
    /** Whether the comment is generic (not tied to a specific line) */
    @encodedName("application/json", "is_generic")
    isGeneric: boolean;
    /** Correlation ID linking related comments */
    @encodedName("application/json", "correlation_id")
    correlationId?: string;
    /** Severity of the comment (e.g. SHOULD, MUST, SUGGESTION, QUESTION) */
    severity?: string;
    /** Confidence score (0-1) */
    @encodedName("application/json", "confidence_score")
    confidenceScore: float32;
}

/**
 * Bad request error response
 */
@error
model BadRequestErrorResponse {
    @statusCode statusCode: 400;
    
    /** Error message */
    message: string;
}

/**
 * Not found error response
 */
@error
model NotFoundErrorResponse {
    @statusCode statusCode: 404;
    
    /** Error message */
    message: string;
}

/**
 * Unauthorized error response
 */
@error
model UnauthorizedErrorResponse {
    @statusCode statusCode: 401;
    
    /** Error message */
    message: string;
}

/**
 * Internal server error response
 */
@error
model InternalServerErrorResponse {
    @statusCode statusCode: 500;
    
    /** Error message */
    message: string;
}
