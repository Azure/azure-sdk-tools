using System;
using System.Collections.Generic;

namespace Stress.Generator
{
    public class Job : Resource
    {
        [ResourceProperty("Test name")]
        public string Name { get; set; }

        [ResourceProperty("Test image")]
        public string Image { get; set; }

        [ResourceProperty("Container command. If using multiple scenarios, use a template like `node dist/{{ .Scenario }}.js`")]
        public List<string> Command { get; set; }

        public Job() : base()
        {
        }
    }
}
