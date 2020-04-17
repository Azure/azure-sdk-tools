using SendGrid;

namespace GitHubIssues
{
    internal static class EmailSender
    {
        public static void SendEmail(string emailToken, string template, string from, string[] to, string[] cc, string title)
        {
            SendGrid.SendGridClient client = new SendGrid.SendGridClient(emailToken);

            SendGrid.Helpers.Mail.SendGridMessage message = new SendGrid.Helpers.Mail.SendGridMessage();
            message.SetFrom(from);

            foreach (var item in to)
            {
                if (!string.IsNullOrEmpty(item))
                {
                    message.AddTo(item);
                }
            }

            foreach (var item in cc)
            {
                if (!string.IsNullOrEmpty(item))
                {
                    message.AddCc(item);
                }
            }

            message.SetSubject($"GitHub Report: {title}");
            message.AddContent(MimeType.Html, template);


#if !DEBUG
            // Don't accidentally send email
            var emailResult = client.SendEmailAsync(message).GetAwaiter().GetResult();
#else
            System.IO.File.WriteAllText("output.html", template);
#endif
        }
    }
}
