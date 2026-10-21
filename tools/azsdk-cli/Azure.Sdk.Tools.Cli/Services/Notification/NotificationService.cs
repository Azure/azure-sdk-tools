// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Sdk.Tools.Cli.Configuration;
using Azure.Sdk.Tools.Cli.Helpers;

namespace Azure.Sdk.Tools.Cli.Services.Notification
{
    public enum NotificationStatus
    {
        Sent,
        Disabled,
        SkippedNoRecipients,
        Failed
    }

    public record NotificationResult(NotificationStatus Status, string? ErrorMessage = null)
    {
        public static NotificationResult Sent { get; } = new(NotificationStatus.Sent);

        public bool IsFailure => Status == NotificationStatus.Failed;
    }

    public interface INotificationService
    {
        /// <summary>
        /// Sends the email described by <paramref name="payload"/>.
        /// Returns the delivery outcome so callers can surface failures. Notifications that are
        /// disabled or have no valid recipients are reported as skipped outcomes, not failures.
        /// </summary>
        Task<NotificationResult> SendEmailNotificationAsync(EmailPayload payload, CancellationToken ct = default);
    }

    public class NotificationService(
        IHttpClientFactory httpClientFactory,
        IEnvironmentHelper environmentHelper,
        ILogger<NotificationService> logger) : INotificationService
    {
        private const string MICROSOFT_EMAIL = "@microsoft.com";
        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public async Task<NotificationResult> SendEmailNotificationAsync(EmailPayload payload, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(payload);

            if (!IsNotificationEnabled(out var serviceUrl))
            {
                return new NotificationResult(NotificationStatus.Disabled);
            }

            payload.EmailTo = NormalizeRecipients(payload.EmailTo);
            payload.CC = NormalizeRecipients(payload.CC);

            if (payload.EmailTo.Count == 0)
            {
                logger.LogWarning("Email notification has no valid recipients. Skipping notification.");
                return new NotificationResult(NotificationStatus.SkippedNoRecipients);
            }

            try
            {
                var httpClient = httpClientFactory.CreateClient();
                using var content = new StringContent(
                    JsonSerializer.Serialize(payload, SerializerOptions), Encoding.UTF8, "application/json");
                using var response = await httpClient.PostAsync(serviceUrl, content, ct);

                if (!response.IsSuccessStatusCode)
                {
                    var responseBody = await response.Content.ReadAsStringAsync(ct);
                    logger.LogWarning(
                        "Failed to send email notification. Status: {StatusCode}. Response: {Response}",
                        response.StatusCode, responseBody);
                    return new NotificationResult(
                        NotificationStatus.Failed,
                        $"Notification service returned HTTP {(int)response.StatusCode} ({response.StatusCode}). Response: {responseBody}");
                }

                logger.LogInformation("Email notification sent to {Recipients}.", string.Join(", ", payload.EmailTo));
                return NotificationResult.Sent;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "An error occurred while sending the email notification.");
                return new NotificationResult(NotificationStatus.Failed, ex.Message);
            }
        }

        private bool IsNotificationEnabled(out string serviceUrl)
        {
            serviceUrl = environmentHelper.GetStringVariable(Constants.NOTIFICATION_SERVICE_URL_ENV_VAR);

            if (string.IsNullOrWhiteSpace(serviceUrl))
            {
                logger.LogDebug(
                    "Notification service URL ({EnvVar}) is not configured. Skipping notification.",
                    Constants.NOTIFICATION_SERVICE_URL_ENV_VAR);
                return false;
            }

            return true;
        }

        private static List<string> NormalizeRecipients(IEnumerable<string>? recipients) =>
            recipients?
                .Select(r => r?.Trim())
                .Where(r => !string.IsNullOrWhiteSpace(r) && r.EndsWith(MICROSOFT_EMAIL, StringComparison.OrdinalIgnoreCase))
                .Select(r => r!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? [];
    }
}
