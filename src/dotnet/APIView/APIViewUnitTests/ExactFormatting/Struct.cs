namespace A {
    public struct S {
        public S(int a)/*-*/{ A = a; Str = null/*-*/;/*-*/}/*-*/
        public int A;
        public string Str { get; }
    }
    public readonly struct S1 {
        public S1(int a)/*-*/{ A = a/*-*/;/*-*/}/*-*/
        public int? A { get; }
    }
    /*-*/internal struct S2 { }/*-*/
}
