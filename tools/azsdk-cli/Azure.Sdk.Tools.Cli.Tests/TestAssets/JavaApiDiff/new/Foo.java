package com.example;

public class Foo {
    public void methodA(int y) {  // Parameter name changed from x to y
        System.out.println(y);
    }
    
    public void methodC() {  // methodB removed, methodC added
        System.out.println("Method C");
    }
}