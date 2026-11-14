@echo off
setlocal
title Wold Virtual 3D - PCK + servidores

for %%I in ("%~dp0..\..\..") do set "SRC=%%~fI"

set "PY=%SRC%\LC_STM\python\python.exe"
if not exist "%PY%" if defined WVP_PYTHON set "PY=%WVP_PYTHON%"
if not exist "%PY%" for /f "delims=" %%P in ('where python 2^>nul') do set "PY=%%P"

set "PYW=%SRC%\LC_STM\python\pythonw.exe"
if not exist "%PYW%" set "PYW=%PY:python.exe=pythonw.exe%"
if not exist "%PYW%" set "PYW=%PY%"

set "SRV=%SRC%\STM_BKCH\stm_bkch1\servidor_oficial.py"
set "LUCIA=%SRC%\MTBGT\WoldVirtualP2P3D\lucia3D\GLTF_lucIA\celebro_3D\puente_3D\servidor_lucia.py"
set "PYTHONPATH=%SRC%;%PYTHONPATH%"
set "WVP_SRC_DIR=%SRC%"

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
if not exist "%PY%" (
  echo No se encuentra el Python del servidor: %PY%
  echo Define WVP_PYTHON o coloca el interprete en %SRC%\LC_STM\python\python.exe
  pause
  exit /b 1
)
if not exist "%SRV%" (
  echo No se encuentra el servidor oficial: %SRV%
  pause
  exit /b 1
)
if not exist "%LUCIA%" (
  echo No se encuentra el servidor de LucIA: %LUCIA%
  pause
  exit /b 1
)
if not exist "%PCK%" (
  echo No se encuentra el PCK: %PCK%
  pause
  exit /b 1
)

cd /d "%SRC%"

echo Arrancando servidor oficial en el puerto 8545...
start "WoldVirtual - Servidor 8545" /min "%PY%" "%SRV%" --puerto 8545 --sin-cuota-ubicacion
timeout /t 2 /nobreak >nul

echo Arrancando cerebro de LucIA en el puerto 8765...
start "WoldVirtual - LucIA 8765" /min "%PYW%" "%LUCIA%" --modelo qwen2.5:7b --puerto 8765
timeout /t 3 /nobreak >nul

echo Lanzando el mundo exportado (wvp3d.pck)...
start "" "%GODOT%" --main-pack "%PCK%" %*
exit /b 0
