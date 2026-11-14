"""MD_STM_mainLCSTM2.py — Puente 3D e IPFS de LucIA.

Módulo refactorizado y autocompletable:

- ``LucIAIPFS``: publica y recupera los pesos de LucIA en el nodo IPFS.
- ``LucIAServidor3D`` (clase principal): levanta el servidor TCP de
  ``puente_3D/servidor_lucia.py``, comprueba el puerto y habla su protocolo
  NDJSON. Su clase anidada ``Nucleo`` fusiona las funciones de bajo nivel
  (socket, proceso y protocolo) para que el orquestador solo coordine.

El objetivo no cambia: un turno 3D entra por ``consulta`` y sale por
``token``/``voz``/``fin``; el servidor corre como proceso aparte y no bloquea
al orquestador. El módulo se puede importar sin efectos secundarios más allá
de preparar ``sys.path``.
"""
from __future__ import annotations

import importlib
import json
import logging
import socket
import subprocess
import sys
import time
from pathlib import Path
from typing import Any, Dict, Final, List, Optional, Tuple

__all__ = ["LucIAIPFS", "LucIAServidor3D"]

logger = logging.getLogger("WoldVirtualP2P3D.LucIA.puente3D")


# ──────────────────────────────────────────────────────────────────────────────
# 1. Rutas del taller: ``Rutas.json`` manda, el cálculo por archivo respalda.
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


# ──────────────────────────────────────────────────────────────────────────────
# 2. Pesos de LucIA sobre IPFS.
# ──────────────────────────────────────────────────────────────────────────────
class LucIAIPFS:
    """Conecta el nodo IPFS con los pesos de LucIA.

    El nodo vive en ``STMIPFS/nodo`` (binario, repositorio, daemon, cliente y
    pesos). Esta clase pone encima lo que necesita LucIA:

    - ``asegurar()``: deja el nodo listo (descarga Kubo si falta, inicializa el
      repositorio y arranca el daemon) y, si no hay pesos locales, los recupera.
    - ``publicar()``: empaqueta los pesos (red, agente, seq2seq) y los sube.
    - ``recuperar()``: los baja, verifica el SHA-256 y los deja en ``.modelos``.
    - ``cerrar()``: publica y para el daemon.
    """

    #: Carpeta de pesos que produce STM_RFPRMN.
    DIR_PESOS: Final[Path] = RFPRMN_DIR / ".modelos"
    #: Archivos de estado que viajan en el paquete.
    PATRONES: Final[Tuple[str, ...]] = ("lucia_rn_*.pkl", "agente.json",
                                        "seq2seq.json")

    def __init__(self, arrancar: bool = False) -> None:
        self._nodo: Any = None
        try:
            from STMIPFS.nodo import NodoIPFS

            self._nodo = NodoIPFS(descargar=True)
        except Exception as err:  # noqa: BLE001 - IPFS es opcional
            logger.debug("Nodo IPFS no disponible: %s", err)
        if arrancar:
            self.asegurar()

    @property
    def disponible(self) -> bool:
        """True si el paquete del nodo se pudo cargar."""
        return self._nodo is not None

    def asegurar(self) -> Dict[str, Any]:
        """Deja el nodo listo y recupera los pesos si faltan localmente."""
        if not self._nodo:
            return {"ok": False, "motivo": "nodo IPFS no disponible"}
        resultado = dict(self._nodo.asegurar())
        if resultado.get("ok") and not self.archivos():
            recuperado = self.recuperar()
            resultado["pesos_recuperados"] = recuperado.get("archivos", [])
        return resultado

    def estado(self) -> Dict[str, Any]:
        """Estado del nodo más la carpeta de pesos de LucIA."""
        if not self._nodo:
            return {"disponible": False, "pesos": []}
        datos = dict(self._nodo.estado())
        datos["disponible"] = True
        datos["pesos"] = [archivo.name for archivo in self.archivos()]
        return datos

    def archivos(self) -> List[Path]:
        """Archivos de pesos que existen ahora mismo."""
        if not self.DIR_PESOS.is_dir():
            return []
        encontrados: List[Path] = []
        for patron in self.PATRONES:
            encontrados += sorted(self.DIR_PESOS.glob(patron))
        return encontrados

    def publicar(self) -> Dict[str, Any]:
        """Sube el paquete de pesos al nodo."""
        if not self._nodo:
            return {"ok": False, "motivo": "nodo IPFS no disponible"}
        archivos = self.archivos()
        if not archivos:
            return {"ok": False, "motivo": "no hay pesos en .modelos"}
        datos = dict(self._nodo.publicar(archivos))
        if datos.get("ok"):
            logger.info("Pesos publicados en IPFS: %s", datos.get("cid"))
        return datos

    def recuperar(self) -> Dict[str, Any]:
        """Baja el paquete y lo restaura en la carpeta de pesos de LucIA."""
        if not self._nodo:
            return {"ok": False, "motivo": "nodo IPFS no disponible"}
        return dict(self._nodo.recuperar(destino=self.DIR_PESOS))

    def cerrar(self) -> Dict[str, Any]:
        """Publica los pesos y para el daemon."""
        if not self._nodo:
            return {"ok": False, "motivo": "nodo IPFS no disponible"}
        publicacion = self.publicar()
        parada = self._nodo.parar()
        return {"ok": bool(publicacion.get("ok")), "publicacion": publicacion,
                "daemon": parada}


