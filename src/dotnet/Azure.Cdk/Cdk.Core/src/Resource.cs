using Azure.Core;
using Azure.Core.Serialization;
using Cdk.ResourceManager;
using Cdk.Resources;
using System.Security.Cryptography;
using System.Text;

namespace Cdk.Core
{
    public abstract class Resource : IModelSerializable<Resource>
    {
        protected internal Dictionary<string, string> ParameterOverrides { get; }
        public IList<Parameter> Parameters { get; }

        protected internal IList<Resource> ResourceReferences { get; }
        
        private IList<Resource> Dependencies { get; }
        
        internal void AddDependency(Resource resource)
        {
            Dependencies.Add(resource);
        }

        public IList<Output> Outputs { get; }
        public IList<Resource> ModuleDependencies { get; }

        public IList<Resource> Resources { get; }
        public Resource? Scope { get; }
        public object Properties { get; }
        public string Version { get; }
        public string Name { get; }

        private ResourceType ResourceType { get; }
        public ResourceIdentifier Id { get; }

        protected Resource(Resource? scope, string resourceName, ResourceType resourceType, string version, object properties)
        {
            Resources = new List<Resource>();
            Scope = scope;
            Scope?.Resources.Add(this);
            Properties = properties;
            Version = version;
            ParameterOverrides = new Dictionary<string, string>();
            Parameters = new List<Parameter>();
            Outputs = new List<Output>();
            ResourceReferences = new List<Resource>();
            ModuleDependencies = new List<Resource>();
            Dependencies = new List<Resource>();
            ResourceType = resourceType;
            Id = scope is null ? ResourceIdentifier.Root : scope is ResourceGroup ? scope.Id.AppendProviderResource(ResourceType.Namespace, ResourceType.GetLastType(), resourceName) : scope.Id.AppendChildResource(ResourceType.GetLastType(), resourceName);
            Name = GetHash();
            if (GetType().IsPublic)
            {
                Outputs.Add(new Output($"APP_{Name}_ID", Id!, this, true));
                Outputs.Add(new Output($"APP_{Name}_NAME", resourceName, this, true));
            }
        }

        protected virtual bool IsChildResource => Scope is not null && Scope is not ResourceGroup && Scope is not Subscription;
        private bool IsChildResourceX => Id.ResourceType != ResourceIdentifier.Root.ResourceType && Id.ResourceType != ResourceGroup.ResourceType && Id.ResourceType != Subscription.ResourceType;

        public void AssignParameter(string propertyName, Parameter parameter)
        {
            ParameterOverrides.Add(propertyName.ToCamelCase(), parameter.Name);
            Parameters.Add(parameter);
        }

        protected static AzureLocation GetLocation(AzureLocation? location = default) => location ?? Environment.GetEnvironmentVariable("AZURE_LOCATION") ?? AzureLocation.WestUS;


        public Output AddOutput(string name, string propertyName, bool isLiteral = false, bool isSecure = false)
        {
            string? reference = GetReference(Properties.GetType(), propertyName, Name.ToCamelCase());
            if (reference is null)
                throw new ArgumentNullException(nameof(propertyName), $"{propertyName} was not found in the property tree for {Properties.GetType().Name}");
            var result = new Output(name, reference, this, isLiteral, isSecure);
            Outputs.Add(result);
            return result;
        }

        private static string? GetReference(Type type, string propertyName, string str)
        {
            var properties = type.GetProperties();
            foreach (var property in properties)
            {
                if (property.Name.Equals(propertyName, StringComparison.Ordinal))
                {
                    return $"{str}.{property.Name.ToCamelCase()}";
                }
            }

            //need to check next level
            foreach (var property in properties)
            {
                var result = GetReference(property.PropertyType, propertyName, $"{str}.{property.Name.ToCamelCase()}");
                if (result is not null)
                    return result;
            }

            return null;
        }

        BinaryData IModelSerializable<Resource>.Serialize(ModelSerializerOptions options) => (options.Format.ToString()) switch
        {
            "bicep" => SerializeModule(options),
            "bicep-module" => SerializeModuleReference(options),
            _ => throw new FormatException($"Unsupported format {options.Format}")
        };

