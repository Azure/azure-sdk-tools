/*-*/
using System;
using System.Runtime.CompilerServices;
using B;

[assembly: InternalsVisibleTo("Azure.Some.Client")]
[assembly: InternalsVisibleTo("Azure.Some.Client.Tests")]
[assembly: InternalsVisibleTo("Azure.Some.Client.Perf")]
namespace B {
    internal class FriendAttribute : Attribute {
        public FriendAttribute(string friendAssemblyName) { }
    }
}
/*-*/
namespace A {
    public class PublicClass {
        public PublicClass()/*-*/{/*-*/;/*-*/}/*-*/
        /*-*/internal int InternalProperty { get; set; }/*-*/
        /*@internal @*/[Friend("TestProject")]
        internal void InternalMethodWithFriendAttribute()/*-*/{/*-*/;/*-*/}/*-*/
        /*@internal @*/[Friend("TestProject")]
        internal int InternalPropertyWithFriendAttribute { get; set; }
        /*-*/internal void InternalMethod(){ }/*-*/
        public void PublicMethod()/*-*/{/*-*/;/*-*/}/*-*/
        public int PublicProperty { get; set; }
    }
}
