'\nmainLCSTM.py - Orquestador Central del Sistema Cognitivo LucIA (WoldVirtualP2P3D 2026)\n=====================================================================================\nCoordinacion maestro: Blockchain BKSVCB, Sesion P2P, IAFREE ($0.00), STYLOS y RPLC.\n'

from __future__ import annotations
import atexit
import json
import logging
import os
import signal
import sys
import threading
import time
import urllib.error
import urllib.request
from pathlib import Path

try:
    from STM_CH.STM_herramientas.rutas_json import obtener as _obtener_ruta_json
except Exception:  # pragma: no cover
    _obtener_ruta_json = None


def _ruta_json_o(clave: str, por_defecto: Path) -> Path:
    if _obtener_ruta_json is None:
        return por_defecto
    try:
        return _obtener_ruta_json(clave)
    except Exception:
        return por_defecto
from typing import Any, Callable, Dict, Final, List, Optional, Tuple, Union

CURRENT_FILE: Final[Path] = Path(__file__).resolve()
GNRL_DIR: Final[Path] = CURRENT_FILE.parent
SRC_DIR: Final[Path] = _ruta_json_o("raices.src", GNRL_DIR.parent.parent)
ROOT_DIR: Final[Path] = _ruta_json_o("raices.tllcd", SRC_DIR.parent)
CONTRRF_DIR: Final[Path] = _ruta_json_o("raices.contrrf", CURRENT_FILE.parents[4])
ENV_FILE: Final[Path] = next((c for c in (GNRL_DIR / ".env", SRC_DIR / ".env", ROOT_DIR / ".env") if c.exists()), SRC_DIR / ".env")
CELEBRO_DIR: Final[Path] = SRC_DIR / "STM_CH"
PSNRL_DIR: Final[Path] = CELEBRO_DIR / "PSNRL"
CMFG_DIR: Final[Path] = SRC_DIR
SBSTM_DIR: Final[Path] = _ruta_json_o("subsistemas.SBSTM.raiz", SRC_DIR / "SBSTM")
STM_RFC_DIR: Final[Path] = _ruta_json_o("runtime.stm_rfc", CONTRRF_DIR / "STM_RFC")
for _p in (GNRL_DIR, SRC_DIR, STM_RFC_DIR, ROOT_DIR, CONTRRF_DIR):
    if _p.exists() and str(_p) not in sys.path:
        sys.path.insert(0, str(_p))

try:
    from ._imports import (
        init_stmch_rutas,
        RFPRMN_DISPONIBLE as _RFPRMN_DISPONIBLE,
        modulo_rfprmn as _RFPRMN,
        ClienteIAFree,
        get_cliente_iafree,
        ColoresLucIA,
        EstiloTerminalLucIA,
        Glifos,
        badge_turno,
        banner_bienvenida,
        formatear_respuesta_lucia,
        panel_ayuda_comandos,
        RPLC_DISPONIBLE as _RPLC_DISPONIBLE,
        get_procesador_rplc,
        reprocesar_con_metricas,
        VOZ_DISPONIBLE as _VOZ_DISPONIBLE,
        VOZ_MOTIVO as _VOZ_MOTIVO,
        hablar_voz as _hablar_voz,
        cancelar_voz as _cancelar_voz,
        prosodia_voz as _prosodia_voz,
        SelectorModelos,
        GestorHRTLucIA,
        GestorJSONLucIA,
        get_gestor_ialocal,
        WATCHDOG_DISPONIBLE as _WATCHDOG_DISPONIBLE,
        arrancar_watchdog as _arrancar_watchdog,
        MEMORIA_DISPONIBLE as _MEMORIA_DISPONIBLE,
        MemoriaSesion,
        get_memoria,
        get_openrouter_vault_key,
        confirmar_vault_openrouter,
        registrar_key_vault_openrouter,
        obtener_componentes_blockchain,
        obtener_conversor_pesos_blockchain,
        instanciar_sesion_p2p,
        limpiar_rutas_stmch,
        importar_agente,
        importar_constructor,
    )
