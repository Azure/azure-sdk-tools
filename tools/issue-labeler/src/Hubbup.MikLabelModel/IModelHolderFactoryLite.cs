// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Hubbup.MikLabelModel;
using IssueLabeler.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Hubbup.MikLabelModel
{
    public interface IModelHolderFactoryLite
    {
        Task<IModelHolder[]> CreateModelHolders(string owner, string repo, string[] modelConfigNames);
        Task<IModelHolder> CreateModelHolder(string owner, string repo, string modelBlobConfigName = null);
        Task<IPredictor> GetPredictor(string owner, string repo, string modelBlobConfigName = null);
    }
    public class ModelHolderFactoryLite : IModelHolderFactoryLite
    {
        private readonly ConcurrentDictionary<(string, string, string), IModelHolder> _models = new ConcurrentDictionary<(string, string, string), IModelHolder>();
        private readonly ILogger<ModelHolderFactoryLite> _logger;
        private IConfiguration _configuration;
        private SemaphoreSlim _sem = new SemaphoreSlim(1,1);

        public ModelHolderFactoryLite(
            ILogger<ModelHolderFactoryLite> logger,
            IConfiguration configuration)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<IModelHolder[]> CreateModelHolders(string owner, string repo, string[] modelConfigNames)
        {
            var modelHolders = new IModelHolder[modelConfigNames.Length];
            var allHeld = true;

            // If all of the models are already held, return them.
            for (int index = 0; index < modelConfigNames.Length; ++index)
            {
                if (_models.TryGetValue((owner, repo, modelConfigNames[index]), out var holder))
                {
                    modelHolders[index] = holder;
                }
                else
                {
                    // At least one model is not held.  No sense in checking the rest.
                    allHeld = false;
                    break;
                }
            }

            if (allHeld)
            {
                return modelHolders;
            }

            // Some models need to be initialized; acquire the semaphore and initialize.
            try
            {
                if (!_sem.Wait(0))
                {
                    await _sem.WaitAsync().ConfigureAwait(false);
                }

                for (int index = 0; index < modelConfigNames.Length; ++index)
                {
                    modelHolders[index] = await CreateModelHolderInternal(owner, repo, modelConfigNames[index]);
                }
            }
            finally
            {
                if (_sem.CurrentCount <= 0)
                {
                    _sem.Release();
                }
            }

            return modelHolders;
        }

        public async Task<IModelHolder> CreateModelHolder(string owner, string repo, string modelBlobConfigName = null)
        {
            if (_models.TryGetValue((owner, repo, modelBlobConfigName), out var modelHolder))
            {
                return modelHolder;
            }

            try
            {
                if (!_sem.Wait(0))
                {
                    await _sem.WaitAsync().ConfigureAwait(false);
                }

                return await CreateModelHolderInternal(owner, repo, modelBlobConfigName).ConfigureAwait(false);
            }
            finally
            {
                if (_sem.CurrentCount <= 0)
                {
                    _sem.Release();
                }
            }
        }

        public async Task<IModelHolder> CreateModelHolderInternal(string owner, string repo, string modelBlobConfigName)
        {
            IModelHolder modelHolder = null;

            if (IsConfigured(repo))
            {
                if (_models.TryGetValue((owner, repo, modelBlobConfigName), out modelHolder))
                {
                    return modelHolder;
                }

                modelHolder = await InitFor(repo, modelBlobConfigName);
                _models.GetOrAdd((owner, repo, modelBlobConfigName), modelHolder);
            }

            return modelHolder;
        }

        public async Task<IPredictor> GetPredictor(string owner, string repo, string modelBlobConfigName = null)
        {
            var modelHolder = await CreateModelHolder(owner, repo, modelBlobConfigName);
            if (modelHolder == null)
            {
                throw new InvalidOperationException($"Repo {owner}/{repo} is not yet configured for label prediction.");
            }
            if (!modelHolder.IsIssueEngineLoaded || (!modelHolder.UseIssuesForPrsToo && !modelHolder.IsPrEngineLoaded))
            {
                throw new InvalidOperationException("Issue engine must be loaded.");
            }
            return new Predictor(_logger, modelHolder) { ModelName = modelBlobConfigName };
        }

        private bool IsConfigured(string repo)
        {
            // the following four configuration values are per repo values.
            string configSection = $"IssueModel.{repo.Replace("-", "_")}.BlobConfigNames";
            if (string.IsNullOrEmpty(_configuration[configSection]))
            {
                configSection = $"IssueModel:{repo}:BlobName";
                if (!string.IsNullOrEmpty(_configuration[configSection]))
                {
                    configSection = $"IssueModel:{repo}:BlobName";
                    if (!string.IsNullOrEmpty(_configuration[configSection]))
                    {
                        configSection = $"PrModel:{repo}:PathPrefix";
                        if (!string.IsNullOrEmpty(_configuration[configSection]))
                        {
                            // has both pr and issue config - allowed
                            configSection = $"PrModel:{repo}:BlobName";
                            return !string.IsNullOrEmpty(_configuration[configSection]);
                        }
                        else
                        {
                            // has issue config only - allowed
                            configSection = $"PrModel:{repo}:BlobName";
                            return string.IsNullOrEmpty(_configuration[configSection]);
                        }
                    }
                }
            }
            else { return true; }
            return false;
        }

        private async Task<IModelHolder> InitFor(string repo, string modelBlobConfigName = null)
        {
            var mh = new ModelHolder(_logger, _configuration, repo, modelBlobConfigName);
            if (!mh.LoadRequested)
            {
                await mh.LoadEnginesAsync();
            }
            return mh;
        }
    }
}