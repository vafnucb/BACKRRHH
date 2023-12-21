@ECHO OFF

set rootpath=%~dp0
set destinationprod="C:\inetpub\wwwroot\RRHH"
set destination=""C:\Users\Adrian Rojas\Desktop\dev\www2""

call :strLen rootpath strlen
set /a strlen=%strlen%-8

CALL SET prevpath=%%rootpath:~0,%strlen%%%
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe  "C:\Users\Adrian Rojas\Desktop\dev\Ucb.Back.net\UcbBack.sln" /p:Configuration=Debug /p:Platform="Any CPU" /p:VisualStudioVersion=12.0 /t:Rebuild


:: rmdir /s /q "%destination%\"

mkdir "%destination%\Areas"
robocopy "%rootpath%\Areas" "C:\Users\Adrian Rojas\Desktop\dev\www2\Areas" /E /COPYALL /it /NFL /NDL /NJH /NJS /nc /ns /np

mkdir "%destination%\bin"
robocopy "%rootpath%\bin" "C:\Users\Adrian Rojas\Desktop\dev\www2\bin" /E /COPYALL /it /NFL /NDL /NJH /NJS /nc /ns /np

mkdir "%destination%\Content"
robocopy "%rootpath%\Content" "C:\Users\Adrian Rojas\Desktop\dev\www2\Content" /E /COPYALL /it /NFL /NDL /NJH /NJS /nc /ns /np

mkdir "%destination%\fonts"
robocopy "%rootpath%\fonts" "C:\Users\Adrian Rojas\Desktop\dev\www2\fonts" /E /COPYALL /it /NFL /NDL /NJH /NJS /nc /ns /np

mkdir "%destination%\Images"
robocopy "%rootpath%\Images" "C:\Users\Adrian Rojas\Desktop\dev\www2\Images" /E /COPYALL /is /NFL /NDL /NJH /NJS /nc /ns /np

mkdir "%destination%\Scripts"
robocopy "%rootpath%\Scripts" "C:\Users\Adrian Rojas\Desktop\dev\www2\Scripts" /E /COPYALL /it /NFL /NDL /NJH /NJS /nc /ns /np

mkdir "%destination%\Views"
robocopy "%rootpath%\Views" "C:\Users\Adrian Rojas\Desktop\dev\www2\Views" /E /COPYALL /it /NFL /NDL /NJH /NJS /nc /ns /np

robocopy "%rootpath%\" "C:\Users\Adrian Rojas\Desktop\dev\www2\\" favicon.ico /COPYALL /it /NFL /NDL /NJH /NJS /nc /ns /np
robocopy "%rootpath%\" "C:\Users\Adrian Rojas\Desktop\dev\www2\\" Global.asax /COPYALL /it /NFL /NDL /NJH /NJS /nc /ns /np
robocopy "%rootpath%\" "C:\Users\Adrian Rojas\Desktop\dev\www2\\" "packages.config" /COPYALL /it /NFL /NDL /NJH /NJS /nc /ns /np
robocopy "%rootpath%\" "C:\Users\Adrian Rojas\Desktop\dev\www2\\" "Web.config" /COPYALL /it /NFL /NDL /NJH /NJS /nc /ns /np




mkdir "%destination%\Static"
robocopy "%prevpath%\Front\dist\static" "C:\Users\Adrian Rojas\Desktop\dev\www2\Static" /E /COPYALL /is /NFL /NDL /NJH /NJS /nc /ns /np
robocopy "%prevpath%\Front\dist\\" "C:\Users\Adrian Rojas\Desktop\dev\www2\Views\Home\\" "index.html" /COPYALL /is /NFL /NDL /NJH /NJS /nc /ns /np


echo "@{    Layout = "";   }" > "C:\Users\Adrian Rojas\Desktop\dev\www2\Views\Home\Index.cshtml"
type "C:\Users\Adrian Rojas\Desktop\dev\www2\Views\Home\index.html" >> "C:\Users\Adrian Rojas\Desktop\dev\www2\Views\Home\Index.cshtml"

ECHO ON
exit /b

:strLen
setlocal enabledelayedexpansion
:strLen_Loop
  if not "!%1:~%len%!"=="" set /A len+=1 & goto :strLen_Loop
(endlocal & set %2=%len%)
goto :eof