# ──────────────────────────────────────────────────────────────────────────────
# 3. Puente TCP con el servidor 3D (clase principal + núcleo anidado).
# ──────────────────────────────────────────────────────────────────────────────
class LucIAServidor3D:
    """Levanta y conecta el puente TCP de LucIA 3D: Godot <-> nucleo <-> Ollama.

    El servidor real vive en el proyecto Godot, en
    ``lucia3D/GLTF_lucIA/celebro_3D/puente_3D/servidor_lucia.py``. Esta clase lo
    arranca **como proceso** (no bloquea el orquestador), comprueba el puerto y
    habla su protocolo NDJSON: ``consulta`` entra, ``token``/``voz``/``fin``
    salen.

    Con ``ipfs`` y ``comandos`` conecta la arquitectura entera en un sitio:
    servidor + pesos + maquina de comandos.

    La clase anidada :class:`Nucleo` fusiona las funciones de bajo nivel
    (comprobar puerto, lanzar y esperar el proceso, hablar el protocolo,
    detener y leer el núcleo neuronal).
    """

    #: Ruta del servidor, relativa a la raiz del proyecto Godot.
    RUTA_RELATIVA: Final[Path] = (Path("lucia3D") / "GLTF_lucIA" / "celebro_3D"
                                  / "puente_3D" / "servidor_lucia.py")
    HOST: Final[str] = "127.0.0.1"

    class Nucleo:
        """Funciones fusionadas del puente 3D: socket, proceso y NDJSON."""

        @classmethod
        def puerto_abierto(cls, host: str, puerto: int,
                           timeout: float = 0.6) -> bool:
            """True si el puerto acepta conexiones."""
            try:
                with socket.create_connection((host, puerto), timeout=timeout):
                    return True
            except OSError:
                return False

        @staticmethod
        def lanzar(ruta_servidor: Path, puerto: int, modelo: str, voz: bool,
                   log: Path) -> Tuple[Any, str]:
            """Lanza el servidor como proceso; devuelve ``(proceso, error)``."""
            argumentos = [sys.executable, str(ruta_servidor),
                          "--puerto", str(puerto)]
            if modelo:
                argumentos += ["--modelo", modelo]
            if not voz:
                argumentos += ["--sin-voz"]
            try:
                with open(log, "ab") as salida:
                    proceso = subprocess.Popen(
                        argumentos, cwd=str(ruta_servidor.parent.parent),
                        stdout=salida, stderr=subprocess.STDOUT,
                        stdin=subprocess.DEVNULL)
            except OSError as error:
                return None, f"no se pudo lanzar: {error}"
            return proceso, ""

        @classmethod
        def esperar(cls, host: str, puerto: int, proceso: Any,
                    timeout: float) -> Dict[str, Any]:
            """Espera a que el puerto abra o el proceso muera."""
            limite = time.monotonic() + max(1.0, timeout)
            while time.monotonic() < limite:
                if cls.puerto_abierto(host, puerto):
                    return {"ok": True, "pid": proceso.pid}
                if proceso.poll() is not None:
                    return {"ok": False,
                            "motivo": "el servidor terminó al arrancar"}
                time.sleep(0.4)
            return {"ok": False,
                    "motivo": f"el puerto {puerto} no abrió en {timeout:.0f}s"}

        @classmethod
        def solicitar(cls, host: str, puerto: int, texto: str,
                      timeout: float = 180.0) -> Dict[str, Any]:
            """Un turno completo: envía la consulta y devuelve el evento ``fin``."""
            if not cls.puerto_abierto(host, puerto):
                return {"ok": False, "motivo": "el servidor no está escuchando"}
            peticion = json.dumps({"tipo": "consulta", "texto": texto},
                                  ensure_ascii=False) + "\n"
            try:
                with socket.create_connection((host, puerto),
                                              timeout=timeout) as conexion:
                    conexion.sendall(peticion.encode("utf-8"))
                    lector = conexion.makefile("r", encoding="utf-8",
                                               errors="replace")
                    for linea in lector:
                        limpia = linea.strip()
                        if not limpia:
                            continue
                        try:
                            evento = json.loads(limpia)
                        except ValueError:
                            continue
                        if evento.get("tipo") == "error":
                            return {"ok": False,
                                    "motivo": evento.get("mensaje", "")}
                        if evento.get("tipo") == "fin":
                            return {"ok": True, **evento}
            except (OSError, ValueError) as error:
                return {"ok": False, "motivo": str(error)}
            return {"ok": False, "motivo": "el servidor cerró sin enviar 'fin'"}

        @classmethod
        def detener(cls, proceso: Any, host: str, puerto: int) -> Dict[str, Any]:
            """Termina el proceso (si lo lanzamos nosotros) y comprueba el puerto."""
            if proceso is None:
                return {"ok": not cls.puerto_abierto(host, puerto),
                        "detenido": False}
            try:
                proceso.terminate()
                proceso.wait(timeout=8)
            except (OSError, subprocess.TimeoutExpired):
                try:
                    proceso.kill()
                except OSError:
                    pass
            return {"ok": not cls.puerto_abierto(host, puerto),
                    "detenido": True}

        @staticmethod
        def estado_nucleo(ruta_servidor: Path) -> Dict[str, Any]:
            """Estado del núcleo neuronal de LucIA 3D (50 sinapsis, deriva, sesión)."""
            try:
                base = ruta_servidor.parent.parent  # celebro_3D
                if str(base) not in sys.path:
                    sys.path.insert(0, str(base))
                modulo = importlib.import_module("nucleo.orquestador_lucia")
                return {"ok": True, **modulo.NucleoLucia().estado()}
            except Exception as error:  # noqa: BLE001 - el nucleo es opcional
                return {"ok": False, "motivo": str(error)}

    def __init__(self, raiz_godot: Optional[Path] = None, puerto: int = 8765,
                 modelo: str = "", voz: bool = True, ipfs: Any = None,
                 comandos: Any = None) -> None:
        self.raiz_godot = (Path(raiz_godot) if raiz_godot
                           else SRC_DIR / "MTBGT" / "WoldVirtualP2P3D")
        self.ruta_servidor = self.raiz_godot / self.RUTA_RELATIVA
        self.puerto = int(puerto)
        self.modelo = modelo
        self.voz = voz
        self.ipfs = ipfs
        self.comandos = comandos
        self._proceso: Any = None
        self._log = self.ruta_servidor.parent / "servidor.log"

    @property
    def disponible(self) -> bool:
        """True si el archivo del servidor existe."""
        return self.ruta_servidor.is_file()

    def activo(self, timeout: float = 0.6) -> bool:
        """True si el puerto del servidor acepta conexiones."""
        return self.Nucleo.puerto_abierto(self.HOST, self.puerto, timeout)

    def arrancar(self, esperar: float = 25.0) -> Dict[str, Any]:
        """Lanza el servidor y espera a que abra el puerto."""
        if not self.disponible:
            return {"ok": False, "motivo": f"no existe {self.ruta_servidor}"}
        if self.activo():
            return {"ok": True, "arrancado": False,
                    "motivo": "ya estaba escuchando"}
        proceso, error = self.Nucleo.lanzar(self.ruta_servidor, self.puerto,
                                            self.modelo, self.voz, self._log)
        if proceso is None:
            return {"ok": False, "motivo": error}
        self._proceso = proceso
        resultado = self.Nucleo.esperar(self.HOST, self.puerto, proceso, esperar)
        if not resultado.get("ok"):
            return {**resultado, "log": str(self._log)}
        return {"ok": True, "arrancado": True, "pid": proceso.pid,
                "log": str(self._log)}

    def consultar(self, texto: str, timeout: float = 180.0) -> Dict[str, Any]:
        """Un turno completo: envía la consulta y devuelve el evento ``fin``."""
        return self.Nucleo.solicitar(self.HOST, self.puerto, texto, timeout)

    def parar(self) -> Dict[str, Any]:
        """Termina el proceso del servidor (si lo lanzamos nosotros)."""
        resultado = self.Nucleo.detener(self._proceso, self.HOST, self.puerto)
        self._proceso = None
        return resultado

    def nucleo(self) -> Dict[str, Any]:
        """Estado del núcleo neuronal de LucIA 3D (50 sinapsis, deriva, sesión)."""
        if not self.disponible:
            return {"ok": False, "motivo": "sin servidor"}
        return self.Nucleo.estado_nucleo(self.ruta_servidor)

    def estado(self) -> Dict[str, Any]:
        """Servidor + IPFS + comandos: la arquitectura entera en un dict."""
        datos: Dict[str, Any] = {
            "disponible": self.disponible, "activo": self.activo(),
            "puerto": self.puerto, "ruta": str(self.ruta_servidor),
            "modelo": self.modelo or "(automático)", "log": str(self._log),
        }
        if self.ipfs is not None:
            try:
                datos["ipfs"] = self.ipfs.estado()
            except Exception as error:  # noqa: BLE001 - IPFS es opcional
                logger.debug("estado IPFS: %s", error)
                datos["ipfs"] = {"ok": False, "motivo": str(error)}
        if self.comandos is not None:
            try:
                datos["comandos"] = self.comandos.aprendizaje()
            except Exception as error:  # noqa: BLE001 - comandos es opcional
                logger.debug("estado comandos: %s", error)
                datos["comandos"] = {"ok": False, "motivo": str(error)}
        return datos

    def cerrar(self) -> Dict[str, Any]:
        """Para el servidor y, si hay nodo IPFS, publica los pesos."""
        datos: Dict[str, Any] = {"servidor": self.parar()}
        if self.ipfs is not None:
            try:
                datos["ipfs"] = self.ipfs.cerrar()
            except Exception as error:  # noqa: BLE001 - IPFS es opcional
                logger.debug("cierre IPFS: %s", error)
                datos["ipfs"] = {"ok": False, "motivo": str(error)}
        return datos