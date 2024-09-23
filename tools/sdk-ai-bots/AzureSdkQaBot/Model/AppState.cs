using Microsoft.TeamsAI.State;

namespace AzureSdkQaBot.Model
{
    public class AppState : TurnState<ConversationState, StateBase, TempState> { }

    public class ConversationState : StateBase
    {
        private const string _citationsKey = "_citationsKey";

        public IList<Citation>? Citations
        {
            get => Get<IList<Citation>>(_citationsKey);
            set => Set(_citationsKey, value);
        }
    }
}
