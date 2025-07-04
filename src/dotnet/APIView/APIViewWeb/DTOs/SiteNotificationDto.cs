using APIViewWeb.Models;

namespace APIViewWeb.DTOs;

public class SiteNotificationDto
{
    public string ReviewId { get; set; }
    public string RevisionId { get; set; }
    public string Title { get; set; }
    public string Summary { get; set; }
    public string Message { get; set; }
    public string Status { get; set; }
}

public static class SiteNotificationStatus
{
    public const string Info = "info";
    public const string Success = "success";
    public const string Error = "error";
    public const string Warning = "warning";
}
