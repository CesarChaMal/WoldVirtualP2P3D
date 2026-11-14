"""MD_STM_mainLCSTM1.py — Núcleo autónomo del orquestador LucIA.

Módulo refactorizado de ``mainLCSTM``: concentra en un único archivo
importable la carga de credenciales, el arranque de los subsistemas
(BKSVCB, PSNRCV, SNSBSTNPRB, IALOCAL, IAFREE, STYLOS, voz, watchdog y
``LC_constructor``) y el bucle conversacional.

El objetivo no cambia: cada turno consulta primero la IA local y solo
después OpenRouter ``:free`` (coste cero), renderiza con STYLOS, emite voz
opcional y deja el aprendizaje en blockchain, pesos neuronales y memoria de
sesión, con la parte pesada fuera del hilo de entrada.

Organización:
    - ``GestorEntornoSeguro``: lectura cacheada y aislada del ``.env``.
    - ``LucIAConstructor``: puente opcional con ``LC_constructor``.
    - ``OrquestadorSistemaLucIA``: orquestador; su clase anidada ``Nucleo``
      unifica las funciones auxiliares (texto, telemetría, estado, arranque
      de subsistemas y renderizado) para que el orquestador solo coordine.

Se puede ejecutar (``python MD_STM_mainLCSTM1.py``) o importar: el único
efecto de la importación es preparar ``sys.path`` y las dependencias
opcionales.
"""
from __future__ import annotations

import atexit
import importlib
import importlib.util
import logging
import os
import queue
import sys
import threading
import time
import weakref
from pathlib import Path
from types import ModuleType
from typing import Any, Callable, Dict, Final, Optional, Tuple

__all__ = [
    "GestorEntornoSeguro",
    "LucIAConstructor",
    "OrquestadorSistemaLucIA",
    "ContextoOrquestadorLucIA",
    "main",
]

logger = logging.getLogger("WoldVirtualP2P3D.LucIA.mainLCSTM")
logger.addHandler(logging.NullHandler())


# ──────────────────────────────────────────────────────────────────────────────
# 1. Rutas del taller: ``Rutas.json`` manda, el cálculo por archivo respalda.
# ──────────────────────────────────────────────────────────────────────────────
CURRENT_FILE: Final[Path] = Path(__file__).resolve()

GNRL_DIR: Final[Path] = CURRENT_FILE.parents[1]      # .../SistemaGeneral
STMGNRL_DIR: Final[Path] = CURRENT_FILE.parents[2]   # .../STMGNRL
_SRC_BASE: Final[Path] = CURRENT_FILE.parents[3]     # .../src
_TLLCD_BASE: Final[Path] = CURRENT_FILE.parents[4]   # .../TLLDCD
_CONTRRF_BASE: Final[Path] = CURRENT_FILE.parents[5]  # .../CONTRRF


def _sumar_ruta(directorio: Path) -> None:
    """Añade ``directorio`` a ``sys.path`` una sola vez."""
    texto = str(directorio)
    if directorio.is_dir() and texto not in sys.path:
        sys.path.insert(0, texto)


for _base in (_CONTRRF_BASE, _TLLCD_BASE, _SRC_BASE, STMGNRL_DIR, GNRL_DIR):
    _sumar_ruta(_base)

try:
    from STM_CH.STM_herramientas.rutas_json import obtener as _obtener_ruta_json
except Exception as _err_rutas:  # pragma: no cover - árbol incompleto
    _obtener_ruta_json = None
    logger.debug("Rutas.json no disponible: %s", _err_rutas)


def _ruta_json_o(clave: str, por_defecto: Path) -> Path:
    """Devuelve la ruta declarada en ``Rutas.json`` o el respaldo local."""
    if _obtener_ruta_json is None:
        return por_defecto
    try:
        return Path(_obtener_ruta_json(clave)).resolve()
    except Exception:
        return por_defecto


SRC_DIR: Final[Path] = _ruta_json_o("raices.src", _SRC_BASE)
TLLCD_DIR: Final[Path] = _ruta_json_o("raices.tllcd", _TLLCD_BASE)
CONTRRF_DIR: Final[Path] = _ruta_json_o("raices.contrrf", _CONTRRF_BASE)
STM_RFC_DIR: Final[Path] = _ruta_json_o("runtime.stm_rfc", CONTRRF_DIR / "STM_RFC")

ENV_FILE: Final[Path] = next(
    (candidato
     for candidato in (GNRL_DIR / ".env", SRC_DIR / ".env", TLLCD_DIR / ".env")
     if candidato.exists()),
    SRC_DIR / ".env",
)

for _base in (STM_RFC_DIR, CONTRRF_DIR, SRC_DIR):
    _sumar_ruta(_base)


# ──────────────────────────────────────────────────────────────────────────────
# 2. Mediador de dependencias (``_imports``) y símbolos opcionales.
# ──────────────────────────────────────────────────────────────────────────────
def _importar_mediador() -> Optional[ModuleType]:
    """Importa ``_imports`` desde el paquete, el árbol o su ruta directa."""
    for nombre in ("STMGNRL.SistemaGeneral._imports", "_imports"):
        try:
            return importlib.import_module(nombre)
        except Exception as err:
            logger.debug("mediador %s no importable: %s", nombre, err)
    ruta = GNRL_DIR / "_imports.py"
    if not ruta.is_file():
        return None
    try:
        especificacion = importlib.util.spec_from_file_location(
            "STMGNRL.SistemaGeneral._imports", ruta)
        if especificacion is None or especificacion.loader is None:
            return None
        modulo = importlib.util.module_from_spec(especificacion)
        sys.modules[especificacion.name] = modulo
        especificacion.loader.exec_module(modulo)
        return modulo
    except Exception as err:
        logger.debug("mediador por ruta no cargable: %s", err)
        return None


_mediador: Optional[ModuleType] = _importar_mediador()


def _dep(nombre: str, por_defecto: Any = None) -> Any:
    """Atributo del mediador o un valor por defecto seguro."""
    if _mediador is None:
        return por_defecto
    return getattr(_mediador, nombre, por_defecto)


_RFPRMN_DISPONIBLE: Final[bool] = bool(_dep("RFPRMN_DISPONIBLE", False))
_RFPRMN: Final[Any] = _dep("modulo_rfprmn")
_ClienteIAFree: Final[Any] = _dep("ClienteIAFree")
get_cliente_iafree: Final[Any] = _dep("get_cliente_iafree")

EstiloTerminalLucIA: Final[Any] = _dep("EstiloTerminalLucIA")
badge_turno: Final[Any] = _dep("badge_turno")
banner_bienvenida: Final[Any] = _dep("banner_bienvenida")
formatear_respuesta_lucia: Final[Any] = _dep("formatear_respuesta_lucia")
panel_ayuda_comandos: Final[Any] = _dep("panel_ayuda_comandos")

_RPLC_DISPONIBLE: Final[bool] = bool(_dep("RPLC_DISPONIBLE", False))
reprocesar_con_metricas: Final[Any] = _dep("reprocesar_con_metricas")

_VOZ_DISPONIBLE: Final[bool] = bool(_dep("VOZ_DISPONIBLE", False))
_VOZ_MOTIVO: Final[str] = str(_dep("VOZ_MOTIVO", "") or "")
_hablar_voz: Final[Any] = _dep("hablar_voz")

