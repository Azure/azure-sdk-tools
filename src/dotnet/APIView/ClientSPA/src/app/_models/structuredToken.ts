export class StructuredToken {
    value: string;
    id: string;
    kind: string;
    tags: Set<string>;
    properties: { [key: string]: string; };
    renderClasses: Set<string>;

    constructor(
        value = '',
        id = '',
        kind = '',
        tags = new Set<string>(),
        properties = {},
        renderClasses = new Set<string>()
    ) {
        this.value = value;
        this.id = id;
        this.kind = kind;
        this.tags = tags;
        this.properties = properties;
        this.renderClasses = renderClasses;
    }
}