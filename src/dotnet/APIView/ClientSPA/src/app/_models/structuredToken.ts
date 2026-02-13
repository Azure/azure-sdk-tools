export class StructuredToken {
    value: string;
    id: string;
    kind: string;
    tags: Set<string>;
    properties: { [key: string]: string; };
    renderClasses: Set<string>;
    
    // Cached computed values for performance - avoid recalculating on every change detection
    cachedClassObject: { [key: string]: boolean } | null = null;
    cachedNavigationId: string | null = null;
    cachedNavigationUrl: string | null = null;
    // Use string instead of object to avoid __spreadValues overhead
    cachedClassString: string = '';

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