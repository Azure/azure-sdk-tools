/*-*/using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using C;

[assembly: InternalsVisibleTo("Azure.Some.Client")]
[assembly: InternalsVisibleTo("Azure.Some.Client.Tests")]
[assembly: InternalsVisibleTo("Azure.Some.Client.Perf")]
namespace C {
    internal class FriendAttribute : Attribute {
        public FriendAttribute(string friendAssemblyName) { }
    }
}
internal interface IInternal
{
    void M();
    void N();
}/*-*/
/*@internal @*/[Friend("TestProject")]
internal interface IInternalWithFriend {
    void M();
    void N();
}
namespace A {
    public interface I1 {
    }
    public interface I2<G> {
    }
    public abstract class K : I1 {
        protected K()/*-*/{/*-*/;/*-*/}/*-*/
        public abstract void M();
    }
    public abstract class L : IDisposable, IAsyncDisposable {
        protected L()/*-*/{/*-*/;/*-*/}/*-*/
        public abstract void Dispose();
        public abstract ValueTask DisposeAsync();
    }
    /*@internal @*/[Friend("TestProject")]
    internal abstract class M : IDisposable {
        protected M()/*-*/{/*-*/;/*-*/}/*-*/
        void IDisposable.Dispose()/*-*/{/*-*/;/*-*/}/*-*/
    }
    public class NClass : K, I1, I2<K> {
        public NClass()/*-*/{/*-*/;/*-*/}/*-*/
        public override sealed void M()/*-*/{/*-*/;/*-*/}/*-*/
    }
    public class OClass/*-*/ : IInternal/*-*/ {
        public OClass()/*-*/{/*-*/;/*-*/}/*-*/
        public void M()/*-*/{/*-*/;/*-*/}/*-*//*-*/
        void IInternal.N(){}
        /*-*/
    }
    public class PClass : IInternalWithFriend {
        public PClass()/*-*/{/*-*/;/*-*/}/*-*/
        void IInternalWithFriend.N()/*-*/{/*-*/;/*-*/}/*-*/
        public void M()/*-*/{/*-*/;/*-*/}/*-*/
    }
}
