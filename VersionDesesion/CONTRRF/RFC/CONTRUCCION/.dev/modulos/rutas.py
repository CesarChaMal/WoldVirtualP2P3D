"""Módulo de Rutas Canónicas para WoldVirtualP2P3D (.dev)
Centraliza la resolución de rutas de todo el ecosistema:
- Raíz del repositorio, versión, CONTRRF, RFC, CONTRUCCION, .dev
- Intérpretes de Python (.dev/.python, .dev/venv)
- Módulos del taller (TLLDCD/src)
- Servidor Oficial Blockchain (STM_BKCH/stm_bkch1)
- Servidor de LucIA 3D (MTBGT/.../puente_3D)
- Visor 3D WPF C# (TLLDCD/src/VSC#/Visor3Ddesarrollo/HerraMientas)
"""
from __future__ import annotations

from pathlib import Path

# --- Directorios Principales de la Jerarquía ---
MODULOS_DIR: Path = Path(__file__).resolve().parent
DEV_DIR: Path = MODULOS_DIR.parent
CONTRUCCION_DIR: Path = DEV_DIR.parent
RFC_DIR: Path = CONTRUCCION_DIR.parent
CONTRRF_DIR: Path = RFC_DIR.parent
VERSION_DIR: Path = CONTRRF_DIR.parent
REPO_ROOT: Path = VERSION_DIR.parent

# --- Subdirectorios de .dev ---
PYTHON_DIR: Path = DEV_DIR / ".python"
PYTHON_EXE: Path = PYTHON_DIR / "python.exe"
PYTHONW_EXE: Path = PYTHON_DIR / "pythonw.exe"

VENV_DIR: Path = DEV_DIR / "venv"
VENV_PYTHON: Path = VENV_DIR / "Scripts" / "python.exe"
VENV_PYTHONW: Path = VENV_DIR / "Scripts" / "pythonw.exe"

PYTEST_CACHE_DIR: Path = DEV_DIR / ".pytest_cache"
RUFF_CACHE_DIR: Path = DEV_DIR / ".ruff_cache"
IPFS_DIR: Path = DEV_DIR / ".ipfs"
VSCODE_DIR: Path = DEV_DIR / ".vscode"
DEV_PYTEST_INI: Path = DEV_DIR / "pytest.ini"

# --- Taller y Código Fuente (TLLDCD) ---
TLLDCD_DIR: Path = CONTRUCCION_DIR / "TLLDCD"
TLLDCD_SRC: Path = TLLDCD_DIR / "src"
STM_RFC_DIR: Path = CONTRUCCION_DIR / "STM_RFC" / "STM_RFPRMN"
REFACTOR_DIR: Path = CONTRUCCION_DIR / "Refactorizacion"
LC_TRABAJANDO_DIR: Path = CONTRUCCION_DIR / "LCtrabajando"

# --- Sistema Principal y Archivo Legado ---
PRY_DIR: Path = VERSION_DIR / "SistemaPrincipal" / "PRY"
PRY_ARCHIVOS_DIR: Path = PRY_DIR / "Archivo" / "VersionesDeRefuerzo"

# --- Servidor Blockchain Oficial (STM_BKCH) ---
BLOCKCHAIN_DIR: Path = TLLDCD_SRC / "STM_BKCH" / "stm_bkch1"
BLOCKCHAIN_SCRIPT: Path = BLOCKCHAIN_DIR / "servidor_oficial.py"
LEDGER_PATH: Path = TLLDCD_SRC / "STM_CH" / "blockchain_ledger.json"
PSNRL_DIR: Path = TLLDCD_SRC / "STM_CH" / "PSNRL"

# --- Servidor LucIA 3D ---
LUCIA_DIR: Path = TLLDCD_SRC / "MTBGT" / "WoldVirtualP2P3D" / "lucia3D" / "GLTF_lucIA" / "celebro_3D" / "puente_3D"
LUCIA_SCRIPT: Path = LUCIA_DIR / "servidor_lucia.py"

# --- Proyecto Visor 3D (C# WPF) ---
VISOR_BASE_DIR: Path = TLLDCD_SRC / "VSC#" / "Visor3Ddesarrollo"
VISOR_HERRAMIENTAS_DIR: Path = VISOR_BASE_DIR / "HerraMientas"
VISOR_CSPROJ: Path = VISOR_HERRAMIENTAS_DIR / "Visor3D.csproj"
VISOR_OUTPUT_DLL: Path = VISOR_HERRAMIENTAS_DIR / "bin" / "Debug" / "net8.0-windows" / "win-x64" / "LCDMWoldVirtualP2P3Dv2.8.4.dll"
VISOR_OUTPUT_EXE: Path = VISOR_HERRAMIENTAS_DIR / "bin" / "Debug" / "net8.0-windows" / "win-x64" / "LCDMWoldVirtualP2P3Dv2.8.4.exe"

# Módulos de Seguridad y Sesión Única del Visor
SEGURIDAD_DIR: Path = VISOR_BASE_DIR / "Seguridad"
SEGURIDAD_SESION_UNICA_DIR: Path = SEGURIDAD_DIR / "SeguridadSesionUnica"
SMTPLC_DIR: Path = SEGURIDAD_DIR / "SMTPLC"

# Archivos de SeguridadSesionUnica
ARRANQUE_CUENTA_CS: Path = SEGURIDAD_SESION_UNICA_DIR / "ArranqueCuenta.cs"
MAQUINA_HUELLA_CS: Path = SEGURIDAD_SESION_UNICA_DIR / "MaquinaHuellaDigital.cs"
REGISTRO_PC_UNICO_CS: Path = SEGURIDAD_SESION_UNICA_DIR / "RegistroPcUnico.cs"
REINICIO_CUENTA_CS: Path = SEGURIDAD_SESION_UNICA_DIR / "ReinicioCuenta.cs"
SESION_UNICA_GUARDA_CS: Path = SEGURIDAD_SESION_UNICA_DIR / "SesionUnicaGuarda.cs"

# Scripts del Visor
LUCIA_BAT_VISOR: Path = VISOR_HERRAMIENTAS_DIR / "Iniciar_Lucia_Visor.bat"
LUCIA_PS1_VISOR: Path = VISOR_HERRAMIENTAS_DIR / "Iniciar_Lucia_Visor.ps1"
LANZAR_BAT: Path = VISOR_HERRAMIENTAS_DIR / "Lanzar.bat"

# Motor y Proyecto Godot
GODOT_PROY_DIR: Path = TLLDCD_SRC / "MTBGT" / "WoldVirtualP2P3D"
GODOT_PROJECT_FILE: Path = GODOT_PROY_DIR / "project.godot"
