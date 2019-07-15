// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace MS.Az.Mgmt.CI.BuildTasks.Common.Services
{
    using Microsoft.Azure.KeyVault;
    using Microsoft.Azure.KeyVault.Models;
    using Microsoft.Build.Framework;
    using Microsoft.IdentityModel.Clients.ActiveDirectory;
    using Microsoft.Rest;
    using MS.Az.Mgmt.CI.BuildTasks.Common.Base;
    using MS.Az.Mgmt.CI.BuildTasks.Common.Logger;
    using System;
    using System.Threading.Tasks;

    public class KeyVaultService : NetSdkUtilTask
    {
        #region Fields
        string _tenantAuthority;
        string _spnSecret;
        KeyVaultClient _keyVaultClient;
        AuthenticationResult _authToken;
        DateTimeOffset expiryOffSet;

        #endregion

        #region Properties
        public override string NetSdkTaskName => "KeyVaultClient";

        string clientId = CommonConstants.AzureAuth.SToS_ClientId;

        string SpnClientId => CommonConstants.AzureAuth.SToS_ClientId;
        string SpnSecret
        {
            get
            {
                if(string.IsNullOrWhiteSpace(_spnSecret))
                {
                    _spnSecret = Environment.GetEnvironmentVariable(CommonConstants.AzureAuth.SToS_ClientSecret);
                }

                return _spnSecret;
            }
        }

        public string TenantAuthority
        {
            get
            {
                if(string.IsNullOrWhiteSpace(_tenantAuthority))
                {
                    _tenantAuthority = string.Format(@"{0}/{1}/", CommonConstants.AzureAuth.AADAuthUriString, CommonConstants.AzureAuth.MSFTTenantId);
                }

                return _tenantAuthority;
            }
        }

        bool IsTokenExpired
        {
            get
            {
                if (DateTime.UtcNow >= expiryOffSet)
                    return true;
                else
                    return false;
            }
        }

        AuthenticationResult AuthToken
        {
            get
            {
                if(_authToken == null)
                {
                    _authToken = GetAdalToken();
                }
                else
                {
                    if(IsTokenExpired)
                    {
                        _authToken = GetAdalToken();
                    }
                }

                return _authToken;
            }
            set
            {
                _authToken = value;
            }
        }

        public KeyVaultClient KVClient
        {
            get
            {
                if(_keyVaultClient == null)
                {
                    string accessToken = AuthToken.AccessToken;
                    ServiceClientCredentials svcCred = new TokenCredentials(accessToken);                    
                    _keyVaultClient = new KeyVaultClient(svcCred);
                }

                return _keyVaultClient;
            }
        }
        #endregion

        #region Constructors
        public KeyVaultService()
        {
            Init();
        }

        public KeyVaultService(NetSdkBuildTaskLogger utilLog) : base(utilLog)
        {
            Init();
        }

        void Init()
        {
            expiryOffSet = DateTime.MinValue;
        }
        
        #endregion

        #region Public Functions
        /// <summary>
        /// Retrieves secrets from keyvault
        /// </summary>
        /// <param name="secretIdentifier">Secret Identifier (e.g. http://kvname.valut.azure.net/secrets/secretname..... )</param>
        /// <returns></returns>
        public string GetSecret(string secretIdentifier)
        {
            if (IsTokenExpired)
            {
                AuthToken = null;   
            }

            Task<SecretBundle> GHUserName = Task.Run(async () => await KVClient.GetSecretAsync(secretIdentifier).ConfigureAwait(false));
            SecretBundle sb = GHUserName.Result;
            return sb.Value;
        }

        #endregion

        #region Private functions
        /// <summary>
        /// TODO: Move to MSAL ASAP
        /// </summary>
        /// <returns></returns>
        AuthenticationResult GetAdalToken()
        {
            AuthenticationResult token = null;
            ClientCredential cc = new ClientCredential(clientId, SpnSecret);
            AuthenticationContext authCtx = new AuthenticationContext(TenantAuthority, true);

            try
            {
                Task<AuthenticationResult> authResult = Task.Run(async () => await authCtx.AcquireTokenAsync(CommonConstants.AzureAuth.KVInfo.KVAudience, cc).ConfigureAwait(false));
                token = authResult.Result;
                UtilLogger.LogInfo(MessageImportance.Low, "Successfully acquired Access Token");
                expiryOffSet = token.ExpiresOn;
            }
            catch(Exception ex)
            {
                UtilLogger.LogError(ex.ToString());
            }

            return token;
        }

        async Task<string> GetAccessToken(string authority, string resource, string scope)
        {
            ClientCredential clientCredential = new ClientCredential(clientId, SpnSecret);
            var context = new AuthenticationContext(authority, TokenCache.DefaultShared);
            var result = await context.AcquireTokenAsync(resource, clientCredential);
            return result.AccessToken;
        }
        #endregion
    }
}
