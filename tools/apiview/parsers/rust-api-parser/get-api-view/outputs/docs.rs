pub mod docs {
    pub mod module_example {
        pub mod lease {
            /// This is a sample module
            pub mod sample_module2 {
                /// This is a sample function
                pub fn sample_function2()
                /// This is a sample struct
                pub struct SampleStruct2 {
                    pub field1: String,
                    pub field2: i32,
                }
            }
            pub fn use_sample_struct2()
        }
    }
    /// This is a sample module
    pub mod sample_module {
        /// This is a sample function
        pub fn sample_function()
        /// This is a sample struct
        pub struct SampleStruct {
            pub field: i32,
        }
    }
    pub trait MyTrait {
        fn example_method(self: &Self);
    }
    pub fn foo<T, V>(v: &T)
    pub fn bar<T, V>(v: &T)
}
