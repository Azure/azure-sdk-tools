using System;
using System.Collections.Generic;
using System.Linq;

namespace swagger_api_parser
{
    public static class Utils
    {
        public static string GetCommonPath(IEnumerable<string> paths)
        {
            var pathsArray = new List<List<string>>() { };
            if (pathsArray == null)
            {
                throw new ArgumentNullException(nameof(pathsArray));
            }

            pathsArray.AddRange(paths.Select(path => path.Split('/')).Select(pathParts => new List<string>(pathParts)));

            var commonPathList = new List<string>() { };

            bool found = false;
            var idx = 0;
            string commonPath = "";
            while (!found)
            {
                if (idx < pathsArray[0].Count)
                {
                    
                    commonPath = pathsArray[0][idx]; 
                }
                
                foreach (var it in pathsArray)
                {
                    if (idx>=it.Count||it[idx] != commonPath)
                    {
                        found = true;
                        break;;
                    }
                }

                if (found)
                {
                    continue;
                }

                idx++;
                commonPathList.Add(commonPath);

            }
            return string.Join("/", commonPathList);
        }
    }
}
