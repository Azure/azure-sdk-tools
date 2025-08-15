import "@typespec/http";

using TypeSpec.Http;

namespace TestProxy;

model TestProxyStartInformation {
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

namespace Admin {
    enum MatcherType {
        BodilessMatcher: "BodilessMatcher",
        CustomDefaultMatcher: "CustomDefaultMatcher",
        HeaderlessMatcher: "HeaderlessMatcher",
    }

    model CustomDefaultMatcher {
        compareBodies?: boolean;
        excludedHeaders?: string;
        ignoredHeaders?: string;
        ignoreQueryOrdering?: boolean;
        ignoredQueryParameters?: string;
    }

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

    model SanitizerList {
        "Sanitizers": string[];
    }

    model RemovedSanitizers {
        "Removed": string[];
    }

    enum StoreType {
        GitStore: "GitStore"
    }

    model TestProxyCertificate {
        PemValue: string;
        PemKey: string;
    }

    model TransportCustomizations {
        AllowAutoRedirect?: boolean;
        TLSValidationCert?: string;
        TLSValidationCertHost?: string;
        Certificates?: TestProxyCertificate[];
        PlaybackResponseTime?: int32;
    }

    model RecordingOptions {
        HandleRedirects?: boolean;
        ContextDirectory?: string;
        AssetsStore?: StoreType;
        Transport?: TransportCustomizations;
    }
}