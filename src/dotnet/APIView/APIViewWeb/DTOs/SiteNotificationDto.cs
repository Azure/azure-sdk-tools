using System;
using APIViewWeb.Models;

namespace APIViewWeb.DTOs;

public class SiteNotificationDto
{
    public string ReviewId { get; set; }
    public string RevisionId { get; set; }
    public string Title { get; set; }
    public string Summary { get; set; }
    public string Message { get; set; }

    private string _status;
    public string Status
    {
        get => _status;
        set => _status = NormalizeStatus(value);
    }

    private static string NormalizeStatus(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Status cannot be null or empty.");

        value = value.Trim().ToLowerInvariant();
        return value switch
        {
            SiteNotificationStatus.Info => SiteNotificationStatus.Info,
            SiteNotificationStatus.Success => SiteNotificationStatus.Success,
            SiteNotificationStatus.Error => SiteNotificationStatus.Error,
            SiteNotificationStatus.Warning => SiteNotificationStatus.Warning,
            _ => throw new ArgumentException($"Invalid status value: {value}")
        };
    }
}

public static class SiteNotificationStatus
{
    public const string Info = "info";
    public const string Success = "success";
    public const string Error = "error";
    public const string Warning = "warning";
}
