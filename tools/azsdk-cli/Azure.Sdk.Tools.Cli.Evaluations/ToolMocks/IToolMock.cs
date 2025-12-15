using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;

namespace Azure.Sdk.Tools.Cli.Evaluations.ToolMocks
{
    public interface IToolMock
    {
        string ToolName { get; }

        // call ids value does not matter just need a common value for GetMockCallandResponse
        string CallId { get; }
        ChatMessage GetMockResponse(string callid);
        ChatMessage GetMockCall();
        IEnumerable<ChatMessage> GetMockCallAndResponse()
        {
            yield return GetMockCall();
            yield return GetMockResponse(CallId);
        }
    }
}
