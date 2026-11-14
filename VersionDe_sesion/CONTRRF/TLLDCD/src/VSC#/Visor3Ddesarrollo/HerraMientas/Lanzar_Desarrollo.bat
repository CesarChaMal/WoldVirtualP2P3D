@echo off
setlocal
title Wold Virtual 3D - Desarrollo

for %%I in ("%~dp0..\..\..") do set "SRC=%%~fI"
set "PROY=%SRC%\MTBGT\WoldVirtualP2P3D"

set "GODOT="
for /f "delims=" %%D in ('dir /b /ad /o-n "%LOCALAPPDATA%\WoldVirtualP2P3D\runtime" 2^>nul') do (
  if not defined GODOT if exist "%LOCALAPPDATA%\WoldVirtualP2P3D\runtime\%%D\Servidor3D\Godot_v4.7.2-stable_win64.exe" set "GODOT=%LOCALAPPDATA%\WoldVirtualP2P3D\runtime\%%D\Servidor3D\Godot_v4.7.2-stable_win64.exe"
)

if not defined GODOT (
  echo No se encuentra el motor Godot en %LOCALAPPDATA%\WoldVirtualP2P3D\runtime
  echo Extrae primero el runtime con el visor o ajusta la ruta.
  pause
  exit /b 1
)
if not exist "%PROY%\project.godot" (
  echo No se encuentra el proyecto Godot en:
  echo   %PROY%
  pause
  exit /b 1
)

echo Lanzando Wold Virtual 3D (proyecto de desarrollo)...
start "" "%GODOT%" --path "%PROY%" %*
exit /b 0
