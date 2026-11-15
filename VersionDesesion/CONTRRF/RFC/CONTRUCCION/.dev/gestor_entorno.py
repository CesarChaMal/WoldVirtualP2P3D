"""Gestor del Entorno de Desarrollo y Sistema General de Rutas (.dev)
WoldVirtualP2P3D · Versión canónica

Centraliza y resuelve:
- Python base (.dev/.python)
- Entorno virtual (.dev/venv)
- Configuración de VS Code (.dev/.vscode)
- Archivo de pruebas (.dev/pytest.ini)
- Caché de pruebas (.dev/.pytest_cache)
- Caché de linter (.dev/.ruff_cache)
- Repositorio IPFS (.dev/.ipfs)
- Jerarquía general de rutas:
    REPO_ROOT:       E:\\WoldVirtualP2P3D
    VERSION_DIR:     E:\\WoldVirtualP2P3D\\VersionDesesion
    CONTRRF_DIR:     E:\\WoldVirtualP2P3D\\VersionDesesion\\CONTRRF
    RFC_DIR:         E:\\WoldVirtualP2P3D\\VersionDesesion\\CONTRRF\\RFC
    CONTRUCCION_DIR: E:\\WoldVirtualP2P3D\\VersionDesesion\\CONTRRF\\RFC\\CONTRUCCION
    DEV_DIR:         E:\\WoldVirtualP2P3D\\VersionDesesion\\CONTRRF\\RFC\\CONTRUCCION\\.dev
    TLLDCD_SRC:      E:\\WoldVirtualP2P3D\\VersionDesesion\\CONTRRF\\RFC\\CONTRUCCION\\TLLDCD\\src
    STM_RFC_DIR:     E:\\WoldVirtualP2P3D\\VersionDesesion\\CONTRRF\\RFC\\CONTRUCCION\\STM_RFC\\STM_RFPRMN
"""
from __future__ import annotations

import json
import os
import shutil
import subprocess
import sys
from pathlib import Path
from typing import Dict, List, Optional

# --- Localización de directorios canónicos ---
DEV_DIR: Path = Path(__file__).resolve().parent
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

# --- Árbol de código ---
TLLDCD_DIR: Path = CONTRUCCION_DIR / "TLLDCD"
TLLDCD_SRC: Path = TLLDCD_DIR / "src"
STM_RFC_DIR: Path = CONTRUCCION_DIR / "STM_RFC" / "STM_RFPRMN"
REFACTOR_DIR: Path = CONTRUCCION_DIR / "Refactorizacion"
LC_TRABAJANDO_DIR: Path = CONTRUCCION_DIR / "LCtrabajando"

PRY_DIR: Path = VERSION_DIR / "SistemaPrincipal" / "PRY"
PRY_ARCHIVOS_DIR: Path = PRY_DIR / "Archivo" / "VersionesDeRefuerzo"


def obtener_python_activo() -> Path:
    """Devuelve el ejecutable de Python prioritario (.python > venv > sys.executable)."""
    if PYTHON_EXE.is_file():
        return PYTHON_EXE
    if VENV_PYTHON.is_file():
        return VENV_PYTHON
    return Path(sys.executable)


def configurar_variables_entorno() -> Dict[str, str]:
    """Configura las variables de entorno para que todo el sistema use las rutas de .dev."""
    DEV_DIR.mkdir(parents=True, exist_ok=True)
    PYTEST_CACHE_DIR.mkdir(parents=True, exist_ok=True)
    RUFF_CACHE_DIR.mkdir(parents=True, exist_ok=True)

    nuevos_valores = {
        "VIRTUAL_ENV": str(VENV_DIR),
        "PYTHONHOME": str(PYTHON_DIR),
        "WVP_PYTHON": str(PYTHON_EXE if PYTHON_EXE.is_file() else obtener_python_activo()),
        "PYTEST_CACHE_DIR": str(PYTEST_CACHE_DIR),
        "RUFF_CACHE_DIR": str(RUFF_CACHE_DIR),
        "RFPRMN_RFC_DIR": str(RFC_DIR),
        "RFPRMN_PRY_DIR": str(PRY_DIR),
    }

    for clave, valor in nuevos_valores.items():
        os.environ[clave] = valor

    # Añadir .python y Scripts del venv al PATH
    py_dir = str(PYTHON_DIR)
    scripts_dir = str(VENV_DIR / "Scripts")
    path_actual = os.environ.get("PATH", "")
    elementos_path = [py_dir, scripts_dir] + [p for p in path_actual.split(os.pathsep) if p not in (py_dir, scripts_dir)]
    os.environ["PATH"] = os.pathsep.join(elementos_path)

    # Inyectar rutas en sys.path
    for p in (str(TLLDCD_SRC), str(STM_RFC_DIR), str(DEV_DIR)):
        if p not in sys.path:
            sys.path.insert(0, p)

    return nuevos_valores


