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

    // Output model type that would normally require a model factory
    // This will be used to test dependency filtering
    public class ExternalOutputModel
    {
        private ExternalOutputModel() { }
        public string Name { get; }
        public int Value { get; }
    }
}
