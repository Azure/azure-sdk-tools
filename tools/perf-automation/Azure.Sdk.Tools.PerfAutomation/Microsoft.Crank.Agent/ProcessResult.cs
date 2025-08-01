﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.Crank.Agent
{
    public class ProcessResult
    {
        public ProcessResult(int exitCode, string standardOutput, string standardError)
        {
            ExitCode = exitCode;
            StandardOutput = standardOutput;
            StandardError = standardError;
        }

        public string StandardOutput { get; }
        public string StandardError { get; }
        public int ExitCode { get; }
        public double AverageCpu { get; set; } = -1;
        public long AverageMemory { get; set; } = -1;
    }
}
