using System;
using System.Collections.Generic;
using System.Text;

namespace CreateRuleFabricBot.Rules
{
    public abstract class BaseCapability
    {
        public abstract string GetPayload();
        public abstract string GetTaskId();
    }
}
