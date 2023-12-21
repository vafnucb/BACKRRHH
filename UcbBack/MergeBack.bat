@echo off

REM Archivos específicos a excluir durante el merge (con rutas relativas)
set EXCLUDED_FILE_1=Web.config
set EXCLUDED_FILE_2=App_Start/WebApiConfig.cs
set EXCLUDED_FILE_3=Controllers/OptionsController.cs

REM Realizar un stash de los cambios locales en la rama de destino (main)
git stash

REM Realizar el merge
git merge devBack

REM Deshacer los cambios específicos en la rama de destino (main) si hay conflictos
git checkout --ours -- "%EXCLUDED_FILE_1%"
git checkout --ours -- "%EXCLUDED_FILE_2%"
git checkout --ours -- "%EXCLUDED_FILE_3%"

REM Commit de los cambios en la rama de destino (main)
git add .
git commit -m "Merge de devBack en main, excluyendo cambios en %EXCLUDED_FILE_1%, %EXCLUDED_FILE_2%, %EXCLUDED_FILE_3%"

REM Aplicar los cambios locales nuevamente (sin aplicar los cambios de los archivos específicos)
git stash apply --index
