"""Gestor del Entorno de Desarrollo y Sistema General de Rutas (.dev)
WoldVirtualP2P3D · Versión canónica modular

Centraliza y orquesta:
- Python base (.dev/.python) y venv (.dev/venv)
- Configuración de VS Code (.dev/.vscode) y Pytest (.dev/pytest.ini)
- Servidor Oficial Blockchain y Cuentas (STM_BKCH puerto 8545)
- Servidor Cerebro de LucIA 3D (puerto 8765)
- Reconstrucción y ciclo de vida del Visor 3D C# WPF (Visor3D.csproj)
- Jerarquía general de rutas canónicas
"""
from __future__ import annotations

import argparse
import sys
from pathlib import Path
from typing import List, Optional

# Añadir el propio directorio .dev a sys.path para resolución de módulos
DEV_DIR_ROOT = Path(__file__).resolve().parent
if str(DEV_DIR_ROOT) not in sys.path:
    sys.path.insert(0, str(DEV_DIR_ROOT))

# Importar desde el paquete modular de .dev
from modulos import (  # noqa: E402
    BLOCKCHAIN_DIR,
    BLOCKCHAIN_SCRIPT,
    CONTRRF_DIR,
    CONTRUCCION_DIR,
    DEV_DIR,
    DEV_PYTEST_INI,
    GODOT_PROJECT_FILE,
    GODOT_PROY_DIR,
    IPFS_DIR,
    LEDGER_PATH,
    LUCIA_BAT_VISOR,
    LUCIA_DIR,
    LUCIA_PS1_VISOR,
    LUCIA_SCRIPT,
    PRY_ARCHIVOS_DIR,
    PRY_DIR,
    PSNRL_DIR,
    PYTEST_CACHE_DIR,
    PYTHON_DIR,
    PYTHON_EXE,
    PYTHONW_EXE,
    REPO_ROOT,
    RFC_DIR,
    RUFF_CACHE_DIR,
    STM_RFC_DIR,
    TLLDCD_DIR,
    TLLDCD_SRC,
    VENV_DIR,
    VENV_PYTHON,
    VENV_PYTHONW,
    VERSION_DIR,
    VISOR_BASE_DIR,
    VISOR_CSPROJ,
    VISOR_HERRAMIENTAS_DIR,
    VISOR_OUTPUT_DLL,
    VISOR_OUTPUT_EXE,
    SEGURIDAD_DIR,
    SEGURIDAD_SESION_UNICA_DIR,
    SMTPLC_DIR,
    VSCODE_DIR,
    actualizar_vscode_config,
    comprobar_dotnet,
    configurar_variables_entorno,
    detener_blockchain,
    detener_lucia,
    ejecutar_pytest,
    estado_blockchain,
    estado_lucia,
    estado_visor,
    iniciar_blockchain,
    iniciar_lucia,
    lanzar_visor,
    limpiar_cache_raiz,
    obtener_python_activo,
    obtener_pythonw_activo,
    reconstruir_visor,
    reiniciar_blockchain,
    reiniciar_lucia,
    traer_pytest_ini,
    validar_ledger_en_disco,
)


