import "@typespec/http";
import "./models.tsp";

using TypeSpec.Http;

namespace APIView;

/**
 * Automatic API Review endpoints
 * These endpoints require API Key authentication via the 'ApiKey' header.
 */
@route("/AutoReview")
namespace AutoReview {
    /**
     * Upload an automatic API review file
     * @param file The API review file to upload
     * @param label Label for the revision
     * @param compareAllRevisions Whether to compare against all revisions
     * @param packageVersion Version of the package
     * @param setReleaseTag Whether to set release tag
     * @param packageType Type of the package
     * @returns URL of the created/updated review
     */
    @post
    @route("/UploadAutoReview")
    op UploadAutoReview(
        @header contentType: "multipart/form-data",
        @multipartBody body: {
            file: HttpPart<bytes>;
        },
        @query label: string,
        @query compareAllRevisions?: boolean = false,
        @query packageVersion?: string,
        @query setReleaseTag?: boolean = false,
        @query packageType?: string
    ): {
        @statusCode statusCode: 200 | 201 | 202;
        @body reviewUrl: string;
    } | UnauthorizedErrorResponse | InternalServerErrorResponse;

    /**
     * Get the approval status of an API review revision
     * @param language Language of the package
     * @param packageName Name of the package
     * @param reviewId Optional review ID
     * @param firstReleaseStatusOnly Whether to check first release status only
     * @param packageVersion Optional package version
     * @returns Status code indicating approval state: 200 (approved), 201 (package approved), 202 (not approved)
     */
    @get
    @route("/GetReviewStatus")
    op GetReviewStatus(
        @query language: string,
        @query packageName: string,
        @query reviewId?: string,
        @query firstReleaseStatusOnly?: boolean,
        @query packageVersion?: string
    ): {
        @statusCode statusCode: 200 | 201 | 202;
    } | NotFoundErrorResponse | UnauthorizedErrorResponse | InternalServerErrorResponse;

    /**
     * Create an API review from Azure DevOps pipeline artifacts
     * @param buildId Build ID in Azure DevOps
     * @param artifactName Name of the artifact
     * @param originalFilePath Path to original file
     * @param reviewFilePath Path to review file
     * @param label Label for the revision
     * @param repoName Repository name
     * @param packageName Package name
     * @param compareAllRevisions Whether to compare against all revisions
     * @param project Azure DevOps project name
     * @param packageVersion Version of the package
     * @param setReleaseTag Whether to set release tag
     * @param packageType Type of the package
     * @returns URL of the created/updated review
     */
    @get
    @route("/CreateApiReview")
    op CreateApiReview(
        @query buildId: string,
        @query artifactName: string,
        @query originalFilePath: string,
        @query reviewFilePath: string,
        @query label: string,
        @query repoName: string,
        @query packageName: string,
        @query compareAllRevisions: boolean,
        @query project: string,
        @query packageVersion?: string,
        @query setReleaseTag?: boolean = false,
        @query packageType?: string
    ): {
        @statusCode statusCode: 200 | 201 | 202;
        @body reviewUrl: string;
    } | {
        @statusCode statusCode: 204;
        @body message: string;
    } | UnauthorizedErrorResponse | InternalServerErrorResponse;
}