        private BinaryData SerializeModuleReference(ModelSerializerOptions options)
        {
            using var stream = new MemoryStream();
            stream.Write(Encoding.UTF8.GetBytes($"module {Name} './resources/{Name}/{Name}.bicep' = {{{Environment.NewLine}"));
            stream.Write(Encoding.UTF8.GetBytes($"  name: '{Id.Name}'{Environment.NewLine}"));
            stream.Write(Encoding.UTF8.GetBytes($"  scope: {Scope!.Name}{Environment.NewLine}"));
            WriteDependencies(stream);
            var parametersToWrite = new HashSet<Parameter>();
            GetAllParametersRecursive(this, parametersToWrite, IsChildResource);
            if (parametersToWrite.Count() > 0)
            {
                stream.Write(Encoding.UTF8.GetBytes($"  params: {{{Environment.NewLine}"));
                foreach (var parameter in parametersToWrite)
                {
                    var value = parameter.IsFromOutput
                        ? parameter.IsLiteral
                            ? $"'{parameter.Value}'"
                            : parameter.Value
                        : parameter.Name;
                    stream.Write(Encoding.UTF8.GetBytes($"    {parameter.Name}: {value}{Environment.NewLine}"));
                }
                stream.Write(Encoding.UTF8.GetBytes($"  }}{Environment.NewLine}"));
            }
            stream.Write(Encoding.UTF8.GetBytes($"}}{Environment.NewLine}"));

            return new BinaryData(stream.GetBuffer().AsMemory(0, (int)stream.Position));
        }

        private void WriteDependencies(MemoryStream stream)
        {
            if (ModuleDependencies.Count == 0)
                return;

            stream.Write(Encoding.UTF8.GetBytes($"  dependsOn: [{Environment.NewLine}"));
            foreach (var dependency in ModuleDependencies)
            {
                stream.Write(Encoding.UTF8.GetBytes($"    {dependency.Name}{Environment.NewLine}"));
            }
            stream.Write(Encoding.UTF8.GetBytes($"  ]{Environment.NewLine}"));
        }

        private BinaryData SerializeModule(ModelSerializerOptions options)
        {
            int depth = GetDepth();
            using var stream = new MemoryStream();

            WriteParameters(stream);

            stream.Write(Encoding.UTF8.GetBytes($"resource {Name} '{ResourceType}@{Version}' = {{{Environment.NewLine}"));

            if (IsChildResource && this is not DeploymentScript)
                stream.Write(Encoding.UTF8.GetBytes($"  parent: {Scope!.Name}{Environment.NewLine}"));

            if(Dependencies.Count > 0)
            {
                stream.Write(Encoding.UTF8.GetBytes($"  dependsOn: [{Environment.NewLine}"));
                foreach(var dependency in Dependencies)
                {
                    stream.Write(Encoding.UTF8.GetBytes($"    {dependency.Name}{Environment.NewLine}"));
                }
                stream.Write(Encoding.UTF8.GetBytes($"  ]{Environment.NewLine}"));
            }

            WriteLines(0, ModelSerializer.Serialize(Properties, options), stream, this);
            stream.Write(Encoding.UTF8.GetBytes($"}}{Environment.NewLine}"));

            foreach (var resource in Resources)
            {
                stream.Write(Encoding.UTF8.GetBytes(Environment.NewLine));
                WriteLines(0, ModelSerializer.Serialize(resource, options), stream, resource);
            }

            WriteResourceReferences(stream);

            WriteOutputs(stream);

            return new BinaryData(stream.GetBuffer().AsMemory(0, (int)stream.Position));
        }

        private void WriteResourceReferences(MemoryStream stream)
        {
            if (ResourceReferences.Count == 0)
                return;

            stream.Write(Encoding.UTF8.GetBytes(Environment.NewLine));
            foreach (var resourceReference in ResourceReferences)
            {
                stream.Write(Encoding.UTF8.GetBytes($"{Environment.NewLine}"));
                stream.Write(Encoding.UTF8.GetBytes($"resource {resourceReference.Name} '{resourceReference.ResourceType}@{resourceReference.Version}' existing = {{{Environment.NewLine}"));
                if (resourceReference.IsChildResource)
                    stream.Write(Encoding.UTF8.GetBytes($"  parent: {resourceReference.Scope!.Name}{Environment.NewLine}"));
                stream.Write(Encoding.UTF8.GetBytes($"  name: '{resourceReference.Id.Name}'{Environment.NewLine}"));
                stream.Write(Encoding.UTF8.GetBytes($"}}"));
            }
        }

