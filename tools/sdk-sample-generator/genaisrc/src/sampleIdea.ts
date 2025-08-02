export const sampleIdeaSchemaDefinition = {
    type: "array",
    description: "A list of sample ideas.",
    items: {
        type: "object",
        description: "A sample idea",
        properties: {
            name: {
                type: "string",
                description: "The name of the sample idea",
            },
            description: {
                type: "string",
                description: "The description of the sample",
            },
            fileName: {
                type: "string",
                description:
                    "The name of the file where the sample code will be saved. USE camel case and DO NOT include the file extension.",
            },
            requests: {
                type: "array",
                description: "A list of requests that the sample will send",
                items: {
                    type: "object",
                    description: "A request that the sample will send",
                    properties: {
                        path: {
                            type: "string",
                            description:
                                "The path the request will be sent to. It MUST include all path segments. Path segments must be complete and includes special characters if any including colons, slashes, and dots.",
                        },
                        description: {
                            type: "string",
                            description: "The description of the request.",
                        },
                        method: {
                            type: "string",
                            description: "The HTTP method of the request.",
                        },
                        queryParams: {
                            type: "array",
                            description:
                                "The query parameters of the request. If the service uses API versions in the query parameters, include it here.",
                            items: {
                                type: "object",
                                description:
                                    "A query parameter of the request.",
                                properties: {
                                    name: {
                                        type: "string",
                                        description:
                                            "The name of the query parameter.",
                                    },
                                    value: {
                                        type: "string",
                                        description:
                                            "The value of the query parameter.",
                                    },
                                },
                            },
                        },
                        headers: {
                            type: "array",
                            description:
                                "The headers of the request. The content-type header with the correct value MUST be included here.",
                            items: {
                                type: "object",
                                description: "A header of the request.",
                                properties: {
                                    name: {
                                        type: "string",
                                        description: "The name of the header.",
                                    },
                                    value: {
                                        type: "string",
                                        description: "The value of the header.",
                                    },
                                },
                            },
                        },
                        body: {
                            type: "object",
                            description:
                                "The body of the request. It must be complete and conforming to the API specification",
                        },
                    },
                },
            },
            prerequisites: {
                type: "object",
                description: "Description of the prerequisites for the sample",
                properties: {
                    setup: {
                        type: "string",
                        description:
                            "The setup the sample needs excluding setting up the main resource",
                    },
                    additionalResources: {
                        type: "array",
                        description:
                            "A list of additional resources required by the sample.",
                        items: {
                            type: "object",
                            description:
                                "A resource required by the sample other than the main resource.",
                            properties: {
                                type: {
                                    type: "string",
                                    description: "The type of the resource.",
                                },
                            },
                        },
                    },
                },
            },
        },
        required: ["name", "fileName", "description", "requests"],
    },
} as JSONSchema;
