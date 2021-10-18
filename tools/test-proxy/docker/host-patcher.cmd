@echo OFF
REM This script modifies the HOSTS file of the test-proxy windows docker image.
REM This is necessary because the assignation of host "host.docker.internal" is not
REM working correctly automatically. This is a common problem that has phased in and out
REM of existence on docker for windows. The below script is a straightforward way of 
REM ensuring that the internal hostname works just like on linux docker.
REM
REM To use this script, add it to the ENTRYPOINT of your windows docker container.
REM It is necessary to add to startup, as docker automatically configures the HOSTS file
REM on each new invocation of a docker container. We need to update the file AFTER the 
REM container is already running.
REM 
REM The below command invokes ipconfig, the output of which looks like below:
REM Windows IP Configuration
REM Ethernet adapter Ethernet:
REM    Connection-specific DNS Suffix  . : corp.microsoft.com
REM    Link-local IPv6 Address . . . . . : fe80::20f0:fead:af44:9e18%4
REM    IPv4 Address. . . . . . . . . . . : 172.28.25.93
REM      ^     ^                                ^
REM      1     2                                14
REM    Subnet Mask . . . . . . . . . . . : 255.255.240.0
REM    Default Gateway . . . . . . . . . : 172.28.16.1
REM After invoking, it iterates across each line, parsing tokens 1, 2, and 14.
REM A "token" is a unit of text surrounded by whitespace. If it discovers the line
REM we are looking for, assign it to variable IPADDR.
for /f "tokens=1-2,14" %%i in ('ipconfig') do ^
if "%%i %%j"=="IPv4 Address." set IPADDR=%%k

REM delete existing hosts file, replace with working configuration.
del "C:\Windows\System32\drivers\etc\hosts"
echo %IPADDR%    host.docker.internal >> "C:\Windows\System32\drivers\etc\hosts"

