using Microsoft.Teams.AI.State;

namespace AzureSdkQaBot.Model
{
    public class AppState : TurnState
    {
        public AppState()
        {
            ScopeDefaults[CONVERSATION_SCOPE] = new ConversationState();
        }

        public new ConversationState Conversation
        {
            get
            {
                TurnStateEntry? scope = GetScope(CONVERSATION_SCOPE);

                if (scope == null)
                {
                    throw new ArgumentException("TurnState hasn't been loaded. Call LoadStateAsync() first.");
                }

                return (ConversationState)scope.Value!;
            }
            set
            {
                TurnStateEntry? scope = GetScope(CONVERSATION_SCOPE);

                if (scope == null)
                {
                    throw new ArgumentException("TurnState hasn't been loaded. Call LoadStateAsync() first.");
                }

                scope.Replace(value!);
            }
        }
    }

    public class ConversationState : Record
    {

        public IList<DocumentCitation>? Citations
        {
            get => Get<IList<DocumentCitation>>(Constants.AppState_Conversation_CitationKey);
            set => Set(Constants.AppState_Conversation_CitationKey, value);
        }
    }
}
