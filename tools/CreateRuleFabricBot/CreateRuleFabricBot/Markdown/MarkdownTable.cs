using System;
using System.Collections.Generic;
using System.IO;

namespace CreateRuleFabricBot.Markdown
{
    public class MarkdownTable
    {
        public string[] Headers { get; set; }
        public List<string[]> Rows { get; set; } = new List<string[]>();

        public static MarkdownTable Parse(string filePath)
        {
            MarkdownTable mt = new MarkdownTable();
            string line;

            using (StreamReader sr = new StreamReader(filePath))
            {
                while ((line = sr.ReadLine()) != null)
                {
                    line = line.Trim();

                    if (!line.StartsWith('|'))
                    {   // We don't have a line that starts with |
                        break;
                    }

                    string[] items = line.Split('|', StringSplitOptions.RemoveEmptyEntries);
                    for (int i = 0; i < items.Length; i++)
                    {
                        items[i] = items[i].Trim();
                    }

                    if (mt.Headers == null)
                    {
                        mt.Headers = items;
                    }
                    else
                    {
                        if (!IsOnlySeparator(items[0]))
                        {
                            mt.Rows.Add(items);
                        }
                    }
                }
            }
            return mt;
        }

        private static bool IsOnlySeparator(string s)
        {
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] != '-')
                {
                    return false;
                }
            }
            return true;
        }
    }
}
