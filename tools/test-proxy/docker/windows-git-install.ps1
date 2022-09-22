mkdir -p /git
Invoke-RestMethod -Uri "https://github.com/git-for-windows/git/releases/download/v2.37.3.windows.1/MinGit-2.37.3-64-bit.zip" -OutFile "/git/git.zip"
Expand-Archive "/git/git.zip" -DestinationPath "/git/" -Force && Remove-Item "/git/git.zip"
