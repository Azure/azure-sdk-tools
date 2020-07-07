namespace CreateRuleFabricBot.Rules
{
    public abstract class BaseCapability
    {
        public abstract string GetPayload();
        public abstract string GetTaskId();
    }
}
