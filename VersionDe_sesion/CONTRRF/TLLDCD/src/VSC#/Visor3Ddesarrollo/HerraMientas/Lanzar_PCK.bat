@echo off
setlocal
title Wold Virtual 3D - PCK exportado

for %%I in ("%~dp0..\..\..") do set "SRC=%%~fI"
set "PCK=%~dp0wvp3d.pck"
if not exist "%PCK%" set "PCK=%SRC%\VSC#\wvp3d.pck"

set "GODOT="
for /f "delims=" %%D in ('dir /b /ad /o-n "%LOCALAPPDATA%\WoldVirtualP2P3D\runtime" 2^>nul') do (
  if not defined GODOT if exist "%LOCALAPPDATA%\WoldVirtualP2P3D\runtime\%%D\Servidor3D\Godot_v4.7.2-stable_win64.exe" set "GODOT=%LOCALAPPDATA%\WoldVirtualP2P3D\runtime\%%D\Servidor3D\Godot_v4.7.2-stable_win64.exe"
)

if not defined GODOT (
  echo No se encuentra el motor Godot en %LOCALAPPDATA%\WoldVirtualP2P3D\runtime
  pause
  exit /b 1
)
if not exist "%PCK%" (
  echo No se encuentra el PCK: %PCK%
  pause
  exit /b 1
)

echo Lanzando el mundo exportado (wvp3d.pck)...
start "" "%GODOT%" --main-pack "%PCK%" %*
exit /b 0
