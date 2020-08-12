using Azure.Sdk.Tools.GitHubIssues.Reports;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GitHubIssues.Helpers
{
    public class TableCreator
    {
        private string _header;
        public TableCreator(string header)
        {
            _header = header;
        }

        public static class Templates
        {
            public static readonly Func<ReportIssue, string> Title = i => $"<a href=\"{i.Issue.HtmlUrl}\">{i.Issue.Title}</a>";
            public static readonly Func<ReportIssue, string> Labels = i => string.Join(',', i.Issue.Labels.Select(l => l.Name));
            public static readonly Func<ReportIssue, string> Author = i => i.Issue.User.Login;
            public static readonly Func<ReportIssue, string> Assigned = i => i.Issue.Assignee?.Login;
            public static readonly Func<ReportIssue, string> Milestone = i => $"<a href=\"{i.Milestone?.HtmlUrl}\">{i.Milestone?.Title}</a>";
        }

        private Dictionary<string, Func<ReportIssue, string>> _formatActions = new Dictionary<string, Func<ReportIssue, string>>();

        public void DefineTableColumn(string header, Func<ReportIssue, string> action) => _formatActions.Add(header, action);

        /// <summary>
        /// Creates an html representation of the list
        /// </summary>
        /// <param name="issues"></param>
        /// <returns></returns>
        public string GetContent(IEnumerable<ReportIssue> issues)
        {
            string _headerRow = CreateHeaderRow(_formatActions.Keys);
            string _templateRow = CreateTemplateRow(_formatActions.Keys.Count);

            StringBuilder formattedTable = new StringBuilder();
            formattedTable.Append($"<h2>{_header}</h2>");
            formattedTable.Append($"<p>Found {issues.Count()} issue(s).</p>");

            // add a scrollable div around the table
            formattedTable.Append(@"<div style=""overflow-x:auto;"">");  // start-scrollable-div
            formattedTable.Append("<table>"); //start-table
            formattedTable.Append(_headerRow);
            foreach (ReportIssue issue in issues)
            {
                List<string> args = new List<string>();
                foreach (string header in _formatActions.Keys)
                {
                    string valueForHeader = _formatActions[header](issue);
                    args.Add(valueForHeader);
                }

                formattedTable.AppendFormat(_templateRow, args.ToArray());
            }

            formattedTable.Append("</table>"); // end-table
            formattedTable.Append("</div>"); // end-scrollable-div

            return formattedTable.ToString();
        }

        private static string CreateHeaderRow(IEnumerable<string> headers)
        {
            StringBuilder headerRow = new StringBuilder();
            headerRow.Append("<tr>");
            foreach (string header in headers)
            {
                headerRow.Append("<th>");
                headerRow.Append(header);
                headerRow.Append("</th>");
            }
            headerRow.Append("</tr>");
            return headerRow.ToString();
        }

        private static string CreateTemplateRow(int columnCount)
        {
            StringBuilder headerRow = new StringBuilder();
            headerRow.Append("<tr>");
            for (int i = 0; i < columnCount; i++)
            {
                headerRow.Append("<td>{");
                headerRow.Append(i);
                headerRow.Append("}</td>");
            }
            headerRow.Append("</tr>");
            return headerRow.ToString();
        }
    }
}
