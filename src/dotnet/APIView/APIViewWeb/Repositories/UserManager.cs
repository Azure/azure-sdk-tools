// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using Microsoft.AspNetCore.Authorization;
using Octokit;

namespace APIViewWeb
{
    public class UserManager
    {
        private CosmosUserRepository _userRepository;

        public UserManager(CosmosUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        public async Task createUser(ClaimsPrincipal User, string Email, HashSet<string> Langauges = null)
        {
            await _userRepository.upsertUserAsync(User, new UserModel(User, Email, Langauges));
        }

        public async Task<UserModel> getUser(ClaimsPrincipal User)
        {
            return await _userRepository.getUserAsync(User);
        }

        public async Task updateEmail(ClaimsPrincipal User, string email)
        {
            UserModel user = await getUser(User);
            user.Email = email;
            await _userRepository.upsertUserAsync(User, user);
        }

        public async Task updateLanguages(ClaimsPrincipal User, HashSet<string> languages)
        {
            UserModel user = await getUser(User);
            if(languages != null)
            {
                user.Languages = languages;
            }
            else
            {
                user.Languages = new HashSet<string>();
            }
            await _userRepository.upsertUserAsync(User, user);
        }

    }
}
