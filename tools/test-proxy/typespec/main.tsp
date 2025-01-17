import "@typespec/http";

using TypeSpec.Http;

@service({
    title: "Test-Proxy Record/Playback server"
})
@server("https://localhost:5001/", "The test-proxy default URL.")

namespace TestProxy;

model StartPayload {
    "x-recording-file": string;
    "x-recording-assets-file"?: string;
}

model CommonErrorResponse {
    Message: string;
    Status: string;
    StackTrace?: string;
    @header("x-request-exception") requestException: boolean;
    @header("x-request-exception-error") requestExceptionError: string;
}

model BadRequestErrorResponse {
    @statusCode statusCode: 400;
    ...CommonErrorResponse
}

model NotFoundErrorResponse {
    @statusCode statusCode: 404;
    ...CommonErrorResponse
}

model UnhandledErrorResponse {
    @statusCode statusCode: 500;
    ...CommonErrorResponse
}

@route("/Record/Start")
namespace RecordStart {
    @post
    op start(@body body: StartPayload): {
        @statusCode statusCode: 200;
        @header("x-recording-id") recordingId: string;
    } | BadRequestErrorResponse | NotFoundErrorResponse | UnhandledErrorResponse;
}

@route("/Record/Stop")
namespace RecordStop {
    model VariablePayload {
        Variables: Record<string>;
    }
    @post
    op stop(@header("x-recording-id") recordingId: string, @body body: VariablePayload): {
        @statusCode statusCode: 200;
    } | BadRequestErrorResponse | UnhandledErrorResponse;
}

// when the recordingId is provided in a header on the request, the server will duplicate an existing playback
// session and return the new session's recordingId. This is part of effective perf testing.
// the body is just a string dictionary of variables, I'm pretty certain this is how I define this in typespec
@route("/Playback/Start")
namespace PlaybackStart {
    @post
    op start(@body body: StartPayload, @header("x-recording-id") recordingId: string | null): {
        @statusCode statusCode: 200;
        @header("x-recording-id") recordingId: string;
        @body body: Record<string>
    } | BadRequestErrorResponse | NotFoundErrorResponse | UnhandledErrorResponse;
}

@route("/Playback/Stop")
namespace PlaybackStop {
    @post
    op stop(@header("x-recording-id") recordingId: string): {
        @statusCode statusCode: 200;
    } | BadRequestErrorResponse | UnhandledErrorResponse;
}

// below all of the Admin/ routes, if a recordingid is provided in a header, then the server will apply whatever the customization is
// just to that session. If no recordingId is provided, then the server will apply the customization to all sessions.
// this is what i'm trying to capture with the @header("x-recording-id") recordingId: string | null typing.
@route("/Admin/SetMatcher")
namespace SetMatcher {
    enum MatcherType {
        BodilessMatcher: "BodilessMatcher",
        CustomDefaultMatcher: "CustomDefaultMatcher",
        HeaderlessMatcher: "HeaderlessMatcher",
    }

    model CustomDefaultMatcherBody {
        compareBodies?: boolean;
        excludedHeaders?: string;
        ignoredHeaders?: string;
        ignoreQueryOrdering?: boolean;
        ignoredQueryParameters?: string;
    }

    // todo: we need to only allow CustomDefaultMatcherBody when the MatcherType is CustomDefaultMatcher
    @post
    op set(@body body: CustomDefaultMatcherBody | null, @header("x-abstraction-identifier") matcherType: MatcherType): {
        @statusCode statusCode: 200;
    } | BadRequestErrorResponse | UnhandledErrorResponse;
}

@route("/Admin/AddSanitizers")
namespace AddSanitizers {
    enum SanitizerType {
        BodyKeySanitizer: "BodyKeySanitizer",
        BodyRegexSanitizer: "BodyRegexSanitizer",
        BodyStringSanitizer: "BodyStringSanitizer",
        GeneralRegexSanitizer: "GeneralRegexSanitizer",
        GeneralStringSanitizer: "GeneralStringSanitizer",
        HeaderRegexSanitizer: "HeaderRegexSanitizer",
        HeaderStringSanitizer: "HeaderStringSanitizer",
        OAuthResponseSanitizer: "OAuthResponseSanitizer",
        RegexEntrySanitizer: "RegexEntrySanitizer",
        RemoveHeaderSanitizer: "RemoveHeaderSanitizer",
        UriRegexSanitizer: "UriRegexSanitizer",
        UriStringSanitizer: "UriStringSanitizer",
        UriSubscriptionIdSanitizer: "UriSubscriptionIdSanitizer",
    }

