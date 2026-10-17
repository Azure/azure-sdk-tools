/*-*/
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Diagnostics.CodeAnalysis;

class PrivateAttribute : Attribute { }

namespace System.Diagnostics.CodeAnalysis
{
    internal class VisibleInternalFooAttribute : Attribute
    {
        public VisibleInternalFooAttribute(string description) { }
    }
}

/*-*/

namespace A {
    public class ArrayAttribute : Attribute {
        public ArrayAttribute(int[] arr)/*-*/{/*-*/;/*-*/}/*-*/
    }
    [DebuggerTypeProxy(typeof(PrivateAttribute))]
    public class Class {
        public Class()/*-*/{/*-*/;/*-*/}/*-*/
        [Conditional("string")]
        [Conditional("string2")]
        public void M()/*-*/{/*-*/;/*-*/}/*-*/
        /*-*/
        // Skipped attributes
        [DebuggerStepThrough]
        [IteratorStateMachine(typeof(Class))]
        [PrivateAttribute]
        [AsyncStateMachine(typeof(Class))]
        [EditorBrowsable(EditorBrowsableState.Never)]
        /*-*/[Public(1)]
        [Public("s")]
        [Public("s", Property = "a")]
        [Public(null, Property = null)]
        [Array(new[] {1, 2, 3})]
        /*@internal @*/[VisibleInternalFoo("foo1")]
        public void M1()/*-*/{/*-*/;/*-*/}/*-*/
    }
    [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
    public class PublicAttribute : Attribute {
        public PublicAttribute(int i)/*-*/{/*-*/;/*-*/}/*-*/
        public PublicAttribute(string s)/*-*/{/*-*/;/*-*/}/*-*/
        public string Property { get; set; }
    }
}
