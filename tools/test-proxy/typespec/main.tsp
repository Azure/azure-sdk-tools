import "@typespec/http";
import "./models.tsp";

using TypeSpec.Http;

@service(#{
    title: "Test-Proxy Record/Playback server"
})
@server("https://localhost:5001/", "The test-proxy default URL.")

namespace TestProxy;

@doc("Start recording for a test.")
@route("/Record/Start")
@post
op StartRecord(
    @doc("File location of the recording.")
    @body body: TestProxyStartInformation
    ): {
    @statusCode statusCode: 200;
    @doc("The recording ID.")
    @header("x-recording-id") recordingId: string;
} | BadRequestErrorResponse | NotFoundErrorResponse | UnhandledErrorResponse;

@doc("Stop recording for a test.")
@route("/Record/Stop")
@post
op StopRecord(
    @doc("The recording ID.")
    @header("x-recording-id")
    recordingId: string,
    @doc("Set to request-response to skip recording this session.")
    @header("x-recording-skip")
    recordingSkip?: string,
    @body variables: Record<string>
    ): {
    @statusCode statusCode: 200;
} | BadRequestErrorResponse | UnhandledErrorResponse;

// when the recordingId is provided in a header on the request, the server will duplicate an existing playback
// session and return the new session's recordingId. This is part of effective perf testing.
@doc("Start playback for a test recording.")
@route("/Playback/Start")
@post
op StartPlayback(
    @doc("File location of the recording.")
    @body body: TestProxyStartInformation,
    @header("x-recording-id") recordingId: string | null
    ): {
    @statusCode statusCode: 200;
    @doc("The recording ID.")
    @header("x-recording-id") recordingId: string;
    @body response: Record<string>
} | BadRequestErrorResponse | NotFoundErrorResponse | UnhandledErrorResponse;

@doc("Stop playback for a test recording.")
@route("/Playback/Stop")
@post
op StopPlayback(
    @doc("The recording ID.")
    @header("x-recording-id") recordingId: string): {
    @statusCode statusCode: 200;
} | BadRequestErrorResponse | UnhandledErrorResponse;

// below all of the Admin/ routes, if a recordingid is provided in a header, then the server will apply whatever the customization is
// just to that session. If no recordingId is provided, then the server will apply the customization to all sessions.
// this is what i'm trying to capture with the @header("x-recording-id") recordingId: string | null typing.
namespace Admin {
    @route("/Admin/SetMatcher")
    // todo: we need to only allow CustomDefaultMatcherBody when the MatcherType is CustomDefaultMatcher
    @post
    op SetMatcher(@body body: CustomDefaultMatcherBody | null, @header("x-abstraction-identifier") matcherType: MatcherType): {
        @statusCode statusCode: 200;
    } | BadRequestErrorResponse | UnhandledErrorResponse;

    @route("/Admin/AddSanitizers")
    @post
    op AddSanitizers(@body sanitizers: SanitizerAddition[], @header("x-recording-id") recordingId: string | null): {
        @statusCode statusCode: 200;
    } | BadRequestErrorResponse | UnhandledErrorResponse;

    @route("/Admin/RemoveSanitizers")
    @post
    op RemoveSanitizers(@body sanitizers: SanitizerList, @header("x-recording-id") recordingId: string | null): {
        @statusCode statusCode: 200;
        @body removed: RemovedSanitizers
    } | BadRequestErrorResponse | UnhandledErrorResponse;

    @route("/Admin/SetRecordingOptions")
    @post
    op SetRecordingOptions(@body body: RecordingOptions, @header("x-recording-id") recordingId: string | null): {
        @statusCode statusCode: 200;
    } | BadRequestErrorResponse | UnhandledErrorResponse;
}