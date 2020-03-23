/*-*/using System;
using System.Threading.Tasks;

/*-*/namespace A {
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
    public abstract class M : IDisposable {
        protected M()/*-*/{/*-*/;/*-*/}
        void IDisposable.Dispose() { }/*-*/
    }
    public class NClass : K, I1, I2<K> {
        public NClass()/*-*/{/*-*/;/*-*/}/*-*/
        public override sealed void M()/*-*/{/*-*/;/*-*/}/*-*/
    }
}
