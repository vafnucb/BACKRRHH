@ECHO OFF

git submodule update --recursive --remote

set rootpath=%~dp0
set destination="C:\inetpub\wwwroot\RRHH"
set destinationdev="C:\Users\Adrian\Desktop\www"

%systemroot%\system32\inetsrv\appcmd set apppool /apppool.name:ucbback01 /enable32BitAppOnWin64:true

mkdir "%destination%\Areas"
robocopy "%rootpath%\Areas" "%destination%\Areas" /E /COPYALL /is

mkdir "%destination%\bin"
robocopy "%rootpath%\bin" "%destination%\bin" /E /COPYALL /is

mkdir "%destination%\Content"
robocopy "%rootpath%\Content" "%destination%\Content" /E /COPYALL /is

mkdir "%destination%\fonts"
robocopy "%rootpath%\fonts" "%destination%\fonts" /E /COPYALL /is

:: mkdir "%destination%\Images"
:: robocopy "%rootpath%\Images" "%destination%\Images" /E /COPYALL /is

mkdir "%destination%\Scripts"
robocopy "%rootpath%\Scripts" "%destination%\Scripts" /E /COPYALL /is

mkdir "%destination%\Views"
robocopy "%rootpath%\Views" "%destination%\Views" /E /COPYALL /is

robocopy "%rootpath%\" "%destination%\\" favicon.ico /COPYALL /is
robocopy "%rootpath%\" "%destination%\\" Global.asax /COPYALL /is
robocopy "%rootpath%\" "%destination%\\" "packages.config" /COPYALL /is
robocopy "%rootpath%\" "%destination%\\" "Web.config" /COPYALL /is


call :strLen rootpath strlen
set /a strlen=%strlen%-8

CALL SET prevpath=%%rootpath:~0,%strlen%%%

mkdir "%destination%\Static"
robocopy "%prevpath%\Front\dist\static" "%destination%\Static" /E /COPYALL /is
robocopy "%prevpath%\Front\dist\\" "%destination%\Views\Home\\" "index.html" /COPYALL /is


echo @{    Layout = "";   } > "%destination%\Views\Home\Index.cshtml"
type "%destination%\Views\Home\index.html" >> "%destination%\Views\Home\Index.cshtml"

ECHO ON
exit /b

:strLen
setlocal enabledelayedexpansion
:strLen_Loop
  if not "!%1:~%len%!"=="" set /A len+=1 & goto :strLen_Loop
(endlocal & set %2=%len%)
goto :eof