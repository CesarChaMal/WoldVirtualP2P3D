"""MD_STM_mainLCSTM3.py — Puente entre la conversación y RFPRMN.

Módulo refactorizado y autocompletable:

- ``LucIAPuenteComandos`` (clase principal): traduce una frase en una acción
  de la máquina de comandos RFPRMN (refactorizar, comentar, probar, informe,
  partir, presupuesto o estado), resuelve la ruta y ejecuta.
- Su clase anidada ``Nucleo`` fusiona las funciones de detección, resolución
  de rutas, ejecución y capacidades.

El objetivo no cambia: si RFPRMN no está, ``disponible`` es False y el chat
sigue funcionando. El módulo se puede importar sin efectos secundarios más
allá de preparar ``sys.path``.
"""
from __future__ import annotations

import logging
import re
import sys
from pathlib import Path
from typing import Any, Dict, Final, Optional, Tuple

__all__ = ["LucIAPuenteComandos"]

logger = logging.getLogger("WoldVirtualP2P3D.LucIA.puenteComandos")


# ──────────────────────────────────────────────────────────────────────────────
# 1. Rutas del taller y dependencias opcionales.
# ──────────────────────────────────────────────────────────────────────────────
CURRENT_FILE: Final[Path] = Path(__file__).resolve()

_SRC_BASE: Final[Path] = CURRENT_FILE.parents[3]      # .../src
_CONTRRF_BASE: Final[Path] = CURRENT_FILE.parents[5]  # .../CONTRRF

if str(_SRC_BASE) not in sys.path:
    sys.path.insert(0, str(_SRC_BASE))

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
CONTRRF_DIR: Final[Path] = _ruta_json_o("raices.contrrf", _CONTRRF_BASE)
STM_RFC_DIR: Final[Path] = _ruta_json_o("runtime.stm_rfc", CONTRRF_DIR / "STM_RFC")
RFPRMN_DIR: Final[Path] = _ruta_json_o("runtime.rfprmn_dir",
                                       STM_RFC_DIR / "STM_RFPRMN")

try:
    from .MD_STM_mainLCSTM1 import LucIAConstructor
except ImportError:  # ejecución directa o importación absoluta
    try:
        from STMGNRL.SistemaGeneral.MD_STM_mainLCSTM.MD_STM_mainLCSTM1 import (
            LucIAConstructor,
        )
    except ImportError as _err_constructor:  # pragma: no cover
        LucIAConstructor = None
        logger.debug("LucIAConstructor no disponible: %s", _err_constructor)

try:
    from .._imports import importar_agente
except ImportError:  # ejecución directa o importación absoluta
    try:
        from STMGNRL.SistemaGeneral._imports import importar_agente
    except ImportError as _err_agente:  # pragma: no cover
        importar_agente = None
        logger.debug("importar_agente no disponible: %s", _err_agente)


# ──────────────────────────────────────────────────────────────────────────────
# 2. Constantes de detección y rutas.
# ──────────────────────────────────────────────────────────────────────────────
#: Rutas absolutas (Windows o POSIX) dentro de una frase.
_RE_RUTA_ABS: Final[re.Pattern[str]] = re.compile(
    r"[A-Za-z]:\\[^\s\"']+|/[\w./-]+\.\w+")
#: Nombres de archivo sueltos que se buscan en las bases del taller.
_RE_RUTA_REL: Final[re.Pattern[str]] = re.compile(
    r"[\w.-]+\.(?:py|gd|tscn|tres|md|json|cs)")

#: Carpetas donde buscar un archivo mencionado sin ruta.
BASES_BUSQUEDA: Final[Tuple[Path, ...]] = (
    CONTRRF_DIR,
    SRC_DIR,
    RFPRMN_DIR,
    RFPRMN_DIR / "STM_RFPRMN_stmDL",
    SRC_DIR / "STMGNRL",
    SRC_DIR / "MTBGT" / "WoldVirtualP2P3D",
)