def traer_pytest_ini() -> bool:
    """Trae pytest.ini desde la raíz del repositorio hacia .dev/pytest.ini.

    Centraliza la configuración de pruebas dentro de .dev y elimina el
    archivo suelto en la raíz para mantener limpio el repositorio.
    """
    ini_raiz = REPO_ROOT / "pytest.ini"
    ini_dev = DEV_DIR / "pytest.ini"

    contenido_canonico = (
        "[pytest]\n"
        "cache_dir = .pytest_cache\n"
        "testpaths = ../TLLDCD/src/tests ../STM_RFC/STM_RFPRMN/tests\n"
        "pythonpath = ../TLLDCD/src ../STM_RFC/STM_RFPRMN ../../../../..\n"
        "python_files = test_*.py\n"
        "python_classes = Test*\n"
        "python_functions = test_*\n"
    )

    movido = False
    if ini_raiz.is_file():
        try:
            ini_dev.write_text(contenido_canonico, encoding="utf-8")
            ini_raiz.unlink()
            movido = True
            print(f"[OK] pytest.ini traído a: {ini_dev}")
            print(f"[OK] Archivo suelto eliminado de la raíz: {ini_raiz}")
        except Exception as err:
            print(f"[AVISO] Error al mover pytest.ini de raíz a .dev: {err}")
    elif not ini_dev.is_file():
        ini_dev.write_text(contenido_canonico, encoding="utf-8")
        movido = True
        print(f"[OK] pytest.ini inicializado en: {ini_dev}")

    return movido


def actualizar_vscode_config() -> Path:
    """Actualiza .dev/.vscode/settings.json y launch.json con la versión de Python de .dev/.python."""
    VSCODE_DIR.mkdir(parents=True, exist_ok=True)
    settings_file = VSCODE_DIR / "settings.json"
    launch_file = VSCODE_DIR / "launch.json"

    py_exe_str = str(PYTHON_EXE).replace("\\", "/")
    dev_dir_str = str(DEV_DIR).replace("\\", "/")
    pytest_ini_str = str(DEV_PYTEST_INI).replace("\\", "/")
    pytest_cache_str = str(PYTEST_CACHE_DIR).replace("\\", "/")

    config = {
        "python.defaultInterpreterPath": py_exe_str,
        "python.pythonPath": py_exe_str,
        "python-envs.defaultEnvManager": "ms-python.python:system",
        "python.analysis.extraPaths": [
            str(TLLDCD_SRC).replace("\\", "/"),
            str(STM_RFC_DIR).replace("\\", "/"),
            dev_dir_str,
            str(REPO_ROOT).replace("\\", "/"),
        ],
        "python.autoComplete.extraPaths": [
            str(TLLDCD_SRC).replace("\\", "/"),
            str(STM_RFC_DIR).replace("\\", "/"),
            dev_dir_str,
            str(REPO_ROOT).replace("\\", "/"),
        ],
        "python.testing.pytestEnabled": True,
        "python.testing.unittestEnabled": False,
        "python.testing.pytestArgs": [
            "-c", pytest_ini_str,
            "-o", f"cache_dir={pytest_cache_str}",
        ],
        "terminal.integrated.env.windows": {
            "PYTHONHOME": str(PYTHON_DIR),
            "WVP_PYTHON": str(PYTHON_EXE),
            "PYTEST_CACHE_DIR": str(PYTEST_CACHE_DIR),
            "RUFF_CACHE_DIR": str(RUFF_CACHE_DIR),
            "RFPRMN_RFC_DIR": str(RFC_DIR),
            "RFPRMN_PRY_DIR": str(PRY_DIR),
        }
    }

    settings_file.write_text(json.dumps(config, indent=4, ensure_ascii=False), encoding="utf-8")

    launch_config = {
        "version": "0.2.0",
        "configurations": [
            {
                "name": "RFPRMN (Fachada .dev)",
                "type": "debugpy",
                "request": "launch",
                "program": str(STM_RFC_DIR / "RFPRMN.py").replace("\\", "/"),
                "python": py_exe_str,
                "args": ["--arranque"],
                "console": "integratedTerminal",
                "cwd": str(STM_RFC_DIR).replace("\\", "/")
            },
            {
                "name": "Gestor Entorno (.dev)",
                "type": "debugpy",
                "request": "launch",
                "program": str(DEV_DIR / "gestor_entorno.py").replace("\\", "/"),
                "python": py_exe_str,
                "args": ["sincronizar"],
                "console": "integratedTerminal",
                "cwd": dev_dir_str
            },
            {
                "name": "Pytest (.dev)",
                "type": "debugpy",
                "request": "launch",
                "module": "pytest",
                "python": py_exe_str,
                "args": ["-c", pytest_ini_str, "-o", f"cache_dir={pytest_cache_str}"],
                "console": "integratedTerminal",
                "cwd": dev_dir_str
            }
        ]
    }
    launch_file.write_text(json.dumps(launch_config, indent=4, ensure_ascii=False), encoding="utf-8")

    print(f"[OK] Configuración VS Code actualizada en: {settings_file}")
    print(f"[OK] Intérprete Python configurado: {py_exe_str}")
    return settings_file