GestorHRTLucIA: Final[Any] = _dep("GestorHRTLucIA")
GestorJSONLucIA: Final[Any] = _dep("GestorJSONLucIA")
get_gestor_ialocal: Final[Any] = _dep("get_gestor_ialocal")

_WATCHDOG_DISPONIBLE: Final[bool] = bool(_dep("WATCHDOG_DISPONIBLE", False))
_arrancar_watchdog: Final[Any] = _dep("arrancar_watchdog")
_MEMORIA_DISPONIBLE: Final[bool] = bool(_dep("MEMORIA_DISPONIBLE", False))
get_memoria: Final[Any] = _dep("get_memoria")

get_openrouter_vault_key: Final[Any] = _dep("get_openrouter_vault_key", lambda: "")
confirmar_vault_openrouter: Final[Any] = _dep("confirmar_vault_openrouter", lambda: None)
registrar_key_vault_openrouter: Final[Any] = _dep("registrar_key_vault_openrouter", lambda: None)
obtener_componentes_blockchain: Final[Any] = _dep("obtener_componentes_blockchain")
obtener_conversor_pesos_blockchain: Final[Any] = _dep("obtener_conversor_pesos_blockchain")
instanciar_sesion_p2p: Final[Any] = _dep("instanciar_sesion_p2p")
limpiar_rutas_stmch: Final[Any] = _dep("limpiar_rutas_stmch")
importar_constructor: Final[Any] = _dep("importar_constructor")


class _SelectorModelosBase:
    """Base mínima si ``SBSTM.selector_modelos`` no está disponible."""

    cliente_iafree: Any = None

    def listar_modelos_gratuitos(self) -> None:
        print("  Selector de modelos no disponible (IAFREE/SelectorModelos ausente).")


_selector_modelos = _dep("SelectorModelos", None)
_BASE_SELECTOR: Final[type] = (_selector_modelos
                               if isinstance(_selector_modelos, type)
                               else _SelectorModelosBase)

_inicializar_stmch: Final[Any] = _dep("init_stmch_rutas")
if callable(_inicializar_stmch):
    try:
        _inicializar_stmch()
    except Exception as _err_stmch:  # pragma: no cover
        logger.debug("init_stmch_rutas: %s", _err_stmch)


# ──────────────────────────────────────────────────────────────────────────────
# 3. Constantes de presentación, comandos y utilidades puras.
# ──────────────────────────────────────────────────────────────────────────────
_OK: Final[str] = "\033[38;5;48m"
_AVISO: Final[str] = "\033[38;5;214m"
_ERROR: Final[str] = "\033[38;5;203m"
_AZUL: Final[str] = "\033[38;5;51m"
_MAGENTA: Final[str] = "\033[38;5;141m"
_DORADO: Final[str] = "\033[38;5;220m"
_RESET: Final[str] = "\033[0m"

_COMANDOS_RFPRMN: Final[Dict[str, str]] = {
    "crear carpeta": "comando_crear_carpeta",
    "crear archivo": "comando_crear_archivo",
    "refactorizar": "comando_refactorizar",
    "actualizar": "actualizar",
    "enviar": "comando_enviar",
    "ia local": "comando_ia_local",
    "cerrar": "cerrar",
}

_PUERTO_BKS: Final[int] = 8545
_INTERVALO_WATCHDOG_S: Final[float] = 60.0


def _float(valor: Any, por_defecto: float = 0.0) -> float:
    """Convierte a ``float`` sin propagar valores inválidos."""
    try:
        return float(valor)
    except (TypeError, ValueError):
        return por_defecto


def _es_tty() -> bool:
    """True si la entrada estándar es una terminal interactiva."""
    try:
        return bool(sys.stdin.isatty())
    except Exception:
        return False


# ──────────────────────────────────────────────────────────────────────────────
# 4. Entorno seguro.
# ──────────────────────────────────────────────────────────────────────────────
class GestorEntornoSeguro:
    """Carga y gestiona de forma aislada credenciales y rutas desde ``.env``.

    La lectura se cachea por ruta y ``mtime``: pedirla en cada arranque no
    vuelve a tocar disco si el archivo no cambió.
    """

    _cache: Dict[str, Any] = {"ruta": None, "mtime": -1.0, "variables": {}}

    @staticmethod
    def _leer(ruta_env: Path) -> Dict[str, str]:
        variables: Dict[str, str] = {}
        try:
            contenido = ruta_env.read_text(encoding="utf-8-sig", errors="replace")
        except OSError as err:
            sys.stderr.write(f"[mainLCSTM] Advertencia leyendo .env: {err}\n")
            return variables
        for linea in contenido.splitlines():
            limpia = linea.strip().lstrip("\ufeff")
            if not limpia or limpia.startswith("#") or "=" not in limpia:
                continue
            clave, _, valor = limpia.partition("=")
            nombre = clave.strip()
            if not nombre:
                continue
            valor = valor.strip().strip("'\"")
            variables[nombre] = valor
            if os.environ.get(nombre) != valor:
                os.environ[nombre] = valor
        return variables

    @classmethod
    def cargar_variables(cls, ruta_env: Path | str = ENV_FILE,
                         recargar: bool = False) -> Dict[str, str]:
        """Carga el ``.env`` (cacheado) y devuelve una copia de las variables."""
        ruta = Path(ruta_env)
        try:
            mtime = ruta.stat().st_mtime
        except OSError:
            return {}
        cache = cls._cache
        if not recargar and cache["ruta"] == ruta and cache["mtime"] == mtime:
            return dict(cache["variables"])
        variables = cls._leer(ruta)
        cache.update(ruta=ruta, mtime=mtime, variables=variables)
        return dict(variables)

    @classmethod
    def obtener(cls, clave: str, por_defecto: str = "") -> str:
        """Valor de la clave en el ``.env`` o en el entorno del proceso."""
        valor = cls.cargar_variables().get(clave)
        if valor is None:
            valor = os.getenv(clave, por_defecto)
        return valor

    @classmethod
    def obtener_openrouter_key(cls) -> str:
        """Clave de OpenRouter: vault primero, ``.env``/entorno después."""
        try:
            clave = get_openrouter_vault_key()
        except Exception as err:
            logger.debug("vault OpenRouter: %s", err)
            clave = ""
        if clave:
            return str(clave).strip()
        return cls.obtener("OPENROUTER_API_KEY").strip()


