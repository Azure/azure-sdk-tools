using System;

namespace Stress.Generator
{
    public class Job : Resource
    {
        [ResourceProperty("Test name")]
        public string Name;

        [ResourceProperty("Container command. If using multiple scenarios, use a template like `node dist/{{ .Scenario }}.js`")]
        public string Command;

        public Job() : base()
        {
            
        }
    }
}
