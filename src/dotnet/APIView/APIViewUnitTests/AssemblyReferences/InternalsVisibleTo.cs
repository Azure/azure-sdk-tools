/*-*/
using System;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("TestProject")]
class FriendAttribute : Attribute {
    public FriendAttribute(string friendAssemblyName) {
        FriendAssemblyName = friendAssemblyName;
    }

    public string FriendAssemblyName { get; }
}
﻿/*-*/
namespace A {
public class PublicClass
    {
        public int PublicProperty { get; set; }
        internal int InternalProperty { get; set; }

        [Friend("TestProject")]
        internal int InternalPropertyWithFriendAttribute { get; set; }

        public void PublicMethod()
        { }

        internal void InternalMethod()
        { }

        [Friend("TestProject")]
        internal void InternalMethodWithFriendAttribute()
        { }
    }
}
