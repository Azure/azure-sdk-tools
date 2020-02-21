using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PipelineGenerator
{
    public class SdkComponent
    {
        public string Name { get; set; }
        public DirectoryInfo Path { get; set; }
        public string RelativeYamlPath { get; set; }
        public string Variant { get; set; }
    }
}
