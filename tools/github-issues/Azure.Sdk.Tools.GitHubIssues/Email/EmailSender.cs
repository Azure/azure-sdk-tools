using Microsoft.Extensions.Logging;
using SendGrid;
using System.IO;

namespace Azure.Sdk.Tools.GitHubIssues.Email
{
    internal static class EmailSender
    {
        public static void SendEmail(string emailToken, string from, string template, string[] to, string[] cc, string title, ILogger log)
        {
            log.LogInformation($"Sending email from {from} with title: {title}");

            SendGrid.SendGridClient client = new SendGrid.SendGridClient(emailToken);

            SendGrid.Helpers.Mail.SendGridMessage message = new SendGrid.Helpers.Mail.SendGridMessage();
            message.SetFrom(from);

            foreach (string item in to)
            {
                if (!string.IsNullOrEmpty(item))
                {
                    message.AddTo(item);
                }
            }

            foreach (string item in cc)
            {
                if (!string.IsNullOrEmpty(item))
                {
                    message.AddCc(item);
                }
            }

            message.SetSubject($"GitHub Report: {title}");
            message.AddContent(MimeType.Html, template);

#if !DEBUG
            var emailResult = client.SendEmailAsync(message).GetAwaiter().GetResult();
#else
            File.WriteAllText("output.html", template);
#endif
        }
    }
}
