# Note, due to how `Expand-Archive` is leveraged in this script,
# powershell core is a requirement for successful execution.
param (
  $ArtifactLocation, # the root of the artifact folder. DevOps $(System.ArtifactsDirectory)
  $Language
)

if ($Language -eq "javascript")
{
    $PublishedDocs = Get-ChildItem "$($DocLocation)/documentation" | Where-Object -FilterScript {$_.Name.EndsWith(".zip")}

    foreach ($Item in $PublishedDocs) {
        $PkgName = "azure-$($Item.BaseName)"
        Write-Host $PkgName
        Expand-Archive -Force -Path "$($DocLocation)/documentation/$($Item.Name)" -DestinationPath "$($DocLocation)/documentation/$($Item.BaseName)"
        $dirList = Get-ChildItem "$($DocLocation)/documentation/$($Item.BaseName)/$($Item.BaseName)" -Attributes Directory

        if($dirList.Length -eq 1){
            $DocVersion = $dirList[0].Name
            Write-Host "Uploading Doc for $($PkgName) Version:- $($DocVersion)..."
            Upload-Blobs -DocDir "$($DocLocation)/documentation/$($Item.BaseName)/$($Item.BaseName)/$($DocVersion)" -PkgName $PkgName -DocVersion $DocVersion
        }
        else{
            Write-Host "found more than 1 folder under the documentation for package - $($Item.Name)"
        }
    }
}

if ($Language -eq "dotnet")
{
    $PublishedPkgs = Get-ChildItem "$($DocLocation)/packages" | Where-Object -FilterScript {$_.Name.EndsWith(".nupkg") -and -not $_.Name.EndsWith(".symbols.nupkg")}
    $PublishedDocs = Get-ChildItem "$($DocLocation)" | Where-Object -FilterScript {$_.Name.StartsWith("Docs.")}

    foreach ($Item in $PublishedDocs) {
        $PkgName = $Item.Name.Remove(0, 5)
        $PkgFullName = $PublishedPkgs | Where-Object -FilterScript {$_.Name -match "$($PkgName).\d"}

        if (($PkgFullName | Measure-Object).count -eq 1)
        {
            $DocVersion = $PkgFullName[0].BaseName.Remove(0, $PkgName.Length + 1)

            Write-Host "Start Upload for $($PkgName)/$($DocVersion)"
            Write-Host "DocDir $($Item)"
            Write-Host "PkgName $($PkgName)"
            Write-Host "DocVersion $($DocVersion)"
            Upload-Blobs -DocDir "$($Item)" -PkgName $PkgName -DocVersion $DocVersion
        }
        else
        {
            Write-Host "Package with the same name Exists. Upload Skipped"
            continue
        }
    }
}

if ($Language -eq "python")
{
    $PublishedDocs = Get-ChildItem "$DocLocation" | Where-Object -FilterScript {$_.Name.EndsWith(".zip")}

    foreach ($Item in $PublishedDocs) {
        $PkgName = $Item.BaseName
        $ZippedDocumentationPath = Join-Path -Path $DocLocation -ChildPath $Item.Name
        $UnzippedDocumentationPath = Join-Path -Path $DocLocation -ChildPath $PkgName
        $VersionFileLocation = Join-Path -Path $UnzippedDocumentationPath -ChildPath "version.txt"

        Expand-Archive -Force -Path $ZippedDocumentationPath -DestinationPath $UnzippedDocumentationPath

        $Version = $(Get-Content $VersionFileLocation).Trim()

        Write-Host "Discovered Package Name: $PkgName"
        Write-Host "Discovered Package Version: $Version"
        Write-Host "Directory for Upload: $UnzippedDocumentationPath"

        Upload-Blobs -DocDir $UnzippedDocumentationPath -PkgName $PkgName -DocVersion $Version
    }
}

if ($Language -eq "java")
{
    $PublishedDocs = Get-ChildItem "$DocLocation" | Where-Object -FilterScript {$_.Name.EndsWith("-javadoc.jar")}
    foreach ($Item in $PublishedDocs) {
        $UnjarredDocumentationPath = ""
        try {
            $PkgName = $Item.BaseName
            # The jar's unpacking command doesn't allow specifying a target directory
            # and will unjar all of the files in whatever the current directory is.
            # Create a subdirectory to unjar into, set the location, unjar and then
            # set the location back to its original location.
            $UnjarredDocumentationPath = Join-Path -Path $DocLocation -ChildPath $PkgName
            New-Item -ItemType directory -Path "$UnjarredDocumentationPath"
            $CurrentLocation = Get-Location
            Set-Location $UnjarredDocumentationPath
            jar -xf "$($Item.FullName)"
            Set-Location $CurrentLocation

            # Get the POM file for the artifact we're processing
            $PomFile = $Item.FullName.Substring(0,$Item.FullName.LastIndexOf(("-javadoc.jar"))) + ".pom"
            Write-Host "PomFile $($PomFile)"

            # Pull the version from the POM
            [xml]$PomXml = Get-Content $PomFile
            $Version = $PomXml.project.version
            $ArtifactId = $PomXml.project.artifactId

            Write-Host "Start Upload for $($PkgName)/$($Version)"
            Write-Host "DocDir $($UnjarredDocumentationPath)"
            Write-Host "PkgName $($ArtifactId)"
            Write-Host "DocVersion $($Version)"

        } Finally {
            if (![string]::IsNullOrEmpty($UnjarredDocumentationPath)) {
                if (Test-Path -Path $UnjarredDocumentationPath) {
                    Write-Host "Cleaning up $UnjarredDocumentationPath"
                    Remove-Item -Recurse -Force $UnjarredDocumentationPath
                }
            }
        }
    }
}

if ($Language -eq "c")
{
    # The documentation publishing process for C differs from the other
    # langauges in this file because this script is invoked once per library
    # publishing. It is not, for example, invoked once per service publishing.
    # This is also the case for other langauge publishing steps above... Those
    # loops are left over from previous versions of this script which were used
    # to publish multiple docs packages in a single invocation.
    $pkgInfo = Get-Content $DocLocation/package-info.json | ConvertFrom-Json
    $pkgName = $pkgInfo.name
    $pkgVersion = $pkgInfo.version

}