def limpiar_cache_raiz() -> bool:
    """Si se generó algún .pytest_cache o .ruff_cache en la raíz, lo limpia."""
    borrado = False
    for nombre in (".pytest_cache", ".ruff_cache"):
        cache_raiz = REPO_ROOT / nombre
        if cache_raiz.is_dir():
            try:
                shutil.rmtree(cache_raiz, ignore_errors=True)
                borrado = True
            except Exception:
                pass
    return borrado


def ejecutar_pytest(args: Optional[List[str]] = None) -> int:
    """Ejecuta pytest forzando que se use .dev/pytest.ini y cache_dir=.dev/.pytest_cache."""
    configurar_variables_entorno()
    traer_pytest_ini()
    limpiar_cache_raiz()

    py = obtener_python_activo()
    args_completos = [
        str(py),
        "-m",
        "pytest",
        "-c",
        str(DEV_PYTEST_INI),
        "-o",
        f"cache_dir={PYTEST_CACHE_DIR}",
    ]
    if args:
        args_completos.extend(args)
    else:
        # Por defecto correr suite de pruebas del taller
        args_completos.append(str(TLLDCD_SRC / "tests"))

    resultado = subprocess.run(args_completos, cwd=str(REPO_ROOT))
    limpiar_cache_raiz()
    traer_pytest_ini()
    return resultado.returncode


def mostrar_informe() -> None:
    """Muestra el estado de todas las rutas del nuevo sistema general."""
    print("=" * 70)
    print("SISTEMA GENERAL DE RUTAS WOLDVIRTUALP2P3D (.dev)")
    print("=" * 70)
    rutas_info = [
        ("Raíz Repositorio", REPO_ROOT, REPO_ROOT.is_dir()),
        ("Versión de Sesión", VERSION_DIR, VERSION_DIR.is_dir()),
        ("CONTRRF", CONTRRF_DIR, CONTRRF_DIR.is_dir()),
        ("RFC", RFC_DIR, RFC_DIR.is_dir()),
        ("CONTRUCCION", CONTRUCCION_DIR, CONTRUCCION_DIR.is_dir()),
        ("Carpeta .dev", DEV_DIR, DEV_DIR.is_dir()),
        ("Python base (.python)", PYTHON_DIR, PYTHON_DIR.is_dir()),
        ("Python ejecutable", PYTHON_EXE, PYTHON_EXE.is_file()),
        ("Entorno venv", VENV_DIR, VENV_DIR.is_dir()),
        ("Python venv ejecutable", VENV_PYTHON, VENV_PYTHON.is_file()),
        ("Config VS Code (.vscode)", VSCODE_DIR, (VSCODE_DIR / "settings.json").is_file()),
        ("Config Pytest (.dev/pytest.ini)", DEV_PYTEST_INI, DEV_PYTEST_INI.is_file()),
        ("Caché Pytest (.pytest_cache)", PYTEST_CACHE_DIR, PYTEST_CACHE_DIR.is_dir()),
        ("Caché Ruff (.ruff_cache)", RUFF_CACHE_DIR, RUFF_CACHE_DIR.is_dir()),
        ("Taller TLLDCD src", TLLDCD_SRC, TLLDCD_SRC.is_dir()),
        ("STM_RFC STM_RFPRMN", STM_RFC_DIR, STM_RFC_DIR.is_dir()),
        ("Sistema Principal PRY", PRY_DIR, PRY_DIR.is_dir()),
    ]
    for nombre, ruta, existe in rutas_info:
        estado = "[OK]" if existe else "[FALTA]"
        print(f"{estado:<8} {nombre:<32} -> {ruta}")
    print("=" * 70)


def main() -> int:
    configurar_variables_entorno()
    traer_pytest_ini()
    actualizar_vscode_config()

    if len(sys.argv) > 1 and sys.argv[1] == "pytest":
        return ejecutar_pytest(sys.argv[2:])
    elif len(sys.argv) > 1 and sys.argv[1] in ("traer-pytest", "traer_pytest"):
        traer_pytest_ini()
        return 0
    elif len(sys.argv) > 1 and sys.argv[1] == "vscode":
        actualizar_vscode_config()
        return 0
    elif len(sys.argv) > 1 and sys.argv[1] == "limpiar":
        borrado = limpiar_cache_raiz()
        traer_pytest_ini()
        print(f"Limpieza en raíz: {'Borrados rastros' if borrado else 'Sin archivos sueltos'}")
        return 0
    elif len(sys.argv) > 1 and sys.argv[1] == "sincronizar":
        configurar_variables_entorno()
        traer_pytest_ini()
        actualizar_vscode_config()
        limpiar_cache_raiz()
        print("Entorno sincronizado y variables aplicadas.")
        mostrar_informe()
        return 0
    else:
        mostrar_informe()
        return 0


if __name__ == "__main__":
    raise SystemExit(main())
