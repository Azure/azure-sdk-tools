namespace A {
    public interface I1 {
    }
    public interface I2<G> {
    }
    public abstract class K : I1 {
        protected K()/*-*/{/*-*/;/*-*/}/*-*/
        public abstract void M();
    }
    public class LClass : K, I1, I2<K> {
        public LClass()/*-*/{/*-*/;/*-*/}/*-*/
        public override sealed void M()/*-*/{/*-*/;/*-*/}/*-*/
    }
}
