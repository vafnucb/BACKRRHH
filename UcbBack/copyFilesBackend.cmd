@ECHO OFF

REM Establecer la ruta ra�z en el directorio actual del script
set rootpath=%~dp0

REM Retroceder un nivel para llegar al directorio principal del repositorio
set repo_path=%rootpath%\..

REM Establecer las rutas de destino para producci�n y desarrollo
set destination="C:\inetpub\wwwroot\RRHH2"

REM Habilitar aplicaciones de 32 bits en la aplicaci�n del grupo de aplicaciones (app pool)
%systemroot%\system32\inetsrv\appcmd set apppool /apppool.name:ADMNALRRHH2 /enable32BitAppOnWin64:true

REM Actualizar el repositorio local con los �ltimos cambios
pushd "%repo_path%"
git pull
popd

REM Crear directorios necesarios y copiar archivos
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

REM ECHO ON
EXIT /B 0