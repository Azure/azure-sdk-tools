const InjectableTypes = {
    Config: Symbol.for('Config'),
    Coordinator: Symbol.for('Coordinator'),
    SpecRetriever: Symbol.for('SpecRetriever'),
    ResponseGenerator: Symbol.for('ResponseGenerator')
}

export { InjectableTypes }
