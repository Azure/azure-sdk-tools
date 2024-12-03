import "@typespec/http";
import "@typespec/rest";
import "@typespec/openapi3";

using TypeSpec.Http;

@service({
    title: "Test-Proxy Record/Playback server"
})
@server("https://localhost:5001/", "The test-proxy default URL.")

namespace TestProxy;

model StartPayload {
    "x-recording-file"?: string;
    "x-recording-assets-file"?: string;
}

@route("/Record/Start")
namespace RecordStart {
    @post
    op start(@body body: StartPayload): {
        @statusCode statusCode: 200;
        @header("x-recording-id") recordingId: string;
    };
}

@route("/Record/Stop")
namespace RecordStop {
    model VariablePayload {
        Variables: Record<string>;
    }
    @post
    op stop(@header("x-recording-id") recordingId: string, @body body: VariablePayload): {
        @statusCode statusCode: 200;
    };
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
    };
}

@route("/Playback/Stop")
namespace PlaybackStop {
    @post
    op stop(@header("x-recording-id") recordingId: string): {
        @statusCode statusCode: 200;
    };
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

    // we need to only allow CustomDefaultMatcherBody when the MatcherType is CustomDefaultMatcher
    @post
    op set(@body body: CustomDefaultMatcherBody | null, @header("x-abstraction-identifier") matcherType: MatcherType): {
        @statusCode statusCode: 200;
    };
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

    model HeaderCondition {
        Key: string;
    }

    model ApplyCondition {
        UriRegex: string;
        HeaderCondition: HeaderCondition;
    }

    model SanitizerAdditionBody {
        key?: string;
        value?: string;
        regex?: string;
        groupForReplace?: string;
        condition?: ApplyCondition;
    }

    // how to strongly tie Body type to SanitizerType? That's the goal here.
    model SanitizerAddition {
        Name: SanitizerType;
        Body: SanitizerAdditionBody;
    }

    @post
    op add(@body sanitizers: SanitizerAddition[], @header("x-recording-id") recordingId: string | null): {
        @statusCode statusCode: 200;
    };
}

@route("/Admin/AddSanitizer")
namespace AddSanitizer {
    model SanitizerAddition {
        Name: AddSanitizers.SanitizerType;
        Body: AddSanitizers.SanitizerAdditionBody;
    }

    @post
    op add(@body sanitizer: AddSanitizers.SanitizerAdditionBody, @header("x-abstraction-identifier") sanitizerType: AddSanitizers.SanitizerType): {
        @statusCode statusCode: 200;
    };
}

@route("/Admin/RemoveSanitizers")
namespace RemoveSanitizers {
    model SanitizerList {
        "Sanitizers": string[];
    }

    @post
    op remove(@body sanitizers: SanitizerList, @header("x-recording-id") recordingId: string | null): {
        @statusCode statusCode: 200;
        @body removed: SanitizerList
    };
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
    };
}