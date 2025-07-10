using APIViewWeb.DTOs;

namespace APIViewWeb.Helpers;

public static class NotificationHelpers
{
    public static SiteNotificationDto GetSiteNotificationForReview(
        SiteNotificationDto siteNotification,
        string jobId,
        string host,
        int generatedCommentCount,
        string errorDetails = "")
    {
        if (siteNotification == null)
        {
            return null;
        }

        return siteNotification.Status switch
        {
            SiteNotificationStatus.Success => CreateSuccessReviewNotification(siteNotification, jobId, host,
                generatedCommentCount),
            SiteNotificationStatus.Error => CreateErrorReviewNotification(siteNotification, jobId, errorDetails),
            _ => null
        };
    }

    private static SiteNotificationDto CreateSuccessReviewNotification(
        SiteNotificationDto siteNotification,
        string jobId,
        string host,
        int generatedCommentCount)
    {
        string pageUrl = $"{host}/review/{siteNotification.ReviewId}?activeApiRevisionId={siteNotification.RevisionId}";
        string commentLabel = generatedCommentCount == 1 ? "comment" : "comments";
        string message =
            $"Copilot generated {generatedCommentCount} {commentLabel}.<br/>Job Id: {jobId}<br/><a href=\"{pageUrl}\" target=\"_blank\">View Review</a>";

        return new SiteNotificationDto
        {
            ReviewId = siteNotification.ReviewId,
            RevisionId = siteNotification.RevisionId,
            Summary = siteNotification.Summary,
            Message = message,
            Status = siteNotification.Status,
            ToastNotification = new ToastNotificationDto
            {
                Title = "Copilot Comments Available",
                Message = $"Copilot generated {generatedCommentCount} {commentLabel}.",
                Action = generatedCommentCount > 0
                    ? SiteNotificationAction.RefreshPage
                    : SiteNotificationAction.None
            }
        };
    }

    private static SiteNotificationDto CreateErrorReviewNotification(
        SiteNotificationDto siteNotification,
        string jobId,
        string errorDetails)
    {
        string message = $"Failed to generate copilot review. Job Id: {jobId}";
        return new SiteNotificationDto
        {
            ReviewId = siteNotification.ReviewId,
            RevisionId = siteNotification.RevisionId,
            Summary = siteNotification.Summary,
            Message = message + errorDetails,
            Status = siteNotification.Status,
            ToastNotification = new ToastNotificationDto
            {
                Title = "Copilot Comments Failed",
                Message = "Failed to generate copilot review.",
                Action = SiteNotificationAction.None
            }
        };
    }
}
