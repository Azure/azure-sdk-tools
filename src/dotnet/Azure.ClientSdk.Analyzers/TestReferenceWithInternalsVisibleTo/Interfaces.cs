namespace TestReferenceWithInternalsVisibleTo
{
    internal interface IInternalInterface
    {
    }

    [Friend("TestProject")]
    internal interface IInternalInterfaceWithFriendAttribute
    {
    }

    public interface IPublicInterface
    {
    }
}
