export enum SiteNotificationStatus {
    Success = 'success',
    Info = 'info',
    Warning = 'warning',
    Error = 'error'
}

export interface SiteNotificationDto {
    reviewId : string;
    revisionId: string;
    title: string;
    summary: string;
    message: string;
    status: SiteNotificationStatus;
}