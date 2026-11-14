@echo off
setlocal
title Wold Virtual 3D - Reiniciar LucIA (8765)

for %%I in ("%~dp0..\..\..") do set "SRC=%%~fI"
set "PYW=%SRC%\LC_STM\python\pythonw.exe"
if not exist "%PYW%" if defined WVP_PYTHONW set "PYW=%WVP_PYTHONW%"
if not exist "%PYW%" if defined WVP_PYTHON set "PYW=%WVP_PYTHON:python.exe=pythonw.exe%"
if not exist "%PYW%" for /f "delims=" %%P in ('where pythonw 2^>nul') do set "PYW=%%P"
if not exist "%PYW%" for /f "delims=" %%P in ('where python 2^>nul') do set "PYW=%%P"
set "SRV=%SRC%\MTBGT\WoldVirtualP2P3D\lucia3D\GLTF_lucIA\celebro_3D\puente_3D\servidor_lucia.py"
set "PYTHONPATH=%SRC%;%PYTHONPATH%"
set "WVP_SRC_DIR=%SRC%"

echo Cerrando servidores de LucIA anteriores...
powershell -NoProfile -Command "Get-CimInstance Win32_Process | Where-Object { $_.CommandLine -like '*servidor_lucia.py*' } | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }"

echo Arrancando LucIA en 127.0.0.1:8765...
start "LucIA 8765" /min "%PYW%" "%SRV%" --modelo qwen2.5:7b --puerto 8765
timeout /t 4 /nobreak >nul

netstat -ano | findstr :8765
if errorlevel 1 (
  echo.
  echo NO se pudo abrir el 8765. Lanzando en modo visible para ver el error...
  "%PYW:\pythonw.exe=\python.exe%" "%SRV%" --modelo qwen2.5:7b --puerto 8765
) else (
  echo.
  echo LucIA escuchando en 8765.
)
pause
