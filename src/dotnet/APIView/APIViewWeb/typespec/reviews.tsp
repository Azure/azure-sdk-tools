import "@typespec/http";
import "./models.tsp";

using TypeSpec.Http;

namespace APIView;

@route("/api/reviews")
@useAuth(BearerAuth)
namespace Reviews {
    /**
     * Result of resolving a review/revision from various inputs.
     * Contains only IDs that should be cached for subsequent API calls.
     */
    model RevisionResolveResult {
        /** The unique identifier of the review */
        reviewId: string;
        /** The unique identifier of the resolved revision. Use this for subsequent API calls. */
        revisionId: string;
    }

    /**
     * Metadata about an API revision
     */
    model RevisionMetadata {
        /** The unique identifier of the API revision */
        revisionId: string;
        /** The version of the package for this revision */
        packageVersion?: string;
        /** Whether this specific revision is approved */
        isApproved: boolean;
        /** The pull request number associated with this revision, if any */
        pullRequestNo?: int32;
        /** Direct link to view this revision in APIView */
        revisionLink: string;
        /** The user who created this revision */
        createdBy: string;
        /** The date and time when this revision was created */
        createdOn: utcDateTime;
    }

    /**
     * Full metadata about a review and its revision
     */
    model ReviewMetadata {
        /** The unique identifier of the review */
        reviewId: string;
        /** The name of the package being reviewed */
        packageName: string;
        /** The programming language of the package */
        language: string;
        /** Whether the review (package name) is approved */
        isApproved: boolean;
        /** The user who created the review */
        createdBy: string;
        /** The date and time when the review was created */
        createdOn: utcDateTime;
        /** The date and time when the review was last updated */
        lastUpdatedOn: utcDateTime;
        /** Information about the API revision */
        revision: RevisionMetadata;
    }

    /**
     * Resolve a review/revision from various input types.
     * Returns IDs that can be cached and used for subsequent API calls.
     * 
     * Supports:
     * - Package name + language (+ optional version)
     * - APIView URL (review or revision link)
     * 
     * @param packageName Package name (e.g., "Azure.Storage.Blobs"). Requires language.
     * @param language Programming language (e.g., "C#", "Python"). Requires packageName.
     * @param version Optional package version. If not specified, resolves to latest revision.
     * @param link APIView URL (e.g., "https://spa.apiview.dev/review/{id}?activeApiRevisionId={revId}")
     * @returns Resolved IDs for use in subsequent API calls.
     */
    @get
    @route("/resolve")
    op resolve(
        @query packageName?: string,
        @query language?: string,
        @query version?: string,
        @query link?: string
    ): {
        @statusCode statusCode: 200;
        @body result: RevisionResolveResult;
    } | BadRequestErrorResponse | NotFoundErrorResponse | InternalServerErrorResponse;

    /**
     * Get full metadata for a review and revision.
     * Requires a revisionId (obtained from the /resolve endpoint).
     * 
     * @param revisionId The API revision ID (from /resolve endpoint).
     * @returns Full review and revision metadata.
     */
    @get
    @route("/metadata")
    op getMetadata(
        @query revisionId: string
    ): {
        @statusCode statusCode: 200;
        @body metadata: ReviewMetadata;
    } | BadRequestErrorResponse | NotFoundErrorResponse | InternalServerErrorResponse;
}
