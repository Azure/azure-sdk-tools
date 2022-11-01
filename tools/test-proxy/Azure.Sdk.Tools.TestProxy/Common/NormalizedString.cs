namespace Azure.Sdk.tools.TestProxy.Common {
    public class NormalizedString {
        private string value;

        public NormalizedString(string s){
            if (s == null)
            {
                value = null;
            }
            else
            {
                value = s.Replace("\\", "/");
            }
        }

        public override string ToString(){
            return value;
        }
    }
}
