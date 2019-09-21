namespace A {
    public interface IInt {
        void M0(string s);
        void M1(string s = null);
        virtual void M2(string s = "s");
        void M3(string s, int m = 3);
        IInt M3(string s, int m = 3);
        void M4<T>();
    }
}
