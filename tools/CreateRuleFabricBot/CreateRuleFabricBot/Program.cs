using CommandLine;
using CreateRuleFabricBot.CommandLine;
using CreateRuleFabricBot.Markdown;
using CreateRuleFabricBot.Rules;
using CreateRuleFabricBot.Rules.IssueRouting;
using CreateRuleFabricBot.Service;
using OutputColorizer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CreateRuleFabricBot
{
    internal class Program
    {
        private const string ParserPrefix = "officebot_";
        private static CommandLineArgs s_options;

        private static void Main(string[] args)
        {
            if (!Parser.TryParse(args, out s_options, new ParserOptions() { VariableNamePrefix = ParserPrefix }))
            {
                return;
            }

            if (s_options.CookieToken == null)
            {
                Colorizer.WriteLine($"[Red!Error]: Please specify authentication token as an argument or in the environment variable [Yellow!{ParserPrefix}_token]");
                return;
            }

            FabricBotClient rs = new FabricBotClient(s_options.Owner, s_options.Repo, s_options.CookieToken);

            string payload = string.Empty;
            string taskId = string.Empty;

            // for Create and update, construct the payload and taskId 
            if (s_options.Action == ActionToTake.create || s_options.Action == ActionToTake.update)
            {
                // Instantiate the object based on the TaskType 
                BaseCapability capability = CreateCapabilityObject(s_options);
                payload = capability.GetPayload();
                taskId = capability.GetTaskId();
            }

            if (s_options.Prompt)
            {
                Colorizer.Write("Proceed with [Cyan!{0}] for repo [Yellow!{1}\\{2}] (y/n)? ", s_options.Action, s_options.Owner, s_options.Repo);
                var key = Console.ReadKey();

                if (key.Key != ConsoleKey.Y)
                {
                    Colorizer.WriteLine("No action taken.");
                    return;
                }
                Colorizer.WriteLine("");
            }

            try
            {
                switch (s_options.Action)
                {
                    case ActionToTake.create:
                        rs.CreateTask(payload);
                        break;
                    case ActionToTake.update:
                        rs.UpdateTask(taskId, payload);
                        break;
                    case ActionToTake.delete:
                        rs.DeleteTask(s_options.TaskId);
                        break;
                    case ActionToTake.deleteall:
                        rs.DeleteAll();
                        break;
                    case ActionToTake.listTaskIds:
                        var taskIds = rs.GetTaskIds();
                        foreach (string item in taskIds)
                        {
                            Colorizer.WriteLine("Found task with id: [Yellow!{0}]", item);
                        }
                        break;
                    default:
                        Colorizer.WriteLine($"[Red!Error]: Command [Yellow!{s_options.Action}] unknown.");
                        return;
                }
                Colorizer.WriteLine("[Green!Done].");
            }
            catch (Exception e)
            {
                Colorizer.WriteLine("[Red!Error]: {0}", e.Message);
            }
        }

        private static BaseCapability CreateCapabilityObject(CommandLineArgs s_options)
        {
            switch (s_options.TaskType)
            {
                case TaskType.IssueRouting:
                    return new IssueRoutingCapability(s_options.Owner, s_options.Repo, s_options.InputDataFile);
                case TaskType.PullRequestLabel:
                    return new PullRequestLabelFolderCapability(s_options.Owner, s_options.Repo, s_options.InputDataFile);
            }

            throw new InvalidOperationException("Unknown task type " + s_options.TaskType);
        }
    }
}
