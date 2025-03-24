# patch

``` ts
class RemoveClassConstructor {
    constructor(remove: string) {}
    constructor(p1: string, p2: string) {}
}

class RemoveClass {}

class RemoveMethodClass {
    removeMethod(a: string): void;
    removeArrowFunc(a: string): void;
}

class RemovePropClass {
    removeProp: string;
}

```
