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

    /** Thread ID for the comment */
    ThreadId?: string;
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
