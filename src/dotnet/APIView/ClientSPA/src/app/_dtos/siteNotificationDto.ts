export enum SiteNotificationStatus {
    Success = 'success',
    Info = 'info',
    Warning = 'warning',
    Error = 'error'
}

export enum SiteNotificationType {
    CopilotReviewCompleted = 'CopilotReviewCompleted',
}

export enum SiteNotificationAction {
    None = 'None',
    RefreshPage = 'RefreshPage',
}

export interface SiteNotificationDto {
    reviewId: string;
    revisionId: string;
    title: string;
    summary: string;
    message: string;
    status: SiteNotificationStatus;
    type: SiteNotificationType;
    toastNotification: ToastNotificationDto;
}

export interface ToastNotificationDto {
    message: string;
    title: string;
    action: SiteNotificationAction;
}