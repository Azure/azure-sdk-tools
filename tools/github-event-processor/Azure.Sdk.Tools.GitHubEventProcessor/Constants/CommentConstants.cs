using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace Azure.Sdk.Tools.GitHubEventProcessor.Constants
{
    /// <summary>
    /// Strings in comments that cause actions to be included/excluded from processing.
    /// </summary>
    public class CommentConstants
    {
        // /azp <command> does things like prepares new pipelines or kicks off pipelines and should
        // also be ignored for comment processing. 
        public const string Azp = "/azp";
        // Check Enforcer commands are all /check-enforcer <something>
        // "What is Check Enforcer" is effectively dead
        public const string CheckEnforcer = "/check-enforcer";
        // This is used to reopen an issue in a comment and causes actions to happen
        public const string Reopen = "/reopen";
        // This is part of the message that's added to a PR that's being closed as part of the cron job that
        // closes stale pull requests. There's a rule that looks for this string as part of a pull request comment to
        // prevent an action.
        public const string ScheduledCloseFragment = "Since there hasn't been recent engagement, this is being closed out.";
        // used to unresolve an issue in a comment and causes actions to happen
        public const string Unresolve = "/unresolve";
    }
}
