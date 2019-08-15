using System.Collections.Generic;
using System.Text;

namespace ApiView
{
    public class StringListApiView : List<LineApiView>
    {
        public override string ToString()
        {
            bool isFirst = true;

            var builder = new StringBuilder();
            foreach (var line in this)
            {
                if (isFirst)
                {
                    isFirst = false;
                }
                else
                {
                    builder.AppendLine();
                }
                builder.Append(line.DisplayString);
            }
            
            return builder.ToString();
        }
    }
}
