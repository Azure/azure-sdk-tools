using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.PerfAutomation
{
    internal static class AsciiiTable
    {
        // |---------------------------------------|------------------|---------|
        // | Name                                  | Requested        | Runtime |
        // |---------------------------------------|------------------|---------|
        // | azure - storage - file - share        | 12.13.1          | unknown |
        // | azure-core                            | 1.29.1           | unknown |
        // | azure - core - http - netty           | 1.12.2           | unknown |
        // | azure - core - http - okhttp          | 1.10.1           | unknown |
        // | azure - storage - blob                | 12.18.0 - beta.1 | unknown |
        // | azure - storage - blob - cryptography | 12.17.0 - beta.1 | unknown |
        // | azure - storage - file - datalake     | 12.10.1          | unknown |
        // | reactor - core                        | 3.4.17           | unknown |
        // |---------------------------------------|------------------|---------|
        // | azure - storage - file - share        | source           | unknown |
        // | azure-core                            | source           | unknown |
        // | azure - core - http - netty           | source           | unknown |
        // | azure - core - http - okhttp          | source           | unknown |
        // | azure - storage - blob                | source           | unknown |
        // | azure - storage - blob - cryptography | source           | unknown |
        // | azure - storage - file - datalake     | source           | unknown |
        // | reactor - core                        | source           | unknown |
        // |---------------------------------------|------------------|---------|
        public static string Generate(IList<string> headers, IList<IList<IList<string>>> table)
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

            WriteHorizontalLine(sb, columnWidths);

            WriteRow(sb, columnWidths, headers);

            WriteHorizontalLine(sb, columnWidths);

            foreach (var rowSet in table)
            {
                foreach (var row in rowSet)
                {
                    WriteRow(sb, columnWidths, row);
                }
                WriteHorizontalLine(sb, columnWidths);
            }

            return sb.ToString();
        }

        private static void WriteRow(StringBuilder sb, IList<int> columnWidths, IList<string> row)
        {
            sb.Append("| ");
            for (var i = 0; i < columnWidths.Count(); i++)
            {
                sb.AppendFormat($"{{0,-{columnWidths[i]}}}", row[i]);
                sb.Append(" | ");
            }
            sb.AppendLine();
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
    }
}
