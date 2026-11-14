@echo off
setlocal
REM HerraMientas -> Visor3Ddesarrollo -> VSC# -> src
for %%I in ("%~dp0..\..\..") do set "SRC=%%~fI"

set "PY=%SRC%\LC_STM\python\pythonw.exe"
if not exist "%PY%" if defined WVP_PYTHONW set "PY=%WVP_PYTHONW%"
if not exist "%PY%" if defined WVP_PYTHON set "PY=%WVP_PYTHON:python.exe=pythonw.exe%"
if not exist "%PY%" for /f "delims=" %%P in ('where pythonw 2^>nul') do set "PY=%%P"
if not exist "%PY%" for /f "delims=" %%P in ('where python 2^>nul') do set "PY=%%P"

set "SRV=%SRC%\MTBGT\WoldVirtualP2P3D\lucia3D\GLTF_lucIA\celebro_3D\puente_3D\servidor_lucia.py"
set "PYTHONPATH=%SRC%;%PYTHONPATH%"
set "WVP_SRC_DIR=%SRC%"
set "LOG=%LOCALAPPDATA%\WoldVirtualP2P3D\logs\lucia_bat.log"
if not exist "%LOCALAPPDATA%\WoldVirtualP2P3D\logs" mkdir "%LOCALAPPDATA%\WoldVirtualP2P3D\logs" >nul 2>&1

cd /d "%~dp0"
echo ==== %date% %time% ==== >> "%LOG%"
echo PY=%PY%>> "%LOG%"
echo SRV=%SRV%>> "%LOG%"
if not exist "%PY%" echo ERROR: no se encontro Python>> "%LOG%" & exit /b 1
if not exist "%SRV%" echo ERROR: no se encontro servidor_lucia.py>> "%LOG%" & exit /b 1

"%PY%" "%SRV%" --modelo qwen2.5:7b --puerto 8765 >> "%LOG%" 2>&1
