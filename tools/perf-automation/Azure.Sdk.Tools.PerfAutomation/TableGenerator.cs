using Azure.Sdk.Tools.PerfAutomation.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Azure.Sdk.Tools.PerfAutomation
{
    public static class TableGenerator
    {
        public static string Generate(IList<string> headers, IList<IList<IList<string>>> table, OutputFormat outputFormat)
        {
            var columns = table.SelectMany(rowSet => rowSet).Prepend(headers);

            var columnWidths = new List<int>(new int[columns.First().Count()]);
            foreach (var row in columns)
            {
                for (var i = 0; i < row.Count; i++)
                {
                    columnWidths[i] = Math.Max(columnWidths[i], row.ElementAt(i).Length);
                }
            }

            var sb = new StringBuilder();

            if (outputFormat == OutputFormat.Txt)
            {
                WriteHorizontalLine(sb, columnWidths);
            }

            WriteRow(sb, columnWidths, headers, outputFormat);

            if (outputFormat == OutputFormat.Txt || outputFormat == OutputFormat.Md)
            {
                WriteHorizontalLine(sb, columnWidths);
            }

            foreach (var rowSet in table)
            {
                foreach (var row in rowSet)
                {
                    WriteRow(sb, columnWidths, row, outputFormat);
                }

                if (outputFormat == OutputFormat.Txt)
                {
                    WriteHorizontalLine(sb, columnWidths);
                }
                else if (outputFormat == OutputFormat.Md && rowSet != table.Last())
                {
                    WriteBlankLine(sb, columnWidths);
                }
                else if (outputFormat == OutputFormat.Csv)
                {
                    sb.AppendLine();
                }
            }

            return sb.ToString();
        }

        private static void WriteRow(StringBuilder sb, IList<int> columnWidths, IList<string> row, OutputFormat outputFormat)
        {
            if (outputFormat == OutputFormat.Txt || outputFormat == OutputFormat.Md)
            {
                sb.Append('|');
                for (var i = 0; i < columnWidths.Count; i++)
                {
                    var cell = (row.Count > i) ? row[i] : null;

                    // Right-align numbers and percentages, left-align everything else
                    if (double.TryParse(cell, out var _) ||
                        (cell != null && cell.EndsWith('%') && double.TryParse(cell.Substring(0, cell.Length - 1), out var _)))
                    {
                        sb.AppendFormat($" {{0,{columnWidths[i]}}} ", cell);
                    }
                    else
                    {
                        sb.AppendFormat($" {{0,-{columnWidths[i]}}} ", cell);
                    }
                    sb.Append('|');
                }
                sb.AppendLine();
            }
            else if (outputFormat == OutputFormat.Csv)
            {
                sb.Append(row.First());
                foreach (var column in row.Skip(1))
                {
                    sb.Append(',');
                    sb.Append(column);
                }
                sb.AppendLine();
            }
        }

        private static void WriteHorizontalLine(StringBuilder sb, IEnumerable<int> columnWidths)
        {
            sb.Append('|');

            foreach (var columnWidth in columnWidths)
            {
                for (var i = 0; i < columnWidth + 2; i++)
                {
                    sb.Append('-');
                }
                sb.Append('|');
            }

            sb.AppendLine();
        }

        private static void WriteBlankLine(StringBuilder sb, IEnumerable<int> columnWidths)
        {
            sb.Append('|');

            foreach (var columnWidth in columnWidths)
            {
                for (var i = 0; i < columnWidth + 2; i++)
                {
                    sb.Append(' ');
                }
                sb.Append('|');
            }

            sb.AppendLine();
        }
    }
}
