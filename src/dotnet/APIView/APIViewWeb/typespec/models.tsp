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
enum APIRevisionSelectionType {
    Undefined: "Undefined",
    Latest: "Latest",
    LatestApproved: "LatestApproved",
    LatestAutomatic: "LatestAutomatic",
    LatestManual: "LatestManual"
}

/**
 * Content return type for API revision
 */
enum APIRevisionContentReturnType {
    Text: "Text",
    CodeFile: "CodeFile"
}

/**
 * Review state
 */
enum ReviewState {
    Open: "Open",
    Closed: "Closed"
}

/**
 * Approval status
 */
enum ApprovalStatus {
    Pending: "Pending",
    Approved: "Approved"
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
    TypeSpec: "TypeSpec"
}

/**
 * Comment type
 */
enum CommentType {
    APIRevision: "APIRevision",
    SampleRevision: "SampleRevision"
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
 * API Revision list item model
 */
model APIRevisionListItemModel {
    /** Revision ID */
    Id: string;

    /** Review ID */
    ReviewId: string;

    /** Revision label */
    Label?: string;

    /** Creation date */
    CreatedOn: utcDateTime;

    /** Last updated date */
    LastUpdatedOn?: utcDateTime;

    /** Created by */
    CreatedBy: string;

    /** API revision type */
    APIRevisionType: APIRevisionType;

    /** Is approved */
    IsApproved: boolean;

    /** Is deleted */
    IsDeleted: boolean;

    /** Approvers */
    Approvers: string[];

    /** Files */
    Files: APICodeFileModel[];

    /** Language */
    Language: Language;

    /** Package name */
    PackageName: string;

    /** Package version */
    PackageVersion?: string;

    /** Viewed by users */
    ViewedBy: string[];

    /** Reviewers */
    Reviewers: string[];

    /** Is released */
    IsReleased: boolean;

    /** Copilot review in progress */
    CopilotReviewInProgress: boolean;

    /** Copilot review job ID */
    CopilotReviewJobId?: string;
}

/**
 * Comment for API view agents
 */
model ApiViewAgentComment {
    /** Comment ID */
    CommentId: string;

    /** Element ID where comment is attached */
    ElementId?: string;

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
}

/**
 * Error response
 */
@error
model ErrorResponse {
    @statusCode statusCode: int32;
    
    /** Error message */
    message: string;
}

/**
 * Bad request error response
 */
model BadRequestErrorResponse {
    @statusCode statusCode: 400;
    
    /** Error message */
    message: string;
}

/**
 * Not found error response
 */
model NotFoundErrorResponse {
    @statusCode statusCode: 404;
    
    /** Error message */
    message: string;
}

/**
 * Unauthorized error response
 */
model UnauthorizedErrorResponse {
    @statusCode statusCode: 401;
    
    /** Error message */
    message: string;
}

/**
 * Internal server error response
 */
model InternalServerErrorResponse {
    @statusCode statusCode: 500;
    
    /** Error message */
    message: string;
}
