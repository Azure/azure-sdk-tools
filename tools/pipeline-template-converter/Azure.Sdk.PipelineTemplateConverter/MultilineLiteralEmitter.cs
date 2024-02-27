using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.EventEmitters;

namespace Azure.Sdk.PipelineTemplateConverter;

// See https://stackoverflow.com/questions/58431796/change-the-scalar-style-used-for-all-multi-line-strings-when-serialising-a-dynam
public class MultilineLiteralEmitter : ChainedEventEmitter
{
    public MultilineLiteralEmitter(IEventEmitter nextEmitter)
        : base(nextEmitter) { }

    public override void Emit(ScalarEventInfo eventInfo, IEmitter emitter)
    {

        if (typeof(string).IsAssignableFrom(eventInfo.Source.Type))
        {
            string value = eventInfo.Source.Value as string ?? string.Empty;
            if (!string.IsNullOrEmpty(value))
            {
                bool isMultiLine = value.IndexOfAny(new char[] { '\r', '\n' }) >= 0;
                if (isMultiLine)
                    eventInfo = new ScalarEventInfo(eventInfo.Source)
                    {
                        Style = ScalarStyle.Any
                    };
            }
        }

        this.nextEmitter.Emit(eventInfo, emitter);
    }
}