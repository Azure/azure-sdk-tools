#define DEBUG
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace TestLibrary
{
    public class PublicClass
    {
        public int publicField = 1;
        public const string publicString = "constant string";
        private int privateField;
        protected int protectedField;
        internal int internalField;
        protected internal int protectedInternalField;
        private protected int privateProtectedField;

        public event EventHandler PublicEvent;
        private event EventHandler PrivateEvent;
        protected event EventHandler ProtectedEvent;
        internal event EventHandler InternalEvent;
        protected internal event EventHandler ProtectedInternalEvent;
        private protected event EventHandler PrivateProtectedEvent;

        [ConditionalAttribute("DEBUG")]
        public static void StaticVoid(string[] args)
        {
            
        }

        private static void PrivateMethod(int times)
        {
            
        }

        public class Repeater
        {
            public static void Repeat(string phrase)
            {

            }
        }

        private class PrivateClass
        {

        }

        protected class ProtectedClass
        {

        }

        internal class InternalClass
        {

        }

        protected internal class ProtectedInternalClass
        {

        }

        private protected class PrivateProtectedClass
        {

        }
    }

    public class SomeEventsSomeFieldsNoMethodsSomeNamedTypes
    {
        public string publicField;
        private object privateField;
        protected int protectedField;
        internal double[] internalField;
        protected internal string protectedInternalField;
        private protected double privateProtectedField;

        public event EventHandler PublicEvent;
        private event EventHandler PrivateEvent;
        protected event EventHandler protectedEvent;
        internal event EventHandler internalEvent;
        protected internal event EventHandler protectedInternalEvent;
        private protected event EventHandler privateProtectedEvent;

        private void PrivateMethod()
        {

        }

        protected void ProtectedMethod()
        {

        }

        internal void InternalMethod()
        {

        }

        protected internal void ProtectedInternalMethod()
        {

        }

        private protected void PrivateProtectedMethod()
        {

        }

        public class PublicNestedClass
        {

        }

        private class PrivateNestedClass
        {

        }

        protected class ProtectedNestedClass
        {

        }

        internal class InternalNestedClass
        {

        }

        protected internal class ProtectedInternalNestedClass
        {

        }

        private protected class PrivateProtectedNestedClass
        {

        }
    }

    class Class  // internal by default
    {
        public string phrase = "This is a test";

        internal void ProtectedMethod(int times = 1)
        {
            
        }

        protected internal void ProtectedInternalMethod()
        {
            
        }

        private protected void PrivateProtectedMethod()
        {

        }
    }

    internal class InternalClass
    {

    }

    public interface PublicInterface<T>
    {
        int TypeParamParamsMethod<T>(T param, string str = "hello");

        string RefKindParamMethod(ref string str);
    }
}
