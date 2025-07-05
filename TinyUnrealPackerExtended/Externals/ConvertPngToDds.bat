@echo off

set "input=%~1"
set "outFolder=%~2"
set "fmt=%3"

REM Запуск texconv.exe (должен лежать рядом с приложением)
texconv.exe -f %fmt% -o "%outFolder%" -y "%input%"

exit /b %ERRORLEVEL%