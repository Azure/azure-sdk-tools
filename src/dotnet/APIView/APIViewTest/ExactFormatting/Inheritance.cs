namespace A {
    public interface I1 {
    }
    public interface I2<G> {
    }
    public class K : I1 {
    }
    public class LClass : K, I1, I2<K> {
    }
}
