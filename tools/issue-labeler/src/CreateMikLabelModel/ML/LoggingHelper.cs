// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.ML.AutoML;
using Microsoft.ML.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace CreateMikLabelModel.ML
{
    internal class LoggingHelper
    {
        private const int Width = 114;
        private readonly ILogger _logger;

        public LoggingHelper(ILogger logger) => _logger = logger;

        internal void PrintIterationMetrics(int iteration, string trainerName, MulticlassClassificationMetrics metrics, double? runtimeInSeconds)
        {
            PrintRow($"{iteration,-4} {trainerName,-35} {metrics?.MicroAccuracy ?? double.NaN,14:F4} {metrics?.MacroAccuracy ?? double.NaN,14:F4} {runtimeInSeconds.Value,9:F1}", Width);
        }

        internal void PrintIterationException(Exception ex)
        {
            _logger.LogInformation($"Exception during AutoML iteration: {ex}");
        }

        internal void PrintMulticlassClassificationMetricsHeader()
        {
            PrintRow($"{"",-4} {"Trainer",-35} {"MicroAccuracy",14} {"MacroAccuracy",14} {"Duration",9}", Width);
        }

        private void PrintRow(string message, int width)
        {
            _logger.LogInformation("|" + message.PadRight(width - 2) + "|");
        }

        public void ConsoleWriteHeader(params string[] lines)
        {
            _logger.LogInformation(" ");
            foreach (var line in lines)
            {
                _logger.LogInformation(line);
            }
            var maxLength = lines.Select(x => x.Length).Max();
            _logger.LogInformation(new string('#', maxLength));
        }

        public static string BuildStringTable(IList<string[]> arrValues)
        {
            var maxColumnsWidth = GetMaxColumnsWidth(arrValues);
            var headerSpliter = new string('-', maxColumnsWidth.Sum(i => i + 3) - 1);

            var sb = new StringBuilder();
            for (var rowIndex = 0; rowIndex < arrValues.Count; rowIndex++)
            {
                if (rowIndex == 0)
                {
                    sb.AppendFormat("  {0} ", headerSpliter);
                    sb.AppendLine();
                }

                for (var colIndex = 0; colIndex < arrValues[0].Length; colIndex++)
                {
                    // Print cell
                    var cell = arrValues[rowIndex][colIndex];
                    cell = cell.PadRight(maxColumnsWidth[colIndex]);
                    sb.Append(" | ");
                    sb.Append(cell);
                }

                // Print end of line
                sb.Append(" | ");
                sb.AppendLine();

                // Print splitter
                if (rowIndex == 0)
                {
                    sb.AppendFormat(" |{0}| ", headerSpliter);
                    sb.AppendLine();
                }

                if (rowIndex == arrValues.Count - 1)
                {
                    sb.AppendFormat("  {0} ", headerSpliter);
                }
            }

            return sb.ToString();
        }

        private static int[] GetMaxColumnsWidth(IList<string[]> arrValues)
        {
            var maxColumnsWidth = new int[arrValues[0].Length];
            for (var colIndex = 0; colIndex < arrValues[0].Length; colIndex++)
            {
                for (var rowIndex = 0; rowIndex < arrValues.Count; rowIndex++)
                {
                    var newLength = arrValues[rowIndex][colIndex].Length;
                    var oldLength = maxColumnsWidth[colIndex];

                    if (newLength > oldLength)
                    {
                        maxColumnsWidth[colIndex] = newLength;
                    }
                }
            }

            return maxColumnsWidth;
        }

        private class ColumnInferencePrinter
        {
            private static readonly string[] TableHeaders = new[] { "Name", "Data Type", "Purpose" };

            private readonly ColumnInferenceResults _results;

            public ColumnInferencePrinter(ColumnInferenceResults results)
            {
                _results = results;
            }

            public void Print()
            {
                var tableRows = new List<string[]>();

                // Add headers
                tableRows.Add(TableHeaders);

                // Add column data
                var info = _results.ColumnInformation;
                AppendTableRow(tableRows, info.LabelColumnName, "Label");
                AppendTableRow(tableRows, info.ExampleWeightColumnName, "Weight");
                AppendTableRow(tableRows, info.SamplingKeyColumnName, "Sampling Key");
                AppendTableRows(tableRows, info.CategoricalColumnNames, "Categorical");
                AppendTableRows(tableRows, info.NumericColumnNames, "Numeric");
                AppendTableRows(tableRows, info.TextColumnNames, "Text");
                AppendTableRows(tableRows, info.IgnoredColumnNames, "Ignored");

                Console.WriteLine(LoggingHelper.BuildStringTable(tableRows));
            }

            private void AppendTableRow(ICollection<string[]> tableRows,
                string columnName, string columnPurpose)
            {
                if (columnName == null)
                {
                    return;
                }

                tableRows.Add(new[]
                {
                    columnName,
                    GetColumnDataType(columnName),
                    columnPurpose
                });
            }

            private void AppendTableRows(ICollection<string[]> tableRows,
                IEnumerable<string> columnNames, string columnPurpose)
            {
                foreach (var columnName in columnNames)
                {
                    AppendTableRow(tableRows, columnName, columnPurpose);
                }
            }

            private string GetColumnDataType(string columnName)
            {
                return _results.TextLoaderOptions.Columns.First(c => c.Name == columnName).DataKind.ToString();
            }
        }
    }
}