def mostrar_informe() -> None:
    """Muestra el estado completo y unificado de rutas, servicios y visor."""
    print("=" * 76)
    print("  SISTEMA GENERAL DE RUTAS Y SERVICIOS WOLDVIRTUALP2P3D (.dev)")
    print("=" * 76)

    # 1. Rutas del Entorno
    print("\n--- [1] JERARQUÍA DE DIRECTORIOS Y CONFIGURACIÓN (.dev) ---")
    rutas_info = [
        ("Raíz Repositorio", REPO_ROOT, REPO_ROOT.is_dir()),
        ("Versión de Sesión", VERSION_DIR, VERSION_DIR.is_dir()),
        ("CONTRRF", CONTRRF_DIR, CONTRRF_DIR.is_dir()),
        ("RFC", RFC_DIR, RFC_DIR.is_dir()),
        ("CONTRUCCION", CONTRUCCION_DIR, CONTRUCCION_DIR.is_dir()),
        ("Carpeta .dev", DEV_DIR, DEV_DIR.is_dir()),
        ("Python base (.python)", PYTHON_DIR, PYTHON_DIR.is_dir()),
        ("Python ejecutable", PYTHON_EXE, PYTHON_EXE.is_file()),
        ("Pythonw ejecutable", PYTHONW_EXE, PYTHONW_EXE.is_file()),
        ("Entorno venv", VENV_DIR, VENV_DIR.is_dir()),
        ("Config VS Code (.vscode)", VSCODE_DIR, (VSCODE_DIR / "settings.json").is_file()),
        ("Config Pytest (.dev/pytest.ini)", DEV_PYTEST_INI, DEV_PYTEST_INI.is_file()),
        ("Caché Pytest (.pytest_cache)", PYTEST_CACHE_DIR, PYTEST_CACHE_DIR.is_dir()),
        ("Taller TLLDCD src", TLLDCD_SRC, TLLDCD_SRC.is_dir()),
        ("STM_RFC STM_RFPRMN", STM_RFC_DIR, STM_RFC_DIR.is_dir()),
        ("Sistema Principal PRY", PRY_DIR, PRY_DIR.is_dir()),
    ]
    for nombre, ruta, existe in rutas_info:
        estado = "[OK]" if existe else "[FALTA]"
        print(f"  {estado:<8} {nombre:<32} -> {ruta}")

    # 2. Servidor Blockchain Oficial
    print("\n--- [2] SERVIDOR BLOCKCHAIN OFICIAL (STM_BKCH · Puerto 8545) ---")
    st_bkch = estado_blockchain()
    puerto_bkch = "[ONLINE]" if st_bkch["abierto"] else "[OFFLINE]"
    print(f"  {puerto_bkch:<10} Puerto TCP 8545: {'Escuchando' if st_bkch['abierto'] else 'Cerrado/Inactivo'}")
    print(f"  {'[OK]' if st_bkch['script_existe'] else '[FALTA]':<10} Script: {st_bkch['ruta_script']}")
    print(f"  {'[OK]' if st_bkch['ledger_ok'] else '[FALTA]':<10} Ledger oficial: {st_bkch['detalle_ledger']}")
    if st_bkch["responde_http"]:
        info = st_bkch["info_http"]
        print(f"  [INFO]     Bloques en memoria: {info.get('total_bloques', '?')} | Estado: {info.get('estado', 'OK')}")

    # 3. Servidor LucIA 3D
    print("\n--- [3] SERVIDOR CEREBRO LUCIA 3D (Puente TCP · Puerto 8765) ---")
    st_lucia = estado_lucia()
    puerto_lucia = "[ONLINE]" if st_lucia["abierto"] else "[OFFLINE]"
    print(f"  {puerto_lucia:<10} Puerto TCP 8765: {'Escuchando' if st_lucia['abierto'] else 'Cerrado/Inactivo'}")
    print(f"  {'[OK]' if st_lucia['script_existe'] else '[FALTA]':<10} Script: {st_lucia['ruta_script']}")
    ollama_str = "Activo" if st_lucia["ollama_activo"] else "Desconectado"
    print(f"  {'[OK]' if st_lucia['ollama_activo'] else '[AVISO]':<10} Ollama (127.0.0.1:11434): {ollama_str}")
    if st_lucia["modelos_ollama"]:
        print(f"  [INFO]     Modelos Ollama disponibles: {', '.join(st_lucia['modelos_ollama'][:5])}")

    # 4. Visor 3D (.NET 8 WPF)
    print("\n--- [4] VISOR 3D (LCDMWoldVirtualP2P3Dv2.8.4 · .NET 8 WPF) ---")
    st_visor = estado_visor()
    print(f"  {'[OK]' if st_visor['dotnet_disponible'] else '[FALTA]':<10} Compilador .NET: {st_visor['dotnet_version']}")
    print(f"  {'[OK]' if st_visor['csproj_existe'] else '[FALTA]':<10} Proyecto C#: {st_visor['ruta_csproj']}")
    dll_status = "[OK]" if st_visor["dll_compilada"] else "[NO COMPILADO]"
    print(f"  {dll_status:<10} Ensamblado DLL: {st_visor['ruta_dll']}")
    if st_visor["dll_fecha_mod"]:
        print(f"  [INFO]     Última modificación: {st_visor['dll_fecha_mod']}")
    print(f"  {'[OK]' if st_visor['godot_proy_existe'] else '[FALTA]':<10} Proyecto Godot 3D: {st_visor['ruta_godot_proy']}")
    modulos_sesion = list(SEGURIDAD_SESION_UNICA_DIR.glob("*.cs")) if SEGURIDAD_SESION_UNICA_DIR.is_dir() else []
    print(f"  {'[OK]' if SEGURIDAD_SESION_UNICA_DIR.is_dir() else '[FALTA]':<10} Seguridad Sesión Única: {SEGURIDAD_SESION_UNICA_DIR} ({len(modulos_sesion)} módulos CS)")

    print("\n" + "=" * 76)
    print("  COMANDOS DISPONIBLES:")
    print("    python gestor_entorno.py sincronizar          (Aplica variables y config VSCode)")
    print("    python gestor_entorno.py reconstruir-visor    (Compila Visor3D con dotnet)")
    print("    python gestor_entorno.py blockchain [iniciar|detener|reiniciar|estado]")
    print("    python gestor_entorno.py lucia [iniciar|detener|reiniciar|estado]")
    print("    python gestor_entorno.py todo                 (Sincroniza, inicia servicios y compila)")
    print("    python gestor_entorno.py pytest               (Ejecuta suite de pruebas)")
    print("=" * 76)