# ──────────────────────────────────────────────────────────────────────────────
# 5. Constructor LucIA (opcional).
# ──────────────────────────────────────────────────────────────────────────────
class LucIAConstructor:
    """Puente con ``LC_constructor``: LucIA crea sus jerarquías sola.

    Es opcional: si el módulo no carga, ``disponible`` es False y el
    orquestador sigue conversando con normalidad.
    """

    VERBOS: Final[Tuple[str, ...]] = (
        "crear", "crea", "construir", "construye", "preparar", "prepara",
        "generar", "genera", "montar", "monta", "hacer", "haz", "hazme",
    )
    CLAVES: Final[Tuple[Tuple[str, Tuple[str, ...]], ...]] = (
        ("crear todo", ("todo", "las tres", "secuencia", "proyecto completo")),
        ("crear mtbgt", ("mtbgt", "godot")),
        ("crear red neuronal", ("red neuronal", "redneuronal", "red")),
        ("crear archivo", ("crear archivo", "archivo vacio", "archivo vacío",
                           "archivos vacios", "archivos vacíos")),
        ("crear carpeta", ("carpeta", "src", "estructura")),
    )

    def __init__(self) -> None:
        try:
            self._modulo = importar_constructor() if callable(importar_constructor) else None
        except Exception as err:
            logger.debug("LC_constructor no cargado: %s", err)
            self._modulo = None
        self._error = "" if self._modulo is not None else "LC_constructor no disponible"
        self._preparado = False
        self._total = 0
        self._total_archivos = 0

    @property
    def disponible(self) -> bool:
        """True si ``LC_constructor`` se pudo cargar."""
        return self._modulo is not None

    def capacidades(self) -> str:
        """Texto para el prompt del sistema con lo que puede construir."""
        if not self.disponible:
            return ""
        return (
            "También tienes el constructor automático de LucIA y puedes usarlo:\n"
            "- `crear carpeta`: LCtrabajando/src con sus 14 subcarpetas;\n"
            "- `crear red neuronal`: LC_STM/RedNeuronal con sus 6 familias;\n"
            "- `crear archivo`: un archivo vacío o la jerarquía de código de "
            "src (`.py`, `.cs`, `.gd`, etc.);\n"
            "- `crear mtbgt`: MTBGT con .godot, addons, AreaDeTrabajo, gdscrip, "
            "gdshader, scene y project.godot vacío;\n"
            "- `crear todo`: ejecuta las tres en orden.\n"
            "LC_constructor está EN CONSTRUCCIÓN y ya operativo para crear "
            "estructura; es la base de tu auto-refactor. Reconoce esta capacidad "
            "y no la niegues. Al arrancar ejecutas `crear todo` automáticamente, "
            "sin esperar a que te lo pidan. Cuando te pidan crear esas "
            "jerarquías, ejecuta el comando; no digas que no puedes."
        )

    def detectar(self, texto: str) -> Optional[str]:
        """Acción de construcción pedida en la frase, o ``None``."""
        minuscula = texto.lower()
        if any(negacion in minuscula for negacion in
               ("no crear", "no crees", "no quiero crear", "cancelar")):
            return None
        if not any(verbo in minuscula for verbo in self.VERBOS):
            return None
        for accion, claves in self.CLAVES:
            if any(clave in minuscula for clave in claves):
                return accion
        return None

    def _jerarquias_presentes(self) -> bool:
        """True si las tres jerarquías del constructor ya existen."""
        if self._modulo is None:
            return False
        try:
            modulo = self._modulo
            return bool(
                (Path(modulo.RUTA_PREDETERMINADA)
                 / modulo.NOMBRE_PREDETERMINADO).is_dir()
                and (Path(modulo.RUTA_RED_PREDETERMINADA)
                     / modulo.NOMBRE_RED_PREDETERMINADO).is_dir()
                and Path(modulo.RUTA_MTBGT_PREDETERMINADA).is_dir())
        except Exception as err:
            logger.debug("jerarquías del constructor: %s", err)
            return False

    def _ejecutar(self, accion: str) -> Dict[str, Any]:
        """Ejecuta una acción del constructor con sus valores por defecto."""
        if self._preparado and self._jerarquias_presentes():
            return {
                "ok": True,
                "accion": accion,
                "ruta": "LCtrabajando",
                "utilidad": 1.0,
                "motivo": "jerarquías ya preparadas",
                "texto": ("LC_constructor: las jerarquías de LucIA ya están "
                          "preparadas (src, RedNeuronal, MTBGT)."),
                "resultado": {"ok": True, "total": self._total, "comando": accion},
            }
        if self._modulo is None:
            return {"ok": False, "accion": accion, "motivo": self._error}
        try:
            resultado = self._modulo.ejecutar_orden(accion)
            if not isinstance(resultado, dict):
                resultado = {"ok": False, "error": "resultado no-diccionario"}
            texto_resultado = self._modulo.formatear_resultado(resultado)
        except Exception as error:
            return {"ok": False, "accion": accion,
                    "motivo": f"{type(error).__name__}: {error}"}
        ruta = (resultado.get("ruta_mtbgt") or resultado.get("ruta")
                or "LCtrabajando")
        return {
            "ok": bool(resultado.get("ok")),
            "accion": accion,
            "ruta": str(ruta),
            "utilidad": 1.0,
            "motivo": ("jerarquía creada correctamente" if resultado.get("ok")
                       else str(resultado.get("error", "sin detalle"))),
            "texto": texto_resultado,
            "resultado": resultado,
        }

    def atender(self, texto: str) -> Optional[Dict[str, Any]]:
        """Ejecuta la construcción detectada y devuelve el resultado."""
        accion = self.detectar(texto)
        if accion is None:
            return None
        return self._ejecutar(accion)

    def construir_base(self) -> Dict[str, Any]:
        """Crea ``src`` con sus carpetas y sus archivos de código vacíos."""
        carpetas = self._ejecutar("crear carpeta")
        if not carpetas.get("ok"):
            return carpetas
        archivos = self._ejecutar("crear archivo")
        crudo_archivos = archivos.get("resultado") or {}
        self._preparado = True
        self._total = int((carpetas.get("resultado") or {}).get("total", 0))
        self._total_archivos = int(crudo_archivos.get("total", 0))
        return {
            "ok": bool(archivos.get("ok", True)),
            "accion": "crear base",
            "ruta": carpetas.get("ruta", ""),
            "texto": f"{carpetas.get('texto', '')}\n{archivos.get('texto', '')}".strip(),
            "resultado": {
                "ok": True,
                "total": self._total,
                "total_archivos": self._total_archivos,
                "archivos": crudo_archivos.get("archivos", []),
                "comando": "crear carpeta+archivo",
                "red_general": (carpetas.get("resultado") or {}).get(
                    "red_general", {}),
            },
            "carpetas": carpetas,
            "archivos_resultado": archivos,
        }

    def construir_todo(self) -> Dict[str, Any]:
        """Ejecuta ``crear todo`` una vez y recuerda que quedó preparado."""
        resultado = self._ejecutar("crear todo")
        if resultado.get("ok"):
            self._preparado = True
            crudo = resultado.get("resultado") or {}
            self._total = int(crudo.get("total", 0))
            acciones = crudo.get("acciones") or []
            self._total_archivos = (int(acciones[-1].get("total", 0))
                                    if acciones else 0)
        return resultado


