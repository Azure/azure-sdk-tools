// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Exporter;

namespace Azure.Sdk.Tools.Cli.Telemetry;

/// <summary>
/// A ConsoleActivityExporter variant that writes its output to stderr instead of stdout.
/// The base ConsoleExporter writes via Console.WriteLine (i.e. Console.Out); in CLI mode
/// stdout is reserved for the command response so debug trace dumps must go to stderr to
/// avoid corrupting JSON/plain output piped to other tools.
/// </summary>
internal sealed class StderrConsoleActivityExporter : ConsoleActivityExporter
{
    private static readonly object SyncRoot = new();

    public StderrConsoleActivityExporter(ConsoleExporterOptions options) : base(options)
    {
    }

    public override ExportResult Export(in Batch<Activity> batch)
    {
        // Temporarily redirect Console.Out to Console.Error so that the base exporter's
        // Console.WriteLine calls land on stderr. Guard with a lock since Console.SetOut
        // is process-global and we don't want concurrent exports racing with the response writer.
        lock (SyncRoot)
        {
            var originalOut = Console.Out;
            try
            {
                Console.SetOut(Console.Error);
                return base.Export(batch);
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }
    }
}
