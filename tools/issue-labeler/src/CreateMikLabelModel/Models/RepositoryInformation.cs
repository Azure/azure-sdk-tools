// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

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