        internal void WriteOutputs(MemoryStream stream)
        {
            if (Outputs.Count > 0)
                stream.Write(Encoding.UTF8.GetBytes(Environment.NewLine));

            var outputsToWrite = new HashSet<Output>();
            GetAllOutputsRecursive(this, outputsToWrite, IsChildResource);
            foreach (var output in outputsToWrite)
            {
                string value;
                if(output.IsLiteral || (!IsChildResource && (output.Source.Equals(this))))
                {
                    value = output.IsLiteral ? $"'{output.Value}'" : output.Value;
                }
                else
                {
                    value = $"{output.Source.Name}.outputs.{output.Name}";
                }
                string name = IsChildResource ? $"{Name}_{output.Name}" : output.Name;
                stream.Write(Encoding.UTF8.GetBytes($"output {name} string = {value}{Environment.NewLine}"));
            }
        }

        private void GetAllOutputsRecursive(Resource resource, HashSet<Output> visited, bool isChild)
        {
            if (!isChild)
            {
                foreach (var output in resource.Outputs)
                {
                    if (!visited.Contains(output))
                    {
                        visited.Add(output);
                    }
                }
                foreach (var child in resource.Resources)
                {
                    GetAllOutputsRecursive(child, visited, isChild);
                }
            }
        }

        protected void WriteParameters(MemoryStream stream)
        {
            var parametersToWrite = new HashSet<Parameter>();
            GetAllParametersRecursive(this, parametersToWrite, IsChildResource);
            foreach (var parameter in parametersToWrite)
            {
                if (this is ResourceGroup && parameter.IsFromOutput)
                    continue;
                string defaultValue = parameter.DefaultValue is null ? string.Empty : $" = '{parameter.DefaultValue}'";

                if (parameter.IsSecure)
                    stream.Write(Encoding.UTF8.GetBytes($"@secure(){Environment.NewLine}"));

                stream.Write(Encoding.UTF8.GetBytes($"@description('{parameter.Description}'){Environment.NewLine}"));
                stream.Write(Encoding.UTF8.GetBytes($"param {parameter.Name} string{defaultValue}{Environment.NewLine}{Environment.NewLine}"));
            }
        }

        private void GetAllParametersRecursive(Resource resource, HashSet<Parameter> visited, bool isChild)
        {
            if (!isChild)
            {
                foreach (var parameter in resource.Parameters)
                {
                    if (!visited.Contains(parameter))
                    {
                        visited.Add(parameter);
                    }
                }
                foreach (var child in resource.Resources)
                {
                    GetAllParametersRecursive(child, visited, isChild);
                }
            }
        }

        protected internal static void WriteLines(int depth, BinaryData data, Stream stream, Resource resource)
        {
            string indent = new string(' ', depth * 2);
            string[] lines = data.ToString().Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < lines.Length; i++)
            {
                string lineToWrite = lines[i];

                ReadOnlySpan<char> line = lines[i];
                int start = 0;
                while (line.Length > start && line[start] == ' ')
                {
                    start++;
                }
                line = line.Slice(start);
                int end = line.IndexOf(':');
                if (end > 0)
                {
                    string name = line.Slice(0, end).ToString();
                    if (resource.ParameterOverrides.TryGetValue(name, out var value))
                    {
                        lineToWrite = $"{new string(' ', start)}{name}: {value}";
                    }
                }
                stream.Write(Encoding.UTF8.GetBytes($"{indent}{lineToWrite}{Environment.NewLine}"));
            }
        }

        private int GetDepth()
        {
            Resource? parent = Scope;
            int depth = 0;
            while (parent is not null)
            {
                depth++;
                parent = parent.Scope;
            }
            return depth;
        }

        Resource IModelSerializable<Resource>.Deserialize(BinaryData data, ModelSerializerOptions options)
        {
            throw new NotImplementedException();
        }

        internal string GetHash()
        {
            string fullScope = $"{GetScopedName(this, Id.Name)}";
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(fullScope));

                // Convert the hash bytes to a base64 string and take the first 8 characters
                string base64Hash = Convert.ToBase64String(hashBytes);
                return $"{GetType().Name.ToCamelCase()}_{GetAlphaNumeric(base64Hash, 8)}";
            }
        }

        private string GetAlphaNumeric(string base64Hash, int chars)
        {
            StringBuilder sb = new StringBuilder();
            int index = 0;
            while (sb.Length <= chars && index < base64Hash.Length)
            {
                if (char.IsLetterOrDigit(base64Hash[index]))
                    sb.Append(base64Hash[index]);
                index++;
            }
            return sb.ToString();
        }

        private static string GetScopedName(Resource resource, string scopedName)
        {
            Resource? parent = resource.Scope;

            return parent is null || parent is Tenant ? scopedName : GetScopedName(parent, $"{parent.Id.Name}_{scopedName}");
        }
    }
}
