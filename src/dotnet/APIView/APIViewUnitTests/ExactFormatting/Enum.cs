/*-*/
using System;
/*-*/
namespace A {
    [Flags]
    public enum FlagsEnum {
        A = 1,
        B = 2,
        C = A | B,
    }
    public enum NotFlags {
        A = 1,
        B = 2,
        C = 2,
        D = 7,
    }
    /*-*/internal enum InternalEnum {/*-*/
        /*-*/A = 1/*-*/
    /*-*/}/*-*/
}
