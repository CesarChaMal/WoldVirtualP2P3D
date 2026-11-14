@echo off
setlocal
title Wold Virtual 3D - Servidor LucIA (8765)

for %%I in ("%~dp0..\..\..") do set "SRC=%%~fI"
set "PY=%SRC%\LC_STM\python\python.exe"
if not exist "%PY%" if defined WVP_PYTHON set "PY=%WVP_PYTHON%"
if not exist "%PY%" for /f "delims=" %%P in ('where python 2^>nul') do set "PY=%%P"
set "SRV=%SRC%\MTBGT\WoldVirtualP2P3D\lucia3D\GLTF_lucIA\celebro_3D\puente_3D\servidor_lucia.py"
set "PYTHONPATH=%SRC%;%PYTHONPATH%"
set "WVP_SRC_DIR=%SRC%"

if not exist "%PY%" (
  echo No se encuentra el Python de LucIA: %PY%
  echo Define WVP_PYTHON o coloca el interprete en %SRC%\LC_STM\python\python.exe
  pause
  exit /b 1
)
if not exist "%SRV%" (
  echo No se encuentra el servidor de LucIA: %SRV%
  pause
  exit /b 1
)

echo Arrancando el cerebro de LucIA en 127.0.0.1:8765...
"%PY%" "%SRV%" --modelo qwen2.5:7b --puerto 8765
exit /b %errorlevel%
