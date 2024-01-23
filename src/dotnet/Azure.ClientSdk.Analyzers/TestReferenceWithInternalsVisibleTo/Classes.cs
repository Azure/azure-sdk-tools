namespace TestReferenceWithInternalsVisibleTo
{
    internal class InternalClass
    {
    }

    [Friend("TestProject")]
    internal class InternalClassWithFriendAttribute
    {
    }

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
