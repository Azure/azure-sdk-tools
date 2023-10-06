namespace TestReferenceWithInternalsVisibleTo
{
    public class PublicClass
    {
        public int PublicProperty { get; set; }
        internal int InternalProperty { get; set; }

        public void PublicMethod()
        { }

        internal void InternalMethod()
        { }
    }
}
