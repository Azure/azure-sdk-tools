using System.Collections.Generic;
using System.Text;

namespace APIView
{
    class StringListAPIV : List<LineAPIV>
    {
        public override string ToString()
        {
            var builder = new StringBuilder();
            foreach (var line in this)
            {
                builder.Append(line.DisplayString);
                builder.AppendLine();
            }
            return builder.ToString();
        }
    }
}
