using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.TestProxy.Common
{
    public class ModifiableRecordSession
    {
        public RecordMatcher CustomMatcher = null;

        public RecordSession Session;

        public ModifiableRecordSession(RecordSession session)
        {
            Session = session;
        }

        public List<ResponseTransform> AdditionalTransforms = new List<ResponseTransform>();

        public List<RecordedTestSanitizer> AdditionalSanitizers = new List<RecordedTestSanitizer>();

    }
}