# ──────────────────────────────────────────────────────────────────────────────
# 3. Puente de conversación ↔ RFPRMN.
# ──────────────────────────────────────────────────────────────────────────────
class LucIAPuenteComandos:
    """Puente entre la conversación y la máquina de comandos RFPRMN.

    El chat 3D solo hablaba con Ollama, así que LucIA contestaba «no puedo
    refactorizarme» aunque el agente sí sabe hacerlo. Esta clase:

    - ``capacidades()``: texto con lo que **de verdad** puede hacer, para el
      prompt del sistema.
    - ``detectar(texto)``: convierte una petición en (acción, ruta).
    - ``atender(texto)``: detecta y ejecuta; devuelve el resultado para que el
      modelo responda con datos reales.

    Todo es opcional: si RFPRMN no está, ``disponible`` es False y el servidor
    sigue funcionando como antes.

    La clase anidada :class:`Nucleo` fusiona las funciones de bajo nivel:
    detectar la acción, resolver la ruta, ejecutar, autoanalizar y describir
    capacidades.
    """

    #: Palabras que activan cada acción del agente.
    CLAVES: Final[Tuple[Tuple[str, Tuple[str, ...]], ...]] = (
        ("refactorizar", ("refactoriza", "refactorizar", "refactor",
                          "reescribe", "mejora el codigo")),
        ("comentar", ("comenta", "comentar", "documenta", "documentar")),
        ("probar", ("haz test", "pasa test", "prueba el archivo", "probar",
                    "diagnostica")),
        ("informe", ("informe", "informacion", "deuda", "analiza")),
        ("partir", ("parte ", "partir", "divide", "dividir")),
        ("presupuesto", ("presupuesto", "450")),
        ("estado", ("estado", "como estas", "que tal")),
    )
    #: Acciones que necesitan una ruta para ejecutarse.
    _ACCIONES_RUTA: Final[frozenset] = frozenset(
        {"refactorizar", "comentar", "probar", "informe", "partir"})

    class Nucleo:
        """Funciones fusionadas del puente: detección, rutas y ejecución."""

        @staticmethod
        def ruta_en(texto: str) -> Optional[Path]:
            """Busca una ruta de archivo dentro del texto."""
            for trozo in _RE_RUTA_ABS.findall(texto):
                candidato = Path(trozo.strip("\"'.,;:"))
                if candidato.is_file():
                    return candidato
            for trozo in _RE_RUTA_REL.findall(texto):
                for base in BASES_BUSQUEDA:
                    candidato = base / trozo
                    if candidato.is_file():
                        return candidato
            return None

        @classmethod
        def detectar_claves(cls, texto: str) -> Optional[Dict[str, Any]]:
            """Detecta la acción por ``CLAVES`` y resuelve la ruta."""
            minuscula = texto.lower()
            for nombre, claves in LucIAPuenteComandos.CLAVES:
                if any(clave in minuscula for clave in claves):
                    return {"accion": nombre, "ruta": cls.ruta_en(texto)}
            return None

        @classmethod
        def detectar(cls, texto: str,
                     constructor: Any) -> Optional[Dict[str, Any]]:
            """Acción y ruta a partir de una frase; ``None`` si no hay petición."""
            if constructor is not None:
                try:
                    accion_constructor = constructor.detectar(texto)
                except Exception as error:  # noqa: BLE001 - el constructor es opcional
                    logger.debug("constructor.detectar: %s", error)
                    accion_constructor = None
                if accion_constructor:
                    return {"accion": accion_constructor, "ruta": None,
                            "constructor": True}
            return cls.detectar_claves(texto)

        @staticmethod
        def ejecutar(agente: Any, accion: str,
                     ruta: Optional[Path]) -> Dict[str, Any]:
            """Decide con el agente y, si procede, ejecuta la acción."""
            try:
                aplicado, utilidad, motivo = agente.decidir(accion, ruta)
            except Exception as error:  # noqa: BLE001 - no tumbar el chat
                return {"ok": False, "accion": accion, "motivo": str(error)}
            if not aplicado:
                return {"ok": False, "accion": accion,
                        "utilidad": round(utilidad, 2), "motivo": motivo}
            try:
                hecho = bool(agente.ejecutar(accion, ruta))
            except Exception as error:  # noqa: BLE001
                return {"ok": False, "accion": accion, "motivo": str(error)}
            return {"ok": hecho, "accion": accion, "ruta": str(ruta or ""),
                    "utilidad": round(utilidad, 2), "motivo": motivo}

        @staticmethod
        def autoanalisis(agente: Any) -> Dict[str, Any]:
            """Estado interno del agente, para «refactorízate» sin archivo."""
            try:
                datos = agente.autoanalisis()
            except Exception as error:  # noqa: BLE001
                return {"ok": False, "accion": "autoanalisis",
                        "motivo": str(error)}
            return {"ok": True, "accion": "autoanalisis", "datos": datos}

        @staticmethod
        def capacidades(disponible: bool, constructor: Any) -> str:
            """Lo que LucIA puede hacer, en primera persona, para el prompt."""
            if disponible:
                partes = [
                    "Tienes una máquina de trabajo real (RFPRMN) y puedes usarla:\n"
                    "- refactorizar código con calidad medida y veto si empeora;\n"
                    "- comentar y documentar funciones;\n"
                    "- probar y diagnosticar un archivo;\n"
                    "- generar informes de deuda técnica;\n"
                    "- partir archivos de más de 450 líneas en un subsistema.\n"
                    "Decides con seis señales (déficit, urgencia, éxito previo, "
                    "coste, red y candidato real) y solo aplicas por encima de 0.55 "
                    "de nota. También puedes analizarte a ti misma y refactorizarte "
                    "con `autoanalisis`. Nunca digas que no puedes refactorizar: "
                    "puedes, y si falta el archivo, pide la ruta."
                ]
            else:
                partes = ["No tienes la máquina de refactor cargada ahora mismo; "
                          "puedes conversar, pero no ejecutar cambios."]
            if constructor is not None and constructor.disponible:
                partes.append(constructor.capacidades())
            return "\n".join(partes)

    def __init__(self) -> None:
        self._agente: Any = None
        self._error = ""
        self._capacidades_cache: Optional[str] = None
        self._constructor = (LucIAConstructor()
                             if LucIAConstructor is not None else None)
        try:
            clase = importar_agente() if callable(importar_agente) else None
            # importar_agente() devuelve la CLASE, no una instancia.
            self._agente = clase() if clase is not None else None
            if self._agente is None:
                self._error = "RFPRMN no disponible"
        except Exception as error:  # noqa: BLE001 - RFPRMN es opcional
            self._error = str(error)

    @property
    def disponible(self) -> bool:
        """True si el agente RFPRMN se pudo cargar."""
        return self._agente is not None

    def capacidades(self) -> str:
        """Lo que LucIA puede hacer, en primera persona, para el prompt."""
        if self._capacidades_cache is None:
            self._capacidades_cache = self.Nucleo.capacidades(
                self.disponible, self._constructor)
        return self._capacidades_cache

    def detectar(self, texto: str) -> Optional[Dict[str, Any]]:
        """Acción y ruta a partir de una frase; None si no hay petición."""
        return self.Nucleo.detectar(texto, self._constructor)

    def _ruta_en(self, texto: str) -> Optional[Path]:
        """Busca una ruta de archivo dentro del texto."""
        return self.Nucleo.ruta_en(texto)

    def atender(self, texto: str) -> Optional[Dict[str, Any]]:
        """Ejecuta lo que pide la frase y devuelve un resumen para el modelo."""
        constructor = self._constructor
        if constructor is not None:
            try:
                construccion = constructor.atender(texto)
            except Exception as error:  # noqa: BLE001 - el constructor es opcional
                logger.debug("constructor.atender: %s", error)
                construccion = None
            if construccion is not None:
                return construccion
            # El constructor ya evaluó la frase: no repetir su detección.
            deteccion = self.Nucleo.detectar_claves(texto)
        else:
            deteccion = self.detectar(texto)
        if not deteccion:
            return None
        accion = deteccion["accion"]
        ruta = deteccion.get("ruta")
        if accion == "estado" and ruta is None:
            return None
        if accion in self._ACCIONES_RUTA and ruta is None:
            if accion == "refactorizar":
                # «refactorízate» sin archivo: que se analice a sí misma en
                # vez de responder que no puede.
                return self._autoanalisis()
            return {"ok": False, "accion": accion,
                    "motivo": "necesito la ruta del archivo"}
        if self._agente is None:
            return {"ok": False, "accion": accion,
                    "motivo": self._error or "RFPRMN no disponible"}
        return self.Nucleo.ejecutar(self._agente, accion, ruta)

    def _autoanalisis(self) -> Dict[str, Any]:
        """Su propio estado interno, para cuando le piden refactorizarse."""
        if self._agente is None:
            return {"ok": False, "accion": "autoanalisis",
                    "motivo": self._error or "RFPRMN no disponible"}
        return self.Nucleo.autoanalisis(self._agente)