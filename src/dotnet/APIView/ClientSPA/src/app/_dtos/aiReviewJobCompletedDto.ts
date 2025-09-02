
export type AIReviewJobStatus = 'Success' | 'Error';
export interface AIReviewJobCompletedDto {
    reviewId : string;
    apirevisionId: string;
    status: AIReviewJobStatus;
    details: string;
    createdBy: string;
    noOfGeneratedComments : number;
    jobId: string;
}