# ──────────────────────────────────────────────────────────────────────────────
# 6. Orquestador y su núcleo funcional anidado.
# ──────────────────────────────────────────────────────────────────────────────
class OrquestadorSistemaLucIA(_BASE_SELECTOR):  # type: ignore[misc, valid-type]
    """Orquestador maestro: BKSVCB, SNSBSTNPRB, IAFREE, STYLOS y 50 neuronas.

    La clase anidada :class:`Nucleo` agrupa todas las funciones auxiliares
    (contexto, reflejo, telemetría, estado, arranque de subsistemas y
    renderizado). El orquestador solo coordina y custodia el estado.
    """

    class Nucleo:
        """Funciones unificadas del orquestador.

        La capa de presentación y el arranque de cada subsistema viven aquí.
        Los métodos de arranque devuelven ``(objeto, línea_de_estado)`` para
        que el orquestador asigne el objeto y muestre todo con un formato único.
        """

        CLAVES_CONTEXTO: Final[Tuple[str, ...]] = (
            "constructor", "refactoriz", "modulo nuevo", "módulo nuevo",
            "auto-refactor", "auto refactor",
        )
        AVISO_CONTEXTO: Final[str] = (
            "[ESTADO REAL DEL SISTEMA: Tienes LC_constructor EN CONSTRUCCIÓN y "
            "ya operativo; crea automáticamente las jerarquías src, RedNeuronal "
            "y MTBGT y será la base de tu auto-refactor. Reconoce esta capacidad; "
            "no la niegues ni digas que no estás construyendo ese módulo.]\n"
        )

        # ── texto y presentación ────────────────────────────────────────────
        @classmethod
        def contexto_capacidades(cls, prompt: str) -> str:
            """Refuerza en el mensaje que ``LC_constructor`` ya está activo."""
            minuscula = prompt.lower()
            if not any(clave in minuscula for clave in cls.CLAVES_CONTEXTO):
                return prompt
            return cls.AVISO_CONTEXTO + prompt

        @staticmethod
        def reflejo_interno(info: Dict[str, Any]) -> str:
            """Respuesta de respaldo cuando no hay IA disponible."""
            tono = info.get("tono_cognitivo", "reflexivo")
            valencia = _float(info.get("estado_emocional", 0.0))
            norma = _float(info.get("norma_delta_aplicada", 0.0))
            return (
                f"Estimulo cognitivo asimilado en red distribuida. Tono: **{tono}** | "
                f"Valencia afectiva: `{valencia:+.3f}` | Delta sinaptico: `{norma:.5f}`.\n"
                "Pesos neuronales sincronizados y activos en persistencia `PSNRL`."
            )

        @staticmethod
        def imprimir_respuesta(texto: str, modelo: str) -> None:
            """Renderiza la respuesta (STYLOS o texto plano)."""
            if EstiloTerminalLucIA is not None and callable(formatear_respuesta_lucia):
                formatear_respuesta_lucia(texto, modelo=modelo)
            else:
                print(f"\nLucIA [{modelo}] ▶ {texto}\n")

        @staticmethod
        def imprimir_telemetria(*, turno: int, duracion_ms: float, modelo: str,
                                deriva: float, neuronas: int, cid: str,
                                txs: int) -> None:
            """Muestra la barra de telemetría del turno."""
            if EstiloTerminalLucIA is not None and callable(badge_turno):
                badge_turno(turno=turno, dt=duracion_ms, mod=modelo, der=deriva,
                            neu=neuronas, cid=cid, txs=txs)
                return
            print(f"  Turno #{turno} | {duracion_ms:.0f}ms | Mod: {modelo} | "
                  f"Deriva: {deriva:.4f} | CID: {cid[:16]}")

        @staticmethod
        def estado_sistema(*, bloques: int, cadena_ok: bool, error_cadena: str,
                           txs: int, neuronas: int, deriva: float, turnos: int,
                           modelo: str, costo_usd: float) -> Dict[str, str]:
            """Construye el reporte de estado integral."""
            cadena = "VALIDA E INMUTABLE" if cadena_ok else f"ERROR: {error_cadena}"
            return {
                "Cadena de Bloques": f"{bloques} bloques [{cadena}]",
                "Txs en Espera": str(txs),
                "Neuronas Activas": f"{neuronas} (ENRN, RF_EN, RF_SL, RNP, SLRN)",
                "Deriva Muon": f"{deriva:.6f}",
                "Turnos de Sesion": str(turnos),
                "Modelo Activo": modelo,
                "Costo Acumulado": f"${costo_usd:.2f} USD (100% Free)",
            }

        @staticmethod
        def imprimir_estado(datos: Dict[str, str]) -> None:
            """Dibuja la tarjeta de estado (STYLOS) o la lista plana."""
            if (EstiloTerminalLucIA is not None
                    and callable(EstiloTerminalLucIA.renderizar_tarjeta_sistema)):
                EstiloTerminalLucIA.renderizar_tarjeta_sistema(
                    "ESTADO INTEGRAL DE LUCIA (2026)", datos)
                print()
                return
            for clave, valor in datos.items():
                print(f"  {clave}: {valor}")

        @staticmethod
        def imprimir_bloque(bloque: Any) -> None:
            """Notifica el bloque recién minado."""
            if (EstiloTerminalLucIA is not None
                    and callable(EstiloTerminalLucIA.renderizar_notificacion_bloque)):
                EstiloTerminalLucIA.renderizar_notificacion_bloque(
                    bloque.indice, bloque.hash_bloque)
                return
            print(f"Bloque #{bloque.indice} minado: {bloque.hash_bloque}")

        @staticmethod
        def imprimir_banner(sesion_id: str, modelo: str) -> None:
            """Banner de bienvenida (STYLOS) o cabecera plana."""
            if EstiloTerminalLucIA is not None and callable(banner_bienvenida):
                banner_bienvenida(sesion_id, modelo)
            else:
                print(f"\n[+] WoldVirtualP2P3D -- LucIA Console 2026 | Mod: {modelo}")

        @staticmethod
        def imprimir_watchdog() -> None:
            """Arranca el watchdog y reporta su estado."""
            if not (_WATCHDOG_DISPONIBLE and callable(_arrancar_watchdog)):
                print(f"  [WDG] Watchdog             : {_AVISO}NO DISPONIBLE{_RESET}")
                return
            try:
                _arrancar_watchdog(intervalo_s=_INTERVALO_WATCHDOG_S)
                print(f"  [WDG] Watchdog subsistemas : {_OK}ACTIVO{_RESET} | "
                      f"Ciclo: {_INTERVALO_WATCHDOG_S:.0f} s")
            except Exception as err:
                print(f"  [WDG] Watchdog             : {_AVISO}AVISO ({err}){_RESET}")

        # ── arranque de subsistemas ─────────────────────────────────────────
        @staticmethod
        def arrancar_blockchain() -> Tuple[Any, str]:
            """Levanta BKSVCB y devuelve el servidor con su línea de estado."""
            if not callable(obtener_componentes_blockchain):
                raise RuntimeError("BKSVCB no disponible")
            get_bks, start_bks = obtener_componentes_blockchain()
            servidor = get_bks()
            bloques = len(getattr(servidor, "cadena", ()) or ())
            start_bks(puerto=_PUERTO_BKS)
            return servidor, f"{_OK}ACTIVA{_RESET} | Bloques: {_DORADO}{bloques}{_RESET}"

        @staticmethod
        def mostrar_cadena(servidor: Any) -> None:
            """Muestra los hashes de la cadena sin tumbar el arranque."""
            try:
                servidor.mostrar_cadena_hashes_terminal()
            except Exception as err:
                logger.debug("cadena de hashes: %s", err)

        @staticmethod
        def arrancar_pesos() -> Tuple[Any, str]:
            """Levanta el conversor PSNRCV y sus sinapsis."""
            if not callable(obtener_conversor_pesos_blockchain):
                raise RuntimeError("PSNRCV no disponible")
            conversor = obtener_conversor_pesos_blockchain()
            sinapsis = len(getattr(conversor, "neuronas", ()) or ())
            return conversor, (f"{_OK}OK{_RESET} | Sinapsis: "
                               f"{_MAGENTA}{sinapsis} Activas{_RESET}")

        @staticmethod
        def aplicar_ledger(servidor: Any) -> str:
            """Transforma el ledger en pesos y arranca el actualizador."""
            resultado = servidor.transformar_ledger_a_pesos_neuronales() or {}
            try:
                servidor.arrancar_actualizador_red()
            except Exception as err:
                logger.debug("actualizador de red: %s", err)
            total = resultado.get("total_bloques", 0)
            norma = _float(resultado.get("norma_acumulada", 0.0))
            return (f"{_OK}{total} bloques{_RESET} | "
                    f"Norma: {_DORADO}{norma}{_RESET}")

        @staticmethod
        def acoplar_p2p() -> Tuple[Any, str]:
            """Instancia la sesión P2P (SNSBSTNPRB)."""
            if not callable(instanciar_sesion_p2p):
                raise RuntimeError("SNSBSTNPRB no disponible")
            sesion = instanciar_sesion_p2p()
            return sesion, (f"{_OK}ACOPLADA{_RESET} | "
                            f"Motor STYLOS: {_AZUL}ACTIVO{_RESET}")

        @staticmethod
        def inicializar_registros() -> Tuple[Any, Any]:
            """Crea GestorJSON/GestorHRT y muestra sus líneas de estado."""
            gestor_json = None
            if GestorJSONLucIA is not None:
                try:
                    gestor_json = GestorJSONLucIA()
                    print(f"  [JSON] {gestor_json.linea_estado()}")
                except Exception as err:
                    logger.debug("GestorJSONLucIA: %s", err)
            gestor_hrts = None
            if GestorHRTLucIA is not None:
                try:
                    gestor_hrts = GestorHRTLucIA()
                    print(f"  [HRTS] {gestor_hrts.linea_estado()}")
                except Exception as err:
                    logger.debug("GestorHRTLucIA: %s", err)
            return gestor_json, gestor_hrts

        @staticmethod
        def linea_voz() -> None:
            """Muestra el estado del motor de voz."""
            try:
                from SBSTM.SistemasLC.STM_SBTM_3.voice_engine import estado_voz
            except Exception as err:
                print(f"  [VOZ]  Voz Neuronal LucIA  : {_ERROR}NO DISPONIBLE "
                      f"({_VOZ_MOTIVO or err}){_RESET}")
                return
            try:
                print(f"  {estado_voz()}")
            except Exception as err:
                print(f"  [VOZ]  Voz Neuronal LucIA  : {_AVISO}AVISO ({err}){_RESET}")

        @classmethod
        def conectar_neural(cls, orquestador: "OrquestadorSistemaLucIA") -> None:
            """Conecta el subsistema neural y su socket IPC."""
            try:
                from SBSTM.SistemasLC.STM_SBTM_3.subsistema_neural import (
                    get_subsistema_neural,
                )
                subsistema = get_subsistema_neural(arrancar_socket=True)
                subsistema.conectar_main_lcstm(orquestador)
                puerto = subsistema.canal_socket.puerto
            except Exception as err:
                print(f"  [SBSTM] Subsistema Neural   : {_AVISO}AVISO ({err}){_RESET}")
                return
            orquestador.subsistema_neural = subsistema
            print(f"  [SBSTM] Subsistema Neural   : {_OK}ACTIVO{_RESET} | "
                  f"Socket Puerto: {_MAGENTA}{puerto}{_RESET} | "
                  f"Destino: {_DORADO}LCtrabajando{_RESET}")

        @classmethod
        def conectar_evolutivo(cls, orquestador: "OrquestadorSistemaLucIA") -> None:
            """Conecta el subsistema evolutivo (4 fases)."""
            try:
                from SBSTM.SistemasLC.STM_SBTM_3.subsistema_evolutivo import (
                    get_subsistema_evolutivo,
                )
                subsistema = get_subsistema_evolutivo()
            except Exception as err:
                print(f"  [SBSTM] Subsistema Evolutivo: {_AVISO}AVISO ({err}){_RESET}")
                return
            orquestador.subsistema_evolutivo = subsistema
            print(f"  [SBSTM] Subsistema Evolutivo: {_OK}ACTIVO{_RESET} | "
                  f"4 Fases: {_MAGENTA}Autopoiesis{_RESET}")

    # ── ciclo de vida ───────────────────────────────────────────────────────
    def __init__(self) -> None:
        self.lock = threading.Lock()
        self._lock_cierre = threading.Lock()
        self._lock_worker = threading.Lock()
        self._cerrado = False
        self.servidor_bks: Any = None
        self.conversor_psn: Any = None
        self.sesion_p2p: Any = None
        self.cliente_iafree: Any = None
        self.subsistema_neural: Any = None
        self.subsistema_evolutivo: Any = None
        self.gestor_json: Any = None
        self.gestor_hrts: Any = None
        self.activa = False
        self.turno_actual = 0
        self.sesion_id = f"LUCIA_{time.strftime('%Y%m%d_%H%M%S')}"
        self.memoria = self._crear_memoria()
        self.constructor_lucia = LucIAConstructor()
        self._reprocesador = (reprocesar_con_metricas
                              if _RPLC_DISPONIBLE and callable(reprocesar_con_metricas)
                              else None)
        self._gestor_ialocal: Any = None
        self._cola_post: queue.SimpleQueue = queue.SimpleQueue()
        self._worker_post: Optional[threading.Thread] = None
        self._mandos: Optional[Tuple[Any, Any]] = None
        self._comandos: Dict[str, Callable[[str], bool]] = {
            "salir": self._cmd_salir, "exit": self._cmd_salir,
            "quit": self._cmd_salir, "estado": self._cmd_estado,
            "status": self._cmd_estado, "ayuda": self._cmd_ayuda,
            "help": self._cmd_ayuda, "modelos": self._cmd_modelos,
            "models": self._cmd_modelos, "free": self._cmd_modelos,
            "minar": self._cmd_minar, "mine": self._cmd_minar,
            "api": self._cmd_api, "apikey": self._cmd_api,
            "registrar api": self._cmd_api,
        }
        _registrar_cierre(self)

    def _crear_memoria(self) -> Any:
        if not (_MEMORIA_DISPONIBLE and callable(get_memoria)):
            return None
        try:
            return get_memoria(self.sesion_id)
        except Exception as err:
            logger.debug("memoria de sesión no disponible: %s", err)
            return None

    @staticmethod
    def _paso(indice: int, etiqueta: str, detalle: str, salto: bool = False) -> None:
        """Imprime una línea de arranque con formato uniforme."""
        print(f"  [{indice}/4] {etiqueta}: {detalle}" + ("\n" if salto else ""))

    def _modelo_activo(self) -> str:
        """Id del modelo activo de IAFREE, o ``N/A`` si no hay cliente."""
        gestor = getattr(self.cliente_iafree, "gestor", None)
        if gestor is None:
            return "N/A"
        try:
            return str((gestor.obtener_modelo_activo() or {}).get("id", "N/A"))
        except Exception as err:
            logger.debug("modelo activo: %s", err)
            return "N/A"

    def _preparar_cliente_ia(self) -> None:
        if callable(get_cliente_iafree):
            try:
                self.cliente_iafree = get_cliente_iafree()
            except Exception as err:
                logger.debug("IAFREE no disponible: %s", err)

    def preparar_constructor_automatico(self) -> None:
        """LucIA crea sus jerarquías sola al arrancar, sin que se lo pidan."""
        constructor = self.constructor_lucia
        if constructor is None or not constructor.disponible:
            print("  [CTOR] Constructor LucIA     : NO DISPONIBLE")
            return
        print("  [CTOR] Constructor LucIA     : ACTIVO | src (carpetas + archivos)")
        try:
            resultado = constructor.construir_base() or {}
        except Exception as err:
            print(f"  [CTOR] Construcción automática: AVISO ({err})")
            return
        if not resultado.get("ok"):
            print(f"  [CTOR] Construcción automática: AVISO "
                  f"({resultado.get('motivo', 'sin detalle')})")
            return
        crudo = resultado.get("resultado") or {}
        print(f"  [CTOR] Construcción automática: OK | {crudo.get('total', 0)} "
              f"carpetas | {crudo.get('total_archivos', 0)} archivos | src")
        red = crudo.get("red_general") or {}
        if red.get("omitida"):
            print("  [CTOR] Cálculo neuronal   : OMITIDO")
        elif red.get("ok"):
            diag = red.get("diagnostico") or {}
            print(f"  [CTOR] Cálculo neuronal   : OK | "
                  f"modo={diag.get('modo', '?')} | "
                  f"{diag.get('neurona_enrn', '?')} -> "
                  f"{diag.get('neurona_rfen', '?')}")
        else:
            print(f"  [CTOR] Cálculo neuronal   : AVISO "
                  f"({red.get('error', 'sin detalle')})")

    def _arrancar_nucleo(self) -> bool:
        """Arranca los cuatro subsistemas principales; ``False`` si uno es fatal."""
        try:
            self.servidor_bks, detalle = self.Nucleo.arrancar_blockchain()
        except Exception as err:
            self._paso(1, "Blockchain BKSVCB    ", f"{_ERROR}ERROR ({err}){_RESET}")
            return False
        self._paso(1, "Blockchain BKSVCB    ", detalle)
        self.Nucleo.mostrar_cadena(self.servidor_bks)

        try:
            self.conversor_psn, detalle = self.Nucleo.arrancar_pesos()
        except Exception as err:
            self._paso(2, "Conversor PSNRCV     ", f"{_ERROR}ERROR ({err}){_RESET}")
            return False
        self._paso(2, "Conversor PSNRCV     ", detalle)

        try:
            self._paso(3, "Ledger -> Pesos PSNRL",
                       self.Nucleo.aplicar_ledger(self.servidor_bks))
        except Exception as err:
            self._paso(3, "Ledger -> Pesos PSNRL", f"{_AVISO}AVISO ({err}){_RESET}")

        try:
            self.sesion_p2p, detalle = self.Nucleo.acoplar_p2p()
        except Exception as err:
            self._paso(4, "Sesion SNSBSTNPRB    ",
                       f"{_AVISO}AVISO ({err}){_RESET}", salto=True)
            return True
        self._paso(4, "Sesion SNSBSTNPRB    ", detalle, salto=True)
        return True

    def inicializar_subsistemas(self) -> bool:
        """Inicializa blockchain, transductor, IAFREE, registros y voz."""
        GestorEntornoSeguro.cargar_variables()
        if _es_tty() and callable(confirmar_vault_openrouter):
            confirmar_vault_openrouter()
        self._preparar_cliente_ia()
        self.Nucleo.imprimir_banner(self.sesion_id, self._modelo_activo())
        self.preparar_constructor_automatico()
        if not self._arrancar_nucleo():
            return False
        self.gestor_json, self.gestor_hrts = self.Nucleo.inicializar_registros()
        self.Nucleo.linea_voz()
        self.activa = True
        self.Nucleo.imprimir_watchdog()
        self.Nucleo.conectar_neural(self)
        self.Nucleo.conectar_evolutivo(self)
        return True

    # ── turno de diálogo ────────────────────────────────────────────────────
    def procesar_turno_dialogo(self, prompt: str, emitir_voz: bool = True) -> None:
        """Ejecuta el pipeline cognitivo completo de un turno a coste cero.

        1. Pre-procesamiento neuronal (tono y estado emocional).
        2. Inferencia local prioritaria (IALOCAL) y OpenRouter ``:free``.
        3. Formateo y presentación con STYLOS.
        4. Asimilación Hebbiana en PSNRL.
        5. Transacción a BKSVCB y minado, fuera del hilo de entrada.
        """
        if not self.activa and self.conversor_psn is None:
            print("  [TURNO] Subsistemas sin inicializar; "
                  "ejecuta inicializar_subsistemas() primero.")
            return
        if self._atender_constructor(prompt):
            return
        prompt = self.Nucleo.contexto_capacidades(prompt)
        self.turno_actual += 1
        inicio = time.perf_counter()
        estado_previo = self._estado_neuronal(prompt)
        if estado_previo is None:
            return
        respuesta, modelo_usado, latencia_llm = self._generar_respuesta(
            prompt, estado_previo)
        self.Nucleo.imprimir_respuesta(respuesta, modelo_usado)
        if emitir_voz:
            self._emitir_voz(respuesta)
        duracion_ms = (time.perf_counter() - inicio) * 1000.0
        self._registrar_memoria(prompt, respuesta, estado_previo,
                                duracion_ms, modelo_usado)
        self._encolar_post_turno(prompt, respuesta, modelo_usado,
                                 self.turno_actual)
        self._imprimir_telemetria_turno(duracion_ms, "async-tx", modelo_usado)
        logger.debug("turno %d listo en %.1f ms (LLM %.0f ms)",
                     self.turno_actual, duracion_ms, latencia_llm)

    def _estado_neuronal(self, prompt: str) -> Optional[Dict[str, Any]]:
        """Consulta el conversor de pesos; ``None`` si no está operativo."""
        if self.conversor_psn is None:
            print("  [PSNRL] Conversor de pesos no disponible.")
            return None
        try:
            estado = self.conversor_psn.procesar_consulta_a_pesos(prompt)
        except Exception as err:
            logger.warning("consulta neuronal: %s", err)
            print(f"  [PSNRL] {err}")
            return None
        if not isinstance(estado, dict):
            estado = {}
        estado["sesion_id"] = self.sesion_id
        return estado

    def _generar_respuesta(self, prompt: str,
                           estado_previo: Dict[str, Any]) -> Tuple[str, str, float]:
        """Respuesta del turno: IALOCAL, IAFREE y reflejo interno como red."""
        respuesta = ""
        modelo_usado = "Reflejo-Interno"
        latencia = 0.0
        if self._gestor_ialocal is None and callable(get_gestor_ialocal):
            try:
                self._gestor_ialocal = get_gestor_ialocal()
            except Exception as err:
                logger.debug("IALOCAL no disponible: %s", err)
        if self._gestor_ialocal is not None:
            try:
                respuesta, modelo_usado = self._gestor_ialocal.ciclo(
                    prompt, estado_previo, self.conversor_psn, self._reprocesador)
                respuesta = respuesta or ""
                modelo_usado = modelo_usado or "Reflejo-Interno"
            except Exception as err:
                print(f"  [IALocal] {err} -> probando OpenRouter")
        if not respuesta and self.cliente_iafree is not None:
            try:
                autenticado = bool(self.cliente_iafree.esta_autenticado())
            except Exception as err:
                logger.debug("autenticación IAFREE: %s", err)
                autenticado = False
            if autenticado:
                try:
                    texto, identificador, latencia_seg = (
                        self.cliente_iafree.generar_respuesta(
                            prompt=prompt, contexto_neuronal=estado_previo,
                            stream_en_vivo=False))
                except Exception as err:
                    logger.debug("IAFREE: %s", err)
                    texto, identificador, latencia_seg = "", "", 0.0
                if texto and not str(texto).startswith("[IAFREE]"):
                    respuesta = str(texto)
                    modelo_usado = str(identificador or "openrouter/free")
                    latencia = _float(latencia_seg)
        if not respuesta:
            respuesta = self.Nucleo.reflejo_interno(estado_previo)
        elif self._reprocesador is not None and not modelo_usado.startswith("IALocal"):
            try:
                respuesta, _ = self._reprocesador(respuesta, estado_previo)
            except Exception as err:
                logger.debug("RPLC: %s", err)
        return respuesta, modelo_usado, latencia

    def _emitir_voz(self, texto: str) -> None:
        if not (_VOZ_DISPONIBLE and callable(_hablar_voz)):
            return
        try:
            _hablar_voz(texto, esperar=False)
        except Exception as err:
            logger.debug("voz: %s", err)

    def _registrar_memoria(self, prompt: str, respuesta: str,
                           estado_previo: Dict[str, Any], duracion_ms: float,
                           modelo: str) -> None:
        if self.memoria is None:
            return
        try:
            self.memoria.registrar_turno(
                prompt, respuesta,
                tono=estado_previo.get("tono_cognitivo", ""),
                valencia=_float(estado_previo.get("estado_emocional", 0.0)),
                ms=duracion_ms, modelo=modelo,
                deriva=_float(getattr(self.conversor_psn, "deriva_acumulada", 0.0)))
        except Exception as err:
            logger.debug("memoria de sesión: %s", err)

    def _encolar_post_turno(self, prompt: str, respuesta: str, modelo: str,
                            turno: int) -> None:
        """Encola el trabajo posterior sin crear un hilo por turno."""
        try:
            self._cola_post.put_nowait((prompt, respuesta, modelo, turno))
        except Exception as err:
            logger.debug("cola post-turno: %s", err)
        self._arrancar_worker_post()

    def _arrancar_worker_post(self) -> None:
        with self._lock_worker:
            if self._worker_post is not None:
                return
            self._worker_post = threading.Thread(
                target=self._bucle_post_turno, name="LucIA-post-turno",
                daemon=True)
            self._worker_post.start()

    def _bucle_post_turno(self) -> None:
        while True:
            item = self._cola_post.get()
            if item is None:
                return
            try:
                self._post_turno_async(*item)
            except Exception as err:
                logger.debug("post-turno: %s", err)

    def _post_turno_async(self, pr: str, resp: str, mod: str, tur: int) -> None:
        """Persistencia diferida: blockchain, minado y entrenamiento neural."""
        servidor = self.servidor_bks
        if servidor is not None:
            try:
                servidor.registrar_aprendizaje_neural(
                    prompt=pr, respuesta=resp, modelo=f"LucIA-{mod}")
                if tur % 3 == 0:
                    servidor.minar_transacciones_pendientes()
            except Exception as err:
                logger.debug("blockchain post-turno: %s", err)
        neural = self.subsistema_neural
        if neural is not None:
            try:
                neural.entrenar_con_vector(
                    f"{pr} -> {resp}", recompensa=1.0, motivo=f"turno_{tur}")
            except Exception as err:
                logger.debug("neural post-turno: %s", err)

    # ── compatibilidad con la API original ──────────────────────────────────
    def _contexto_capacidades(self, prompt: str) -> str:
        return self.Nucleo.contexto_capacidades(prompt)

    def _generar_reflejo_interno(self, prompt: str, info: Dict[str, Any]) -> str:
        return self.Nucleo.reflejo_interno(info)

    def _imprimir_telemetria_turno(self, duracion_ms: float, cid: str,
                                   modelo_id: str) -> None:
        conversor = self.conversor_psn
        servidor = self.servidor_bks
        self.Nucleo.imprimir_telemetria(
            turno=self.turno_actual, duracion_ms=duracion_ms, modelo=modelo_id,
            deriva=_float(getattr(conversor, "deriva_acumulada", 0.0)),
            neuronas=len(getattr(conversor, "neuronas", ()) or ()),
            cid=cid,
            txs=len(getattr(servidor, "transacciones_pendientes", ()) or ()))

    # ── estado y comandos ───────────────────────────────────────────────────
    def mostrar_estado_sistema(self) -> None:
        """Despliega en terminal el estado de todos los componentes."""
        servidor = self.servidor_bks
        if servidor is None:
            print("  [ESTADO] Blockchain BKSVCB no inicializada.")
            return
        try:
            valida, error = servidor.validar_cadena()
        except Exception as err:
            valida, error = False, err
        conversor = self.conversor_psn
        metricas: Dict[str, Any] = {}
        if self.cliente_iafree is not None:
            try:
                metricas = self.cliente_iafree.obtener_metricas_consumo() or {}
            except Exception as err:
                logger.debug("métricas IAFREE: %s", err)
        datos = self.Nucleo.estado_sistema(
            bloques=len(getattr(servidor, "cadena", ()) or ()),
            cadena_ok=bool(valida), error_cadena=str(error or ""),
            txs=len(getattr(servidor, "transacciones_pendientes", ()) or ()),
            neuronas=len(getattr(conversor, "neuronas", ()) or ()),
            deriva=_float(getattr(conversor, "deriva_acumulada", 0.0)),
            turnos=self.turno_actual, modelo=self._modelo_activo(),
            costo_usd=_float(metricas.get("costo_acumulado_usd", 0.0)))
        self.Nucleo.imprimir_estado(datos)

    def _atender_constructor(self, entrada: str) -> bool:
        constructor = self.constructor_lucia
        if constructor is None:
            return False
        try:
            construccion = constructor.atender(entrada)
        except Exception as err:
            logger.debug("constructor LucIA: %s", err)
            return False
        if construccion is None:
            return False
        print(construccion.get("texto", ""))
        return True

    def ejecutar_comando_rfprmn(self, comando: str) -> bool:
        """Ejecuta un comando RFPRMN; ``False`` para salir del bucle."""
        if not (_RFPRMN_DISPONIBLE and _RFPRMN is not None):
            print(f"RFPRMN no esta disponible en: {STM_RFC_DIR / 'RFPRMN.py'}")
            return True
        if comando == "cerrar":
            try:
                _RFPRMN.cerrar()
            except Exception as err:
                logger.debug("RFPRMN.cerrar: %s", err)
            print(f"\n{_AVISO}Iniciando protocolo de salida y "
                  f"sincronizacion...{_RESET}")
            return False
        funcion = getattr(_RFPRMN, _COMANDOS_RFPRMN.get(comando, ""), None)
        if callable(funcion):
            try:
                funcion()
            except Exception as error:
                print(f"Error ejecutando '{comando}': {error}")
        else:
            print(f"El comando RFPRMN '{comando}' no esta disponible.")
        return True

    def _cmd_salir(self, _entrada: str) -> bool:
        print(f"\n{_AVISO}Iniciando protocolo de salida y sincronizacion...{_RESET}")
        return False

    def _cmd_estado(self, _entrada: str) -> bool:
        self.mostrar_estado_sistema()
        return True

    def _cmd_ayuda(self, _entrada: str) -> bool:
        if callable(panel_ayuda_comandos):
            panel_ayuda_comandos()
        return True

    def _cmd_modelos(self, _entrada: str) -> bool:
        self.listar_modelos_gratuitos()
        return True

    def _cmd_minar(self, _entrada: str) -> bool:
        servidor = self.servidor_bks
        if servidor is None:
            print("  Blockchain BKSVCB no inicializada.")
            return True
        try:
            bloque = servidor.minar_transacciones_pendientes()
        except Exception as err:
            print(f"  No se pudo minar: {err}")
            return True
        if bloque:
            self.Nucleo.imprimir_bloque(bloque)
        else:
            print("  No hay transacciones pendientes para minar.")
        return True

    def _cmd_api(self, _entrada: str) -> bool:
        try:
            registrar_key_vault_openrouter()
        except Exception as err:
            print(f"  No se pudo registrar: {err}")
        return True

    def _seleccionar_modelo(self, modelo_id: str) -> None:
        gestor = getattr(self.cliente_iafree, "gestor", None)
        ok = False
        if gestor is not None:
            try:
                ok = bool(gestor.seleccionar_por_id(modelo_id))
            except Exception as err:
                logger.debug("selección de modelo: %s", err)
        color, mensaje = ((_OK, f"Modelo gratuito seleccionado: {modelo_id}")
                          if ok else
                          (_AVISO, f"Modelo '{modelo_id}' no encontrado "
                                    f"en catalogo :free."))
        print(f"  {color}{mensaje}{_RESET}")

    @staticmethod
    def _cargar_mandos() -> Tuple[Any, Any]:
        try:
            from SBSTM.SistemasLC.STM_SBTM_1.comandos import despachar, es_comando
            return es_comando, despachar
        except Exception as err:
            logger.debug("comandos SBSTM no disponibles: %s", err)
            return None, None

    def _despachar_comando(self, entrada: str) -> bool:
        """Envía la entrada a los comandos SBSTM si corresponde."""
        if self._mandos is None:
            self._mandos = self._cargar_mandos()
        es_comando, despachar = self._mandos
        if es_comando is None or despachar is None:
            return False
        try:
            if not es_comando(entrada):
                return False
            resultado = despachar(entrada)
        except Exception as err:
            logger.debug("comandos SBSTM: %s", err)
            return False
        if resultado:
            print(f"\n{resultado}\n")
        return True

    def ejecutar_bucle_interactivo(self) -> None:
        """Bucle principal de recepción e interpretación de comandos."""
        while self.activa:
            try:
                entrada = input(f"{_AVISO}LucIA> Tu:{_RESET} ").strip()
            except (KeyboardInterrupt, EOFError):
                print()
                break
            if not entrada:
                continue
            comando = entrada.lower()
            manejador = self._comandos.get(comando)
            if manejador is not None:
                if not manejador(entrada):
                    break
                continue
            if comando.startswith("modelo "):
                self._seleccionar_modelo(entrada[7:].strip())
                continue
            if self._atender_constructor(entrada):
                continue
            if comando in _COMANDOS_RFPRMN:
                if not self.ejecutar_comando_rfprmn(comando):
                    break
                continue
            if self._despachar_comando(entrada):
                continue
            self.procesar_turno_dialogo(entrada)
        self.activa = False

    # ── cierre ──────────────────────────────────────────────────────────────
    def _cerrar_sub(self, nombre: str) -> None:
        subsistema = getattr(self, nombre, None)
        if subsistema is None:
            return
        try:
            subsistema.cerrar()
        except Exception as err:
            logger.debug("%s.cerrar: %s", nombre, err)
        setattr(self, nombre, None)

    def cerrar_sistema(self) -> None:
        """Cierre ordenado: mina lo pendiente, sincroniza y libera recursos."""
        with self._lock_cierre:
            if self._cerrado:
                return
            self._cerrado = True
        self.activa = False
        try:
            self._cola_post.put_nowait(None)
        except Exception:
            pass
        servidor = self.servidor_bks
        if servidor is None:
            return
        with self.lock:
            print("\n" + _AZUL + "=" * 76 + _RESET
                  + "\n  \033[1;37mCONSOLIDANDO ESTADO COGNITIVO...\033[0m")
            try:
                bloque = servidor.minar_transacciones_pendientes()
                if bloque:
                    print(f"  Bloque final consolidado: "
                          f"{_DORADO}{str(bloque.hash_bloque)[:28]}...{_RESET}")
            except Exception as err:
                logger.debug("minado final: %s", err)
            try:
                resultado = servidor.cerrar_sesion_y_subir_ipfs()
                if isinstance(resultado, dict) and resultado.get("ipfs_ok"):
                    print(f"  {_OK}Sincronizacion IPFS completada. "
                          f"Ledger y pesos subidos.{_RESET}")
                else:
                    print(f"  {_AVISO}[H1] IPFS no disponible. Ledger y PSNRL "
                          f"CONSERVADOS en disco (no borrados).{_RESET}")
                    detalle = (resultado.get("ipfs_error", "")
                               if isinstance(resultado, dict) else "")
                    if detalle:
                        print(f"  \033[38;5;241m  Detalle: "
                              f"{str(detalle)[:120]}{_RESET}")
            except Exception as err:
                print(f"  {_AVISO}Cierre: {err}{_RESET}")
            print("\n" + _AZUL + "=" * 76 + _RESET + "\n")
            self.servidor_bks = None
            if callable(limpiar_rutas_stmch):
                try:
                    limpieza = limpiar_rutas_stmch() or {}
                    print(f"  STM_CH cache limpiada: "
                          f"{limpieza.get('borrados', 0)} artefactos.")
                except Exception as err:
                    logger.debug("limpieza STM_CH: %s", err)
            self._cerrar_sub("subsistema_neural")
            self._cerrar_sub("subsistema_evolutivo")


