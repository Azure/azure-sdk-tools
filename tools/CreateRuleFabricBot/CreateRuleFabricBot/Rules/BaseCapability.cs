namespace CreateRuleFabricBot.Rules
{
    public abstract class BaseCapability
    {
        protected readonly string _repo;
        protected readonly string _owner;
        private readonly string _configurationFile;

        public BaseCapability(string org, string repo, string configurationFile)
        {
            _repo = repo;
            _owner = org;
            _configurationFile = configurationFile;

            if (!string.IsNullOrEmpty(configurationFile))
            {
                ReadConfigurationFromFile(configurationFile);
            }
        }

        internal abstract void ReadConfigurationFromFile(string configurationFile);
        public abstract string GetPayload();
        public abstract string GetTaskId();
    }
}
