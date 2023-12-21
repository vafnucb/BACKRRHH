@ECHO OFF

set rootpath=%~dp0
set destinationprod="C:\inetpub\wwwroot\RRHH"
set destination=""C:\Users\Juanpi\Desktop\dev\www.Personas""

call :strLen rootpath strlen
set /a strlen=%strlen%-8

CALL SET prevpath=%%rootpath:~0,%strlen%%%
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe  "C:\Users\Juanpi\Desktop\dev\Actual\Ucb.Back.net\UcbBack.sln" /p:Configuration=Debug /p:Platform="Any CPU" /p:VisualStudioVersion=12.0 /t:Rebuild


:: rmdir /s /q "%destination%\"

mkdir "%destination%\Areas"
robocopy "%rootpath%\Areas" "C:\Users\Juanpi\Desktop\dev\www.Personas\Areas" /E /COPYALL /it /NFL /NDL /NJH /NJS /nc /ns /np

mkdir "%destination%\bin"
robocopy "%rootpath%\bin" "C:\Users\Juanpi\Desktop\dev\www.Personas\bin" /E /COPYALL /it /NFL /NDL /NJH /NJS /nc /ns /np

mkdir "%destination%\Content"
robocopy "%rootpath%\Content" "C:\Users\Juanpi\Desktop\dev\www.Personas\Content" /E /COPYALL /it /NFL /NDL /NJH /NJS /nc /ns /np

mkdir "%destination%\fonts"
robocopy "%rootpath%\fonts" "C:\Users\Juanpi\Desktop\dev\www.Personas\fonts" /E /COPYALL /it /NFL /NDL /NJH /NJS /nc /ns /np

mkdir "%destination%\Images"
robocopy "%rootpath%\Images" "C:\Users\Juanpi\Desktop\dev\www.Personas\Images" /E /COPYALL /is /NFL /NDL /NJH /NJS /nc /ns /np

mkdir "%destination%\Scripts"
robocopy "%rootpath%\Scripts" "C:\Users\Juanpi\Desktop\dev\www.Personas\Scripts" /E /COPYALL /it /NFL /NDL /NJH /NJS /nc /ns /np

mkdir "%destination%\Views"
robocopy "%rootpath%\Views" "C:\Users\Juanpi\Desktop\dev\www.Personas\Views" /E /COPYALL /it /NFL /NDL /NJH /NJS /nc /ns /np

robocopy "%rootpath%\" "C:\Users\Juanpi\Desktop\dev\www.Personas\\" favicon.ico /COPYALL /it /NFL /NDL /NJH /NJS /nc /ns /np
robocopy "%rootpath%\" "C:\Users\Juanpi\Desktop\dev\www.Personas\\" Global.asax /COPYALL /it /NFL /NDL /NJH /NJS /nc /ns /np
robocopy "%rootpath%\" "C:\Users\Juanpi\Desktop\dev\www.Personas\\" "packages.config" /COPYALL /it /NFL /NDL /NJH /NJS /nc /ns /np
robocopy "%rootpath%\" "C:\Users\Juanpi\Desktop\dev\www.Personas\\" "Web.config" /COPYALL /it /NFL /NDL /NJH /NJS /nc /ns /np




mkdir "%destination%\Static"
robocopy "%prevpath%\Front\dist\static" "C:\Users\Juanpi\Desktop\dev\www.Personas\Static" /E /COPYALL /is /NFL /NDL /NJH /NJS /nc /ns /np
robocopy "%prevpath%\Front\dist\\" "C:\Users\Juanpi\Desktop\dev\www.Personas\Views\Home\\" "index.html" /COPYALL /is /NFL /NDL /NJH /NJS /nc /ns /np


echo "@{    Layout = "";   }" > "C:\Users\Juanpi\Desktop\dev\www.Personas\Views\Home\Index.cshtml"
type "C:\Users\Juanpi\Desktop\dev\www.Personas\Views\Home\index.html" >> "C:\Users\Juanpi\Desktop\dev\www.Personas\Views\Home\Index.cshtml"

ECHO ON
exit /b

:strLen
setlocal enabledelayedexpansion
:strLen_Loop
  if not "!%1:~%len%!"=="" set /A len+=1 & goto :strLen_Loop
(endlocal & set %2=%len%)
goto :eof
