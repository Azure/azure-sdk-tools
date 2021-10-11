import {
    BaseHttpController,
    HttpResponseMessage,
    JsonContent,
    all,
    controller,
    httpGet
} from 'inversify-express-utils'

@controller('/')
export class AuthServerController extends BaseHttpController {
    constructor() {
        super()
    }

    @httpGet('common/.well-known/openid-configuration')
    public async mockFingerPrint() {
        const result = {
            token_endpoint: 'https://localhost/common/oauth2/token',
            token_endpoint_auth_methods_supported: [
                'client_secret_post',
                'private_key_jwt',
                'client_secret_basic'
            ],
            jwks_uri: 'https://localhost/common/discovery/keys',
            response_modes_supported: ['query', 'fragment', 'form_post'],
            subject_types_supported: ['pairwise'],
            id_token_signing_alg_values_supported: ['RS256'],
            response_types_supported: [
                'code',
                'id_token',
                'code id_token',
                'token id_token',
                'token'
            ],
            scopes_supported: ['openid'],
            issuer: 'https://sts.microsoftonline.de/{tenantid}/',
            microsoft_multi_refresh_token: true,
            authorization_endpoint: 'https://localhost/common/oauth2/authorize',
            device_authorization_endpoint: 'https://localhost/common/oauth2/devicecode',
            http_logout_supported: true,
            frontchannel_logout_supported: true,
            end_session_endpoint: 'https://localhost/common/oauth2/logout',
            claims_supported: [
                'sub',
                'iss',
                'cloud_instance_name',
                'cloud_instance_host_name',
                'cloud_graph_host_name',
                'msgraph_host',
                'aud',
                'exp',
                'iat',
                'auth_time',
                'acr',
                'amr',
                'nonce',
                'email',
                'given_name',
                'family_name',
                'nickname'
            ],
            check_session_iframe: 'https://localhost/common/oauth2/checksession',
            userinfo_endpoint: 'https://localhost/common/openid/userinfo',
            tenant_region_scope: null,
            cloud_instance_name: 'microsoftonline.de',
            cloud_graph_host_name: 'graph.cloudapi.de',
            msgraph_host: 'graph.microsoft.de',
            rbac_url: 'https://pas.cloudapi.de'
        }
        const response = new HttpResponseMessage(200)
        response.content = new JsonContent(result)
        return response
    }

    @all('*/oauth2/token')
    @all('*/oauth2/v2.0/token')
    public async mockLogin() {
        const jwt = {
            token_type: 'Bearer',
            expires_in: '200086399',
            ext_expires_in: '200086399',
            expires_on: '2609221607',
            not_before: '1609134907',
            resource: 'https://management.azure.com/',
            access_token: 'ss'
        }
        const response = new HttpResponseMessage(200)
        response.content = new JsonContent(jwt)
        return response
    }

    @all('subscriptions')
    public async mockGetSubscriptions() {
        const result = {
            value: [
                {
                    cloudName: 'mock',
                    homeTenantId: '00000000-0000-0000-0000-000000000000',
                    id: '00000000-0000-0000-0000-000000000000',
                    isDefault: false,
                    managedByTenants: [
                        {
                            tenantId: '00000000-0000-0000-0000-000000000000'
                        }
                    ],
                    name: 'Code generate Test and Infra',
                    state: 'Enabled',
                    tenantId: '00000000-0000-0000-0000-000000000000',
                    user: {
                        name: '00000000-0000-0000-0000-000000000000',
                        type: 'servicePrincipal'
                    }
                }
            ]
        }
        const response = new HttpResponseMessage(200)
        response.content = new JsonContent(result)
        return response
    }

    @all('*/servicePrincipals')
    public async mockGraphService() {
        const result = {
            'odata.metadata':
                'https://graph.windows.net/0000000-0000-0000-0000-000000000000/$metadata#directoryObjects',
            value: [
                {
                    'odata.type': 'Microsoft.DirectoryServices.ServicePrincipal',
                    objectType: 'ServicePrincipal',
                    objectId: '4faa32fd-963a-464d-b6bf-c12cf48c0317',
                    deletionTimestamp: null,
                    accountEnabled: true,
                    addIns: [],
                    alternativeNames: [],
                    appDisplayName: 'appconfiguration',
                    appId: '0000000-0000-0000-0000-000000000000',
                    applicationTemplateId: null,
                    appOwnerTenantId: '0000000-0000-0000-0000-000000000000',
                    appRoleAssignmentRequired: false,
                    appRoles: [],
                    displayName: 'appconfiguration',
                    errorUrl: null,
                    homepage: null,
                    informationalUrls: {
                        termsOfService: null,
                        support: null,
                        privacy: null,
                        marketing: null
                    },
                    keyCredentials: [],
                    logoutUrl: null,
                    notificationEmailAddresses: [],
                    oauth2Permissions: [],
                    passwordCredentials: [],
                    preferredSingleSignOnMode: null,
                    preferredTokenSigningKeyEndDateTime: null,
                    preferredTokenSigningKeyThumbprint: null,
                    publisherName: 'Microsoft',
                    replyUrls: [],
                    samlMetadataUrl: null,
                    samlSingleSignOnSettings: null,
                    servicePrincipalNames: ['0000000-0000-0000-0000-000000000000'],
                    servicePrincipalType: 'Application',
                    signInAudience: 'AzureADMyOrg',
                    tags: ['WindowsAzureActiveDirectoryIntegratedApp'],
                    tokenEncryptionKeyId: null
                }
            ]
        }
        const response = new HttpResponseMessage(200)
        response.content = new JsonContent(result)
        return response
    }

    @all('subscriptions/*/providers')
    public async mockProviderService() {
        const result = {
            value: [
                {
                    id:
                        '/subscriptions/0000000-0000-0000-0000-000000000000/providers/Microsoft.Marketplace',
                    namespace: 'Microsoft.Marketplace',
                    authorizations: [
                        {
                            applicationId: '0000000-0000-0000-0000-000000000000'
                        },
                        {
                            applicationId: '0000000-0000-0000-0000-000000000000'
                        }
                    ],
                    resourceTypes: [
                        {
                            resourceType: 'register',
                            locations: [],
                            apiVersions: ['2020-01-01']
                        }
                    ],
                    registrationState: 'Registered'
                }
            ]
        }
        const response = new HttpResponseMessage(200)
        response.content = new JsonContent(result)
        return response
    }
}
