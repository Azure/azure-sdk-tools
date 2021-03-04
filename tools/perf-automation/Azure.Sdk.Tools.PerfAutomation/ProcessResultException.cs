using Microsoft.Crank.Agent;
using System;

namespace Azure.Sdk.Tools.PerfAutomation
{
    public class ProcessResultException : Exception
    {
        public ProcessResultException(string command, ProcessResult result)
        {
            Command = command;
            Result = result;
        }

        public override string Message =>
            $"Command '{Command}' failed with exit code {Result.ExitCode}" + Environment.NewLine + Environment.NewLine +
            "Standard Output:" + Environment.NewLine + Result.StandardOutput + Environment.NewLine +
            "Standard Error:" + Environment.NewLine + Result.StandardError + Environment.NewLine;

        public string Command { get; }
        public ProcessResult Result { get; }
    }
}
