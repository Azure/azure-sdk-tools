import * as convict from 'convict';

import { RepoType, SDK } from './commonType';

export interface RepoInfo {
    type: string;
    path: string;
    branch: string;
}

export class PublishResultConfig {
    mongodb: {
        server: string;
        port: number;
        database: string;
        username: string;
        password: string;
        ssl: boolean;
    };
    defaultCodegenRepo: RepoInfo;
    defaultSwaggerRepo: RepoInfo;
    defaultSDKRepos: {
        [key: string]: RepoInfo;
    };
}

export const publishResultConfig = convict<PublishResultConfig>({
    mongodb: {
        server: {
            doc: 'The host used to connect db',
            format: String,
            default: '',
            env: 'sdkGenerationMongoDbHost',
        },
        port: {
            doc: 'The port used to connect db',
            format: Number,
            default: 10225,
            env: 'sdkGenerationMongoDbPort',
        },
        database: {
            doc: 'The database used to connect db',
            format: String,
            default: '',
            env: 'sdkGenerationMongoDbDatabase',
        },
        username: {
            doc: 'The username used to connect db',
            format: String,
            default: '',
            env: 'sdkGenerationMongoDbUsername',
        },
        password: {
            doc: 'The password used to connect db',
            format: String,
            default: '',
            env: 'sdkGenerationMongoDbPassword',
        },
        ssl: {
            doc: 'Whether used ssl to connect db',
            format: Boolean,
            default: true,
            env: 'sdkGenerationMongoDbSsl',
        },
    },
    defaultCodegenRepo: {
        type: {
            doc: 'The codegen repository type.',
            format: String,
            default: RepoType.Github,
        },
        path: {
            doc: 'The url path of codegen repository.',
            format: String,
            default: 'https://github.com/Azure/azure-sdk-pipeline',
        },
        branch: {
            doc: 'The main branch of codegen repository.',
            format: String,
            default: 'dev',
        },
    },
    defaultSwaggerRepo: {
        type: {
            doc: 'The swagger repository type.',
            format: String,
            default: RepoType.Github,
        },
        path: {
            doc: 'The url path of swagger repository.',
            format: String,
            default: 'https://github.com/AzureSDKPipelineBot/azure-rest-api-specs',
        },
        branch: {
            doc: 'The main branch of swagger repository.',
            format: String,
            default: 'main',
        },
    },
    defaultSDKRepos: {
        // doc: "The map of sdk repositories.[sdk]:[sdk repository configuration]",
        // format: (val)=>{/*noop */},
        // default: {}
        [SDK.GoSDK]: {
            type: {
                doc: 'The go repository type.',
                format: String,
                default: RepoType.Github,
            },
            path: {
                doc: 'The url path of go repository.',
                format: String,
                default: 'https://github.com/Azure/azure-sdk-for-go',
            },
            branch: {
                doc: 'The main branch of go repository.',
                format: String,
                default: 'main',
            },
        },
        [SDK.NetSDK]: {
            type: {
                doc: 'The dotnet repository type.',
                format: String,
                default: RepoType.Github,
            },
            path: {
                doc: 'The url path of dotnet repository.',
                format: String,
                default: 'https://github.com/Azure/azure-sdk-for-net',
            },
            branch: {
                doc: 'The main branch of dotnet repository.',
                format: String,
                default: 'main',
            },
        },
        [SDK.JsSDK]: {
            type: {
                doc: 'The js repository type.',
                format: String,
                default: RepoType.Github,
            },
            path: {
                doc: 'The url path of js repository.',
                format: String,
                default: 'https://github.com/Azure/azure-sdk-for-js',
            },
            branch: {
                doc: 'The main branch of js repository.',
                format: String,
                default: 'main',
            },
        },
        [SDK.JavaSDK]: {
            type: {
                doc: 'The java repository type.',
                format: String,
                default: RepoType.Github,
            },
            path: {
                doc: 'The url path of java repository.',
                format: String,
                default: 'https://github.com/Azure/azure-sdk-for-net',
            },
            branch: {
                doc: 'The main branch of java repository.',
                format: String,
                default: 'main',
            },
        },
        [SDK.PythonSDK]: {
            type: {
                doc: 'The python repository type.',
                format: String,
                default: RepoType.Github,
            },
            path: {
                doc: 'The url path of python repository.',
                format: String,
                default: 'https://github.com/Azure/azure-sdk-for-net',
            },
            branch: {
                doc: 'The main branch of python repository.',
                format: String,
                default: 'main',
            },
        },
    },
});