except (ImportError, ValueError):
    from STMGNRL.SistemaGeneral._imports import (
        init_stmch_rutas,
        RFPRMN_DISPONIBLE as _RFPRMN_DISPONIBLE,
        modulo_rfprmn as _RFPRMN,
        ClienteIAFree,
        get_cliente_iafree,
        ColoresLucIA,
        EstiloTerminalLucIA,
        Glifos,
        badge_turno,
        banner_bienvenida,
        formatear_respuesta_lucia,
        panel_ayuda_comandos,
        RPLC_DISPONIBLE as _RPLC_DISPONIBLE,
        get_procesador_rplc,
        reprocesar_con_metricas,
        VOZ_DISPONIBLE as _VOZ_DISPONIBLE,
        VOZ_MOTIVO as _VOZ_MOTIVO,
        hablar_voz as _hablar_voz,
        cancelar_voz as _cancelar_voz,
        prosodia_voz as _prosodia_voz,
        SelectorModelos,
        GestorHRTLucIA,
        GestorJSONLucIA,
        get_gestor_ialocal,
        WATCHDOG_DISPONIBLE as _WATCHDOG_DISPONIBLE,
        arrancar_watchdog as _arrancar_watchdog,
        MEMORIA_DISPONIBLE as _MEMORIA_DISPONIBLE,
        MemoriaSesion,
        get_memoria,
        get_openrouter_vault_key,
        confirmar_vault_openrouter,
        registrar_key_vault_openrouter,
        obtener_componentes_blockchain,
        obtener_conversor_pesos_blockchain,
        instanciar_sesion_p2p,
        limpiar_rutas_stmch,
        importar_agente,
        importar_constructor,
    )

init_stmch_rutas()


_COMANDOS_RFPRMN: Final[Dict[str, str]] = {"crear carpeta": "comando_crear_carpeta", "crear archivo": "comando_crear_archivo", "refactorizar": "comando_refactorizar", "actualizar": "actualizar", "enviar": "comando_enviar", "ia local": "comando_ia_local", "cerrar": "cerrar"}

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")
except Exception:
    pass

logging.basicConfig(level=logging.CRITICAL)
for _log_name in ("", "WoldVirtualP2P3D", "LC", "urllib3", "ENRN", "SLRN", "RNP", "httpx"):
    logging.getLogger(_log_name).setLevel(logging.CRITICAL)


# ---------------------------------------------------------------------------
# Orquestador y entorno seguro: viven en MD_STM_mainLCSTM/MD_STM_mainLCSTM1.py
# Se importan para mantener intacta la API publica de mainLCSTM.
# ---------------------------------------------------------------------------
try:
    from .MD_STM_mainLCSTM.MD_STM_mainLCSTM1 import (
        GestorEntornoSeguro,
        LucIAConstructor,
        OrquestadorSistemaLucIA,
    )
except ImportError:  # ejecucion directa: python mainLCSTM.py
    from STMGNRL.SistemaGeneral.MD_STM_mainLCSTM.MD_STM_mainLCSTM1 import (
        GestorEntornoSeguro,
        LucIAConstructor,
        OrquestadorSistemaLucIA,
    )

# ---------------------------------------------------------------------------
# Puente 3D e IPFS: viven en MD_STM_mainLCSTM/MD_STM_mainLCSTM2.py
# ---------------------------------------------------------------------------
try:
    from .MD_STM_mainLCSTM.MD_STM_mainLCSTM2 import (
        LucIAIPFS,
        LucIAServidor3D,
    )
except ImportError:  # ejecucion directa: python mainLCSTM.py
    from STMGNRL.SistemaGeneral.MD_STM_mainLCSTM.MD_STM_mainLCSTM2 import (
        LucIAIPFS,
        LucIAServidor3D,
    )

# ---------------------------------------------------------------------------
# Puente de comandos: vive en MD_STM_mainLCSTM/MD_STM_mainLCSTM3.py
# ---------------------------------------------------------------------------
try:
    from .MD_STM_mainLCSTM.MD_STM_mainLCSTM3 import LucIAPuenteComandos
except ImportError:  # ejecucion directa: python mainLCSTM.py
    from STMGNRL.SistemaGeneral.MD_STM_mainLCSTM.MD_STM_mainLCSTM3 import (
        LucIAPuenteComandos,
    )


