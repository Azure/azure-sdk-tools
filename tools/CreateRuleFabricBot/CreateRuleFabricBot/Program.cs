using CommandLine;
using CreateRuleFabricBot.CommandLine;
using CreateRuleFabricBot.Markdown;
using CreateRuleFabricBot.Rules.IssueRouting;
using CreateRuleFabricBot.Service;
using OutputColorizer;
using System;
using System.Collections.Generic;
using System.Linq;

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
            IssueRoutingCapability irc = new IssueRoutingCapability(s_options.Owner, s_options.Repo);

            string payload = string.Empty;

            if (s_options.Action == ActionToTake.create || s_options.Action == ActionToTake.update)
            {
                Colorizer.Write("Parsing service table... ");
                MarkdownTable table = MarkdownTable.Parse(s_options.ServicesFile);
                Colorizer.WriteLine("[Green!done].");

                foreach (var row in table.Rows)
                {
                    if (!string.IsNullOrEmpty(row[2].Trim()))
                    {
                        // the row at position 0 is the label to use on top of 'Service Attention'
                        string[] labels = new string[] { "Service Attention", row[0] };

                        // The row at position 2 is the set of mentionees to ping on the issue.
                        IEnumerable<string> mentionees = row[2].Split(',').Select(x => x.Replace("@", "").Trim());

                        //add the service
                        irc.AddService(labels, mentionees);
                    }
                }

                payload = irc.ToJson();
                Colorizer.WriteLine("Found [Yellow!{0}] service routes.", irc.RouteCount);
            }

            if (s_options.Prompt)
            {
                Colorizer.WriteLine("Proceed with [Cyan!{0}] for repo [Yellow!{1}\\{2}] (y/n)? ", s_options.Action, s_options.Owner, s_options.Repo);
                var key = Console.ReadKey();

                if (key.Key != ConsoleKey.Y)
                {
                    Colorizer.WriteLine("No action taken.");
                    return;
                }
            }

            try
            {
                switch (s_options.Action)
                {
                    case ActionToTake.create:
                        rs.CreateTask(payload);
                        break;
                    case ActionToTake.update:
                        rs.UpdateTask(IssueRoutingCapability.GetTaskId(s_options.Owner, s_options.Repo), payload);
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
    }
}
