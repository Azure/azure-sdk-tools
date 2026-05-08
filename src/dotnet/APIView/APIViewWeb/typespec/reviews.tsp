import "@typespec/http";
import "./models.tsp";

using TypeSpec.Http;

namespace APIView;

@route("/api/v1/reviews")
@useAuth(BearerAuth)
namespace Reviews {
    /**
     * TODO: Is this really necessary? 
     * List and search reviews (packages).
     * Returns a paginated list of reviews matching the specified filters.
     * (#15407)
     *
     * @param language Filter by programming language (e.g., "Python", "C#").
     * @param packageName Filter by package name (substring match).
     * @param isApproved Filter by approval status.
     * @param isClosed Filter by closed status.
     * @param noOfItemsRead Continuation offset — number of items already read. Default 0.
     * @param pageSize Number of items per page (max 200). Default 50.
     * @returns Paginated list of matching reviews.
     */
    @get
    @route("/list")
    op listReviews(
        @query language?: string,
        @query packageName?: string,
        @query isApproved?: boolean,
        @query isClosed?: boolean,
        @query noOfItemsRead?: int32 = 0,
        @query pageSize?: int32 = 50
    ): {
        @statusCode statusCode: 200;
        @body result: ReviewListResponse;
    } | BadRequestErrorResponse | InternalServerErrorResponse;

    /**
     * Start an automated API review job via the AVC (API View Copilot) service.
     * The target text can be raw API surface text or wrapped in a markdown code block
     * (fences are stripped automatically and the language tag is used if language is not provided).
     *
     * @param request The review job request body.
     * @returns A job ID that can be used to poll for results.
     */
    @post
    @route("/start-copilot-review-job")
    op startCopilotReviewJob(
        @body request: StartCopilotReviewJobRequest
    ): {
        @statusCode statusCode: 202;
        @body result: CopilotReviewJobStartedResponse;
    } | BadRequestErrorResponse | InternalServerErrorResponse;

    /**
     * Get the status or result of a copilot review job.
     * Poll this endpoint with the job ID returned from start-copilot-review-job.
     * When the job completes successfully, the response includes the generated review comments.
     *
     * @param jobId The unique job identifier (obtained from start-copilot-review-job).
     * @returns The current job status and, when complete, the review comments.
     */
    @get
    @route("/get-copilot-review-job/{jobId}")
    op getCopilotReviewJob(
        @path jobId: string
    ): {
        @statusCode statusCode: 200;
        @body result: CopilotReviewJobResponse;
    } | BadRequestErrorResponse | InternalServerErrorResponse;
}
