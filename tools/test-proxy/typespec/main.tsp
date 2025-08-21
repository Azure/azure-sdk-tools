import "@typespec/http";
import "./models.tsp";

using TypeSpec.Http;

@service(#{
    title: "Test-Proxy Record/Playback server"
})
@server("https://localhost:5001/", "The test-proxy default URL.")

namespace TestProxy;

/**
 * Start recording for a test.
 * @param body File location of the recording.
 * @param recordingId The recording ID.
 */
@route("/Record/Start")
@post
op StartRecord(@body body: TestProxyStartInformation): {
    @statusCode statusCode: 200;
    @header("x-recording-id") recordingId: string;
} | BadRequestErrorResponse | NotFoundErrorResponse | UnhandledErrorResponse;

/**
 * Stop recording for a test.
 * @param recordingId The recording ID.
 * @param recordingSkip Optional header that can be set to request-response to skip recording this session.
 * @param variables A set of variables for the recording session.
 */
@route("/Record/Stop")
@post
op StopRecord(@header("x-recording-id") recordingId: string, @header("x-recording-skip") recordingSkip?: string, @body variables: Record<string>): {
    @statusCode statusCode: 200;
} | BadRequestErrorResponse | UnhandledErrorResponse;

/**
 * Start playback for a test recording.
 * @param body File location of the recording.
 * @param recordingId The recording ID. If provided, the server will duplicate an existing playback session and return the new session's recordingId.
 */
@route("/Playback/Start")
@post
op StartPlayback(@body body: TestProxyStartInformation, @header("x-recording-id") recordingId?: string): {
    @statusCode statusCode: 200;
    @header("x-recording-id") recordingId: string;
    @body response: Record<string>
} | BadRequestErrorResponse | NotFoundErrorResponse | UnhandledErrorResponse;

/**
 * Stop playback for a test recording.
 * @param recordingId The recording ID.
 */
@route("/Playback/Stop")
@post
op StopPlayback(@header("x-recording-id") recordingId: string): {
    @statusCode statusCode: 200;
} | BadRequestErrorResponse | UnhandledErrorResponse;

namespace Admin {
    /**
     * Set the matcher for the test proxy. If a recording ID is provided in the header,
     * the matcher will be set only for that session. Otherwise, it will be set globally.
     * @param matcherType The type of matcher to set.
     * @param matcher The matcher configuration. Only required if matcherType is CustomDefaultMatcher.
     */
    @route("/Admin/SetMatcher")
    @post
    op SetMatcher(@header("x-abstraction-identifier") matcherType: MatcherType, @body matcher?: CustomDefaultMatcher): {
        @statusCode statusCode: 200;
    } | BadRequestErrorResponse | UnhandledErrorResponse;

    /**
     * Add sanitizers to the test proxy. If a recording ID is provided in the header,
     * the sanitizers will be added only for that session. Otherwise, they will be added globally.
     */
    @route("/Admin/AddSanitizers")
    @post
    op AddSanitizers(@body sanitizers: SanitizerAddition[], @header("x-recording-id") recordingId?: string): {
        @statusCode statusCode: 200;
    } | BadRequestErrorResponse | UnhandledErrorResponse;

    /**
     * Remove sanitizers from the test proxy. If a recording ID is provided in the header,
     * the sanitizers will be removed only for that session. Otherwise, they will be removed globally.
     */
    @route("/Admin/RemoveSanitizers")
    @post
    op RemoveSanitizers(@body sanitizers: SanitizerList, @header("x-recording-id") recordingId?: string): {
        @statusCode statusCode: 200;
        @body removed: RemovedSanitizers
    } | BadRequestErrorResponse | UnhandledErrorResponse;

    /**
     * Set recording options for the test proxy. If a recording ID is provided in the header,
     * the options will be set only for that session. Otherwise, they will be set globally.
     */
    @route("/Admin/SetRecordingOptions")
    @post
    op SetRecordingOptions(@body body: RecordingOptions, @header("x-recording-id") recordingId?: string): {
        @statusCode statusCode: 200;
    } | BadRequestErrorResponse | UnhandledErrorResponse;
}