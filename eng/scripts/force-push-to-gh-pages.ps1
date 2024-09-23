# ASSUMPTIONS BEFORE RUNNING
# * that an environment variable $env:GH_TOKEN is populated with the appropriate PAT to allow pushing of github releases
# * that there exists a gh-pages-root as a SOURCE branch that we can safely append all doc changes to
# * that there exists a gh-pages TARGET branch that we can safely force push our changes to

param (
  # example: $(Release.Artifacts.<artifactName>.SourceVersion)
  $shaVersions, # comma separated list of sha versions that correspond to the set of artifact directories
  
  # example: $(System.ArtifactsDirectory)/_artifacts/docfolder/sphinx
  $changedDirectories, # comma separated list of paths to artifact directories

  # example: $(System.DefaultWorkingDirectory)/azure-sdk-for-python-ghpages
  $cloneDirectory, # the working directory where git actions will take place.

  $user="azure-sdk",
  $email="azure-sdk@users.noreply.github.com",

  $repoId = "" # the full name of the target repository. EG "Azure/azure-sdk-for-java"
)

git clone --depth 1 --branch gh-pages-root https://github.com/$repoId.git $cloneDirectory
cd $cloneDirectory

# reset back to a safe commit point
git checkout -b gh-pages-temp

# apply changes from change directories
$sourceDirs = $changedDirectories -Split ","

foreach($directory in $sourceDirs)
{
  Copy-Item -Path $directory/* -Destination $cloneDirectory -Recurse -Force
}

git add -A .
git -c user.name="$user" -c user.email="$email" commit -m "Auto-generated docs from $repoName SHA(s) $shaVersions"
git push https://$($env:GH_TOKEN)@github.com/$repoId.git -f gh-pages-temp:gh-pages
