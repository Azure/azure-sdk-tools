export interface Span {
    filename: string;
    begin: [number, number];
    end: [number, number];
}

export interface Item {
    id: string;
    name?: string;
    docs?: string;
    visibility: string;
    span?: Span;
    inner: any;
}

export interface ApiJson {
    crate_version: string;
    includes_private: boolean;
    index: { [key: string]: Item };
}