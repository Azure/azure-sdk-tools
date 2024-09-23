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

        public static implicit operator string(NormalizedString s)
        {
            return s.ToString();
        }

        public static explicit operator NormalizedString(string s)
        {
            return new NormalizedString(s);
        }

        public override string ToString(){
            return value;
        }
    }
}
