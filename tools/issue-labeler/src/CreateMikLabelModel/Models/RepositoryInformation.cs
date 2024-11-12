// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace CreateMikLabelModel.Models
{
    public record RepositoryInformation(string Owner, string Name)
    {
       /// <summary>
       ///   Creates a new instance of the <see cref="RepositoryInformation" /> by parsing a repository
       ///   path.
       /// </summary>
       ///
       /// <param name="repositoryPath">The full repository path, in the format "Owner/repository-name".</param>
       ///
       /// <example>
       ///   <code>
       ///       var info = RepositoryInformation.Parse("Azure/azure-sdk-for-net");
       ///   </code>
       /// </example>
       ///
       public static RepositoryInformation Parse(string repositoryPath)
       {
           var parts = repositoryPath.Split('/');
           return new(parts[0], parts[1]);
       }
    }
}
