@echo OFF
REM ipconfig output looks like this:
REM Windows IP Configuration
REM Ethernet adapter Ethernet:
REM    Connection-specific DNS Suffix  . : corp.microsoft.com
REM    Link-local IPv6 Address . . . . . : fe80::20f0:fead:af44:9e18%4
REM    IPv4 Address. . . . . . . . . . . : 172.28.25.93
REM    Subnet Mask . . . . . . . . . . . : 255.255.240.0
REM    Default Gateway . . . . . . . . . : 172.28.16.1

for /f "tokens=1-2,14" %%i in ('ipconfig') do ^
if "%%i %%j"=="IPv4 Address." set IPADDR=%%k

del "C:\Windows\System32\drivers\etc\hosts"
echo %IPADDR%    host.docker.internal >> "C:\Windows\System32\drivers\etc\hosts"

