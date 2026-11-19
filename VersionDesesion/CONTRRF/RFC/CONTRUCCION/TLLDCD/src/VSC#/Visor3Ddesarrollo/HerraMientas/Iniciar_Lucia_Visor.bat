@echo off
setlocal
REM Arbol activo (..\..\.. desde HerraMientas = TLLDCD\src) y archivo de
REM versiones anteriores, donde vive el Python embebido que no se versiona.
set "SRC=%~dp0..\..\.."
if not defined WVP_LEGACY_SRC set "WVP_LEGACY_SRC=%~dp0..\..\..\..\..\..\..\..\..\VersionDesesion\SistemaPrincipal\PRY\Archivo\VersionesDeRefuerzo\actualizacion_c5074e3b\src"
if not exist "%WVP_LEGACY_SRC%" set "WVP_LEGACY_SRC=%~dp0..\..\..\..\..\..\..\..\..\VersionDesesion\SistemaPrincipal\PRY\Archivo\VersionesDeRefuerzo\actualizacion_c5074e3b\src"
set "DEV_DIR=%~dp0..\..\..\..\..\.dev"
set "PYTHONPYCACHEPREFIX=%DEV_DIR%\pycache"
set "PY="
if exist "%DEV_DIR%\.python\pythonw.exe" set "PY=%DEV_DIR%\.python\pythonw.exe"
if not defined PY if exist "%DEV_DIR%\venv\Scripts\pythonw.exe" set "PY=%DEV_DIR%\venv\Scripts\pythonw.exe"
if not defined PY set "PY=%SRC%\LC_STM\python\pythonw.exe"
if not exist "%PY%" set "PY=%WVP_LEGACY_SRC%\LC_STM\python\pythonw.exe"
set "SRV=%SRC%\MTBGT\WoldVirtualP2P3D\lucia3D\GLTF_lucIA\celebro_3D\puente_3D\servidor_lucia.py"
if not exist "%SRV%" set "SRV=%WVP_LEGACY_SRC%\MTBGT\WoldVirtualP2P3D\lucia3D\GLTF_lucIA\celebro_3D\puente_3D\servidor_lucia.py"
set "LOG=%LOCALAPPDATA%\WoldVirtualP2P3D\logs\lucia_bat.log"
cd /d "%~dp0"
"%PY%" "%SRV%" --modelo qwen2.5:7b --puerto 8765 >> "%LOG%" 2>&1