class ContextoOrquestadorLucIA:
    """Gestor de contexto para pruebas, evaluacion o invocacion programatica."""
    def __init__(self) -> None:
        self.orquestador = OrquestadorSistemaLucIA()
    def __enter__(self) -> OrquestadorSistemaLucIA:
        if not self.orquestador.inicializar_subsistemas():
            raise RuntimeError("Fallo durante la inicializacion de subsistemas en LucIA")
        return self.orquestador
    def __exit__(self, exc_type: Any, exc_val: Any, exc_tb: Any) -> bool:
        self.orquestador.cerrar_sistema()
        return False
logger = logging.getLogger("WoldVirtualP2P3D.STMGNRL.LucIAComandos")


class LucIAComandos:
    """LucIA usando su propia maquina de comandos (RFPRMN) sin humano.

    Cada comando se ejecuta solo si la decision matematica del agente lo
    autoriza (`utilidad >= umbral`), calculada con el deficit de deuda, la
    urgencia, el exito previo y el coste. El cierre tambien se decide asi: no
    se cierra mientras haya un flujo abierto o trabajo pendiente.
    """

    def __init__(self, umbral: float = 0.55) -> None:
        self._agente = None
        if not _RFPRMN_DISPONIBLE or _RFPRMN is None:
            logger.debug("RFPRMN no disponible: LucIA se queda sin comandos.")
            return
        clase = importar_agente() if importar_agente else None
        if clase is None:
            logger.debug("AgenteRFPRMN no importable.")
            return
        try:
            self._agente = clase(umbral=umbral)
        except Exception as err:  # noqa: BLE001 - nunca romper el arranque
            logger.debug("No se pudo crear el agente: %s", err)

    @property
    def disponible(self) -> bool:
        """True si LucIA puede lanzar comandos."""
        return self._agente is not None

    def __getattr__(self, nombre: str) -> Any:
        """Expone todos los comandos del agente como metodos."""
        agente = self.__dict__.get("_agente")
        if agente is None or not hasattr(agente, nombre):
            raise AttributeError(nombre)
        return getattr(agente, nombre)

    def comando(self, nombre: str, ruta: Optional[str] = None) -> bool:
        """Ejecuta un comando por nombre si la utilidad lo permite."""
        if not self._agente:
            return False
        return bool(self._agente.ejecutar(nombre, ruta))

    def utilidad(self, nombre: str, ruta: Optional[str] = None) -> float:
        """Utilidad calculada de un comando (0-1) sobre ese archivo."""
        if not self._agente:
            return 0.0
        return float(self._agente.utilidad(nombre, self._agente.analizar(ruta)
                                           if ruta else {})[0])

    def cerrar(self) -> bool:
        """Cierra la maquina de comandos solo si la decision lo autoriza."""
        if not self._agente:
            return False
        autorizado, valor, motivo = self._agente.desea_cerrar()
        logger.info("LucIA cierre: %s (%.3f, %s)",
                    "si" if autorizado else "no", valor, motivo)
        return bool(self._agente.cerrar_si_procede())

    def ciclo(self, rutas: List[str]) -> Dict[str, Any]:
        """Ciclo autonomo: salud, probar, partir/refactorizar/comentar, informe."""
        if not self._agente:
            return {"acciones": [], "cerrado": False, "motivo": "RFPRMN no disponible"}
        return dict(self._agente.ciclo(rutas))


