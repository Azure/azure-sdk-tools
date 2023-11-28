
export declare const enum ApiViewTokenKind {
    Text = 0,
    Newline = 1,
    Whitespace = 2,
    Punctuation = 3,
    Keyword = 4,
    LineIdMarker = 5, // use this if there are no visible tokens with ID on the line but you still want to be able to leave a comment for it
    TypeName = 6,
    MemberName = 7,
    StringLiteral = 8
}

export declare interface IApiViewFile {
    Name: string;
    Tokens: IApiViewToken[];
    Navigation: IApiViewNavItem[];
    PackageName: string;
    VersionString: string;
    Language: string;
    PackageVersion: string;
}

export declare interface IApiViewToken {
    Kind: ApiViewTokenKind;
    DefinitionId?: string;
    NavigateToId?: string;
    Value?: string;
}

export declare interface IApiViewNavItem {
    Text: string;
    NavigationId: string;
    ChildItems: IApiViewNavItem[];
    Tags: {
        [propertyName: string]: string;
    };
}