def _registrar_cierre(orquestador: OrquestadorSistemaLucIA) -> None:
    """Registra el cierre en ``atexit`` sin frenar el recolector de basura."""
    try:
        referencia = weakref.WeakMethod(orquestador.cerrar_sistema)
    except TypeError:  # pragma: no cover - implementación exótica
        atexit.register(orquestador.cerrar_sistema)
        return
    atexit.register(lambda: (metodo := referencia()) and metodo())


# ──────────────────────────────────────────────────────────────────────────────
# 7. Punto de entrada.
# ──────────────────────────────────────────────────────────────────────────────
class ContextoOrquestadorLucIA:
    """Gestor de contexto para pruebas, evaluación o invocación programática."""

    def __init__(self) -> None:
        self.orquestador = OrquestadorSistemaLucIA()

    def __enter__(self) -> OrquestadorSistemaLucIA:
        if not self.orquestador.inicializar_subsistemas():
            raise RuntimeError(
                "Fallo durante la inicializacion de subsistemas en LucIA")
        return self.orquestador

    def __exit__(self, exc_type: Any, exc_val: Any, exc_tb: Any) -> bool:
        self.orquestador.cerrar_sistema()
        return False


def main() -> int:
    """Punto de arranque del orquestador unificado."""
    orquestador = OrquestadorSistemaLucIA()
    try:
        if not orquestador.inicializar_subsistemas():
            sys.stderr.write(
                "[mainLCSTM] Fallo durante la inicializacion de subsistemas.\n")
            return 1
        orquestador.ejecutar_bucle_interactivo()
        return 0
    except KeyboardInterrupt:
        print()
        return 0
    except Exception as err:
        sys.stderr.write(f"[mainLCSTM] Error no controlado: {err}\n")
        return 1
    finally:
        orquestador.cerrar_sistema()


if __name__ == "__main__":
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        sys.stderr.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass
    logging.basicConfig(level=logging.CRITICAL)
    raise SystemExit(main())