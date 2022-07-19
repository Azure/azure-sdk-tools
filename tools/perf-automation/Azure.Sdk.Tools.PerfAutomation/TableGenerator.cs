using Azure.Sdk.Tools.PerfAutomation.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.PerfAutomation
{
    internal static class TableGenerator
    {
        public static string Generate(IList<string> headers, IList<IList<IList<string>>> table, OutputFormat outputFormat)
        {
            var columns = table.SelectMany(rowSet => rowSet).Prepend(headers);

            var columnWidths = new List<int>(new int[columns.First().Count()]);
            foreach (var row in columns)
            {
                for (var i=0; i < row.Count(); i++)
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

            WriteHorizontalLine(sb, columnWidths);

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
            }

            return sb.ToString();
        }

        private static void WriteRow(StringBuilder sb, IList<int> columnWidths, IList<string> row, OutputFormat outputFormat)
        {
            if (outputFormat == OutputFormat.Txt || outputFormat == OutputFormat.Md)
            {
                sb.Append("| ");
                for (var i = 0; i < columnWidths.Count(); i++)
                {
                    sb.AppendFormat($"{{0,-{columnWidths[i]}}}", row[i]);
                    sb.Append(" | ");
                }
                sb.AppendLine();

            }
        }

        private static void WriteHorizontalLine(StringBuilder sb, IEnumerable<int> columnWidths)
        {
            sb.Append('|');

            foreach (var columnWidth in columnWidths)
            {
                for (var i=0; i < columnWidth + 2; i++)
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
