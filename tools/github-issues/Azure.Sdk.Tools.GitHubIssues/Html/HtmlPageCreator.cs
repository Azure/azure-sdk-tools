using System.Text;

namespace GitHubIssues.Html
{
    public class HtmlPageCreator
    {
        private readonly string _title;
        public HtmlPageCreator(string title)
        {
            _title = title;
        }

        private readonly StringBuilder _content = new StringBuilder();

        private const string Header = @"<!DOCTYPE html>
<html lang=""en"" xmlns=""http://www.w3.org/1999/xhtml"">
<head>
    <meta charset = ""utf-8"" />
    <title>##title##</title>
    <style>##css##</style>
</head>
<body>";

        private const string Footer = @"</body>
</html>";

        private const string CSS = @"
table {
  font-family: ""Calibri"";
  border-collapse: collapse;
  width: 100%;
}

th, td {
  text-align: left;
  padding: 8px;
}

tr:nth-child(even){background-color: #e5f1f9}

th {
  background-color: #0078ca;
  color: white;
}
";


        public void AddContent(string content)
        {
            _content.Append(content);
        }

        public string GetContent()
        {
            StringBuilder email = new StringBuilder();
            email.Append(Header);
            email.Replace("##title##", _title);
            email.Replace("##css##", CSS);

            email.Append(_content);

            email.Append(Footer);
            return email.ToString();
        }
    }
}
