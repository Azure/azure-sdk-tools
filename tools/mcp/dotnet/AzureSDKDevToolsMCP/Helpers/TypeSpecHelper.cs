using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AzureSDKDSpecTools.Helpers
{
    public interface ITypeSpecHelper
    {
        public bool IsTypeSpecProjectPath(string path);
        public bool IsTypeSpecProjectForMgmtPlane(string Path);
        public bool GetAPIVersion(string path);
    }
    public class TypeSpecHelper : ITypeSpecHelper
    {
        static readonly string TSPCONFIG_FILENAME = "tspconfig.yaml";

        public bool IsTypeSpecProjectPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("TypeSpec project root path cannot be null or empty.", nameof(path));
            }

            // Check if the path is a valid TypeSpec project path
            if (!Directory.Exists(path) || !File.Exists(Path.Combine(path, TSPCONFIG_FILENAME)))
            {
                return false;
            }
            return true;
        }

        public bool GetAPIVersion(string path)
        {
            throw new NotImplementedException();
        }

        public bool IsTypeSpecProjectForMgmtPlane(string Path)
        {
            //Todo: Process TypeSpec project and find whether this is mgmt or dataplane.
            return true;
        }
    }
}