class LucIAPlanificador:
    """Planifica los comandos con dos señales mas: su red y el tiempo.

    - **Prediccion de la red** (quinta senal de la utilidad): cuanto respalda
      LucIA una accion segun lo que ha aprendido (memoria del seq2seq y pasos de
      su red, encogida hacia 0.5 cuando aun sabe poco).
    - **Presupuesto de tiempo**: cada accion lleva un coste estimado en segundos
      y la sesion tiene un maximo; lo que no cabe se descarta y el ciclo se
      corta cuando se agota, en vez de quedarse minutos bloqueado.
    """

    def __init__(self, presupuesto_segundos: float = 300.0,
                 umbral: float = 0.55) -> None:
        self._agente = None
        clase = importar_agente() if importar_agente else None
        if clase is not None:
            try:
                self._agente = clase(umbral=umbral,
                                     presupuesto_segundos=presupuesto_segundos)
            except Exception as err:  # noqa: BLE001 - nunca romper el arranque
                logger.debug("No se pudo crear el planificador: %s", err)

    @property
    def disponible(self) -> bool:
        """True si hay agente con el que planificar."""
        return self._agente is not None

    def senal_red(self, ruta: str) -> float:
        """Prediccion de LucIA para ese archivo (0-1; 0.5 si no hay agente)."""
        if not self._agente:
            return 0.5
        try:
            contenido = Path(ruta).read_text(encoding="utf-8-sig")
        except OSError:
            contenido = ""
        return float(self._agente.senal_red(contenido))

    def estimar(self, comando: str, ruta: Optional[str] = None) -> float:
        """Segundos estimados del comando sobre ese archivo."""
        if not self._agente:
            return 0.0
        analisis = self._agente.analizar(ruta) if ruta else {}
        return float(self._agente.estimar_tiempo(comando, analisis))

    def restante(self) -> float:
        """Segundos que quedan del presupuesto de la sesion."""
        return float(self._agente.restante()) if self._agente else 0.0

    def exito(self, comando: str) -> float:
        """Calidad media conseguida por ese comando (0.5 si aun no hay datos).

        No cuenta aciertos: cada intento aporta la nota que dio la red de LucIA
        al aplicarlo, asi que una accion que sale a medias pesa menos.
        """
        return float(self._agente.exito_previo(comando)) if self._agente else 0.5

    def aprendizaje(self) -> Dict[str, Dict[str, float]]:
        """Lo aprendido por comando: calidad media e intentos de la sesion."""
        if not self._agente:
            return {}
        historial = getattr(self._agente, "historial", {})
        return {
            accion: {
                "calidad_media": round(self._agente.exito_previo(accion), 3),
                "intentos": int(historial.get(accion, [0.0, 0])[1]),
            }
            for accion in sorted(historial)
        }

    def plan(self, rutas: List[str],
             acciones: Tuple[str, ...] = ("partir", "refactorizar",
                                          "comentar", "informe")) -> List[Dict[str, Any]]:
        """Ordena las acciones por **utilidad por segundo** y marca las que caben."""
        if not self._agente:
            return []
        filas: List[Dict[str, Any]] = []
        for ruta in rutas:
            analisis = self._agente.analizar(ruta)
            for accion in acciones:
                valor, senales = self._agente.utilidad(accion, analisis)
                segundos = self._agente.estimar_tiempo(accion, analisis)
                filas.append({
                    "ruta": str(ruta), "accion": accion,
                    "utilidad": valor, "segundos": segundos,
                    "eficiencia": round(valor / max(segundos, 1.0), 4),
                    "red": senales["red"],
                    "cabe": segundos <= self._agente.restante(),
                })
        return sorted(filas, key=lambda fila: (not fila["cabe"],
                                               -fila["eficiencia"]))

    def ejecutar(self, rutas: List[str]) -> Dict[str, Any]:
        """Ciclo autonomo con presupuesto de tiempo."""
        if not self._agente:
            return {"acciones": [], "cerrado": False, "motivo": "sin agente"}
        return dict(self._agente.ciclo(rutas))

    def cerrar(self) -> bool:
        """Cierra solo si la decision matematica lo autoriza."""
        if not self._agente:
            return False
        return bool(self._agente.cerrar_si_procede())


def obtener_diagnostico_orquestador() -> Dict[str, Any]:
    """Genera un reporte estructural para validar la salud de todo el stack."""
    cf = get_cliente_iafree() if get_cliente_iafree else None
    return {
        "orquestador_version": "2026.5.0",
        "iafree_activo": cf.esta_autenticado() if cf else False,
        "modelos_gratuitos_total": len(cf.gestor.listar_modelos()) if cf else 0,
        "modelo_predeterminado": cf.gestor.obtener_modelo_activo()["id"] if cf else "N/A",
        "costo_acumulado": cf.obtener_metricas_consumo()["costo_acumulado_usd"] if cf else 0.0,
        "timestamp": time.time(),
    }
def main() -> int:
    """Punto de arranque del orquestador unificado mainLCSTM."""
    orquestador = OrquestadorSistemaLucIA()
    try:
        if not orquestador.inicializar_subsistemas():
            sys.stderr.write("[mainLCSTM] Fallo durante la inicializacion de subsistemas.\n")
            return 1
        orquestador.ejecutar_bucle_interactivo()
        return 0
    except Exception as err:
        sys.stderr.write(f"[mainLCSTM] Error no controlado: {err}\n")
        return 1
    finally:
        orquestador.cerrar_sistema()
if __name__ == "__main__":
    sys.exit(main())