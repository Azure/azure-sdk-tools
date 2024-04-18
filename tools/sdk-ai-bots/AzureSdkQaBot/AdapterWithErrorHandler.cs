using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Builder.TraceExtensions;
using Microsoft.Bot.Connector.Authentication;

namespace AzureSdkQaBot
{
    public class AdapterWithErrorHandler : CloudAdapter
    {
        public AdapterWithErrorHandler(BotFrameworkAuthentication auth, ILogger<CloudAdapter> logger)
            : base(auth, logger)
        {
            OnTurnError = async (turnContext, exception) =>
            {
                // Log any leaked exception from the application.
                // NOTE: In production environment, you should consider logging this to
                // Azure Application Insights. Visit https://aka.ms/bottelemetry to see how
                // to add telemetry capture to your bot.
                logger.LogError(exception, $"[OnTurnError] unhandled error : {exception}");
                // Send a message to the user
                string errorMsg = exception.Message;
                if (exception.InnerException != null)
                {
                    errorMsg = exception.InnerException.ToString();
                    if (errorMsg.Contains("This model's maximum context length is"))
                    {
                        errorMsg = "I'm sorry, but the question you've asked is a bit too long for me to handle. Could you please try rephrasing it in a shorter way?";
                        await turnContext.SendActivityAsync(errorMsg);
                        return;
                    }
                }
                await turnContext.SendActivityAsync($"The bot encountered an error: {errorMsg}");
                // Send a trace activity    
                await turnContext.TraceActivityAsync("OnTurnError Trace", exception, "TurnError");
            };
        }
    }
}