    // todo: handle the additional HeaderCondition (where the header must match a certain value)
    model ApplyCondition {
        UriRegex: string;
    }

    @discriminator("Name")
    model SanitizerAddition {
        Name: SanitizerType;
    }

    model CommonRegexProperties {
        value?: string;
        regex?: string;
        groupForReplace?: string;
        condition?: ApplyCondition;
    }

    model BodyKeySanitizer extends SanitizerAddition {
        Name: SanitizerType.BodyKeySanitizer;
        Body: {
            jsonPath: string;
            ...CommonRegexProperties
        };
    }

    model BodyRegexSanitizer extends SanitizerAddition {
        Name: SanitizerType.BodyRegexSanitizer;
        Body: {
            ...CommonRegexProperties
        };
    }

    model BodyStringSanitizer extends SanitizerAddition {
        Name: SanitizerType.BodyStringSanitizer;
        Body:  {
            target: string;
            value?: string;
            condition?: ApplyCondition;
        };
    }

    model GeneralRegexSanitizer extends SanitizerAddition {
        Name: SanitizerType.GeneralRegexSanitizer;
        Body: {
            ...CommonRegexProperties
        };
    }

    model GeneralStringSanitizer extends SanitizerAddition {
        Name: SanitizerType.GeneralStringSanitizer;
        Body: {
            target: string;
            value?: string;
            condition?: ApplyCondition;
        };
    }

    model HeaderRegexSanitizer extends SanitizerAddition {
        Name: SanitizerType.HeaderRegexSanitizer;
        Body: {
            key: string;
            ...CommonRegexProperties
        };
    }

    model HeaderStringSanitizer extends SanitizerAddition {
        Name: SanitizerType.HeaderStringSanitizer;
        Body: {
            key: string;
            target: string;
            value?: string;
            condition?: ApplyCondition;
        };
    }

    model OAuthResponseSanitizer extends SanitizerAddition {
        Name: SanitizerType.OAuthResponseSanitizer;
    }

    enum RegexEntryValues {
        body: "body",
        header: "header",
        uri: "uri"
    }

    model RegexEntrySanitizer extends SanitizerAddition {
        Name: SanitizerType.RegexEntrySanitizer;
        Body: {
            target: RegexEntryValues;
            regex: string;
        };
    }

    model RemoveHeaderSanitizer extends SanitizerAddition {
        Name: SanitizerType.RemoveHeaderSanitizer;
        Body: {
            headersForRemoval: string;
        };
    }

    model UriRegexSanitizer extends SanitizerAddition {
        Name: SanitizerType.UriRegexSanitizer;
        Body: {
            ...CommonRegexProperties
        };
    }

    model UriStringSanitizer extends SanitizerAddition {
        Name: SanitizerType.UriStringSanitizer;
        Body: {
            target: string;
            value?: string;
            condition?: ApplyCondition;
        };
    }

    model UriSubscriptionIdSanitizer extends SanitizerAddition {
        Name: SanitizerType.UriSubscriptionIdSanitizer;
        Body: {
            value?: string;
            condition?: ApplyCondition;
        };
    }

    @post
    op add(@body sanitizers: SanitizerAddition[], @header("x-recording-id") recordingId: string | null): {
        @statusCode statusCode: 200;
    } | BadRequestErrorResponse | UnhandledErrorResponse;
}

@route("/Admin/RemoveSanitizers")
namespace RemoveSanitizers {
    model SanitizerList {
        "Sanitizers": string[];
    }

    model RemovedSanitizers {
        "Removed": string[];
    }

    @post
    op remove(@body sanitizers: SanitizerList, @header("x-recording-id") recordingId: string | null): {
        @statusCode statusCode: 200;
        @body removed: RemovedSanitizers
    } | BadRequestErrorResponse | UnhandledErrorResponse;
}

@route("/Admin/SetRecordingOptions")
namespace SetRecordingOptions {
    enum StoreType {
        GitStore: "GitStore"
    }

    model Certificate {
        PemValue: string;
        PemKey: string;
    }

    model TransportCustomizations {
        AllowAutoRedirect?: boolean;
        TLSValidationCert?: string;
        TLSValidationCertHost?: string;
        Certificates?: Certificate[];
        PlaybackResponseTime?: int32;
    }

    model RecordingOptions {
        HandleRedirects?: boolean;
        ContextDirectory?: string;
        AssetsStore?: StoreType;
        Transport?: TransportCustomizations;
    }

    @post
    op set(@body body: RecordingOptions, @header("x-recording-id") recordingId: string | null): {
        @statusCode statusCode: 200;
    } | BadRequestErrorResponse | UnhandledErrorResponse;
}