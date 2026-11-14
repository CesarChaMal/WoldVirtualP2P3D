@echo off
setlocal
title Wold Virtual 3D - Vista previa del visor

for %%I in ("%~dp0..\..\..") do set "SRC=%%~fI"
set "PROY=%SRC%\MTBGT\WoldVirtualP2P3D"

echo Cerrando servidores de LucIA anteriores...
powershell -NoProfile -Command "Get-CimInstance Win32_Process | Where-Object { $_.CommandLine -like '*servidor_lucia.py*' } | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }" >nul 2>&1
timeout /t 1 /nobreak >nul

echo Arrancando LucIA en 127.0.0.1:8765...
start "LucIA 8765" /min "%~dp0Iniciar_Lucia_Visor.bat"

for /L %%i in (1,1,40) do (
  netstat -ano | findstr ":8765" | findstr "LISTENING" >nul
  if not errorlevel 1 goto listo
  timeout /t 1 /nobreak >nul
)

echo.
echo ERROR: LucIA no abrio el puerto 8765. No se abre el visor.
echo Revisa %%LOCALAPPDATA%%\WoldVirtualP2P3D\logs y ejecuta Reiniciar_Lucia.bat.
pause
exit /b 1

:listo
echo LucIA escuchando.

set "EXE="
for /f "delims=" %%F in ('dir /b /s "%~dp0bin\*WoldVirtualP2P3Dv*.exe" 2^>nul') do (
  if not defined EXE set "EXE=%%F"
)

if defined EXE (
  echo Abriendo vista previa: %EXE%
  start "" "%EXE%"
  exit /b 0
)

if not exist "%PROY%\project.godot" (
  echo No hay binario compilado ni proyecto Godot en %PROY%
  pause
  exit /b 1
)

echo Sin binario compilado: abriendo el mundo de desarrollo con Godot...
start "WoldVirtual - Desarrollo" /min "%~dp0Lanzar_Desarrollo.bat"
exit /b 0
