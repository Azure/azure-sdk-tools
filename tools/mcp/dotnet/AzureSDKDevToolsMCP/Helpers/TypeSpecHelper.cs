using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AzureSDKDSpecTools.Models;
using Microsoft.Extensions.Logging;

namespace AzureSDKDSpecTools.Helpers
{
    public interface ITypeSpecHelper
    {
        public bool IsValidTypeSpecProjectPath(string path);
        public bool IsTypeSpecProjectForMgmtPlane(string Path);
    }
    public class TypeSpecHelper : ITypeSpecHelper
    {        

        private ILogger<TypeSpecHelper> logger;

        public TypeSpecHelper(ILogger<TypeSpecHelper> _logger)
        {
            logger = _logger;
        }

        public bool IsValidTypeSpecProjectPath(string path)
        {
            return TypeSpecProject.IsValidTypeSpecProjectPath(path);
        }

        public bool IsTypeSpecProjectForMgmtPlane(string Path)
        {
            var typeSpecObject = TypeSpecProject.ParseTypeSpecConfig(Path);
            return typeSpecObject?.IsManagementPlane ?? false;
        }
    }
}