def comando_todo() -> int:
    """Orquesta la sincronización completa, arranque de servicios y compilación del visor."""
    print(">>> 1/4 Sincronizando entorno y variables...")
    configurar_variables_entorno()
    traer_pytest_ini()
    actualizar_vscode_config()
    limpiar_cache_raiz()

    print("\n>>> 2/4 Verificando/Iniciando Servidor Blockchain (8545)...")
    st_b = estado_blockchain()
    if not st_b["abierto"]:
        ok_b, msg_b = iniciar_blockchain(segundo_plano=True)
        print(f"  {msg_b}")
    else:
        print("  Servidor Blockchain ya se encuentra activo en el puerto 8545.")

    print("\n>>> 3/4 Verificando/Iniciando Servidor LucIA 3D (8765)...")
    st_l = estado_lucia()
    if not st_l["abierto"]:
        ok_l, msg_l = iniciar_lucia(segundo_plano=True)
        print(f"  {msg_l}")
    else:
        print("  Servidor LucIA ya se encuentra activo en el puerto 8765.")

    print("\n>>> 4/4 Reconstruyendo Visor 3D...")
    ok_v, msg_v = reconstruir_visor()
    print(f"  {msg_v}")

    print("\n[FIN] Todo el entorno y los servicios han sido preparados.")
    return 0 if ok_v else 1


def main(argv: Optional[List[str]] = None) -> int:
    if argv is None:
        argv = sys.argv[1:]

    configurar_variables_entorno()

    if not argv or argv[0] in ("informe", "status", "--help", "-h"):
        mostrar_informe()
        return 0

    sub = argv[0].lower()

    if sub == "sincronizar":
        configurar_variables_entorno()
        traer_pytest_ini()
        actualizar_vscode_config()
        limpiar_cache_raiz()
        print("[OK] Entorno sincronizado y variables aplicadas.")
        mostrar_informe()
        return 0

    elif sub in ("reconstruir-visor", "reconstruir_visor", "reconstruir", "build"):
        limpiar = "--limpiar" in argv or "-l" in argv
        publicar = "--publicar" in argv or "-p" in argv
        ok, msg = reconstruir_visor(limpiar=limpiar, publicar_single_file=publicar)
        print(msg)
        return 0 if ok else 1

    elif sub in ("lanzar-visor", "lanzar_visor", "run"):
        return lanzar_visor(argv[1:])

    elif sub == "blockchain":
        accion = argv[1].lower() if len(argv) > 1 else "estado"
        if accion == "iniciar":
            ok, msg = iniciar_blockchain()
            print(msg)
            return 0 if ok else 1
        elif accion == "detener":
            detener_blockchain()
            print("[OK] Servidor Blockchain detenido.")
            return 0
        elif accion == "reiniciar":
            ok, msg = reiniciar_blockchain()
            print(msg)
            return 0 if ok else 1
        elif accion in ("estado", "status"):
            st = estado_blockchain()
            print(f"Puerto 8545 abierto: {st['abierto']}")
            print(f"Ledger: {st['detalle_ledger']}")
            if st["responde_http"]:
                print(f"Info HTTP: {st['info_http']}")
            return 0
        elif accion == "validar":
            ok, tot, msg = validar_ledger_en_disco()
            print(msg)
            return 0 if ok else 1
        else:
            print(f"Acción desconocida para blockchain: {accion}")
            return 1

    elif sub in ("lucia", "lucia3d"):
        accion = argv[1].lower() if len(argv) > 1 else "estado"
        if accion == "iniciar":
            ok, msg = iniciar_lucia()
            print(msg)
            return 0 if ok else 1
        elif accion == "detener":
            detener_lucia()
            print("[OK] Servidor LucIA detenido.")
            return 0
        elif accion == "reiniciar":
            ok, msg = reiniciar_lucia()
            print(msg)
            return 0 if ok else 1
        elif accion in ("estado", "status"):
            st = estado_lucia()
            print(f"Puerto 8765 abierto: {st['abierto']}")
            print(f"LucIA listo: {st['listo']}")
            print(f"Ollama activo: {st['ollama_activo']}")
            if st["modelos_ollama"]:
                print(f"Modelos Ollama: {st['modelos_ollama']}")
            return 0
        else:
            print(f"Acción desconocida para lucia: {accion}")
            return 1

    elif sub == "todo":
        return comando_todo()

    elif sub == "pytest":
        return ejecutar_pytest(argv[1:])

    elif sub in ("traer-pytest", "traer_pytest"):
        traer_pytest_ini()
        return 0

    elif sub == "vscode":
        actualizar_vscode_config()
        return 0

    elif sub == "limpiar":
        borrado = limpiar_cache_raiz()
        traer_pytest_ini()
        print(f"Limpieza en raíz: {'Borrados rastros' if borrado else 'Sin archivos sueltos'}")
        return 0

    else:
        print(f"Comando desconocido: {sub}")
        mostrar_informe()
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
