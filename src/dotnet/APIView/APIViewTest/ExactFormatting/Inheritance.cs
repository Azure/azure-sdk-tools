namespace A {
    public interface I1 {
    }
    public interface I2<G> {
    }
    public abstract class K : I1 {
        public abstract void M();
    }
    public class LClass : K, I1, I2<K> {
        public override sealed void M()/*-*/{/*-*/;/*-*/}/*-*/
    }
}
