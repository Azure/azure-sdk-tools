$username = New-Guid
$password = New-Guid
$salt = New-Guid

$basicAuthorizationHeaderValue = "$($username):$($password)"
$basicAuthorizationHeaderValueBytes = [System.Text.Encoding]::UTF8.GetBytes($basicAuthorizationHeaderValue)
$encodedBasicAuthorizationHeaderValue = [System.Convert]::ToBase64String($basicAuthorizationHeaderValueBytes)
$saltedEncodedBasicAuthorizationHeaderValue = "$encodedBasicAuthorizationHeaderValue$salt"
$saltedEncodedBasicAuthorizationHeaderValueBytes = [System.Text.Encoding]::UTF8.GetBytes($saltedEncodedBasicAuthorizationHeaderValue)

$sha256 = [System.Security.Cryptography.SHA256CryptoServiceProvider]::new()
$hashBytes = $sha256.ComputeHash($saltedEncodedBasicAuthorizationHeaderValueBytes)
$encodedHash = [System.Convert]::ToBase64String($hashBytes)

Write-Host "Username: $username"
Write-Host "Password: $password"
Write-Host "Salt: $salt"
Write-Host "Hash: $encodedHash"
