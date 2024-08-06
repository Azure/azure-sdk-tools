export class StructuredToken {
    value: string;
    id: string;
    kind: string;
    tags: Set<string>;
    properties: { [key: string]: string; };
    renderClasses: Set<string>;

    constructor() {
        this.value = '';
        this.id = '';
        this.kind = '';
        this.tags = new Set();
        this.properties = {};
        this.renderClasses = new Set();
    }
}