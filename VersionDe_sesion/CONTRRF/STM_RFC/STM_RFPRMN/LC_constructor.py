r"""LC_constructor — primer comando automático de LucIA (``crear carpeta``).

Recibe de la máquina de comandos una orden como::

    Ruta donde crear: E:\...\LCtrabajando
    Nombre de la carpeta: src

y la ejecuta pasando por la red neuronal completa de ``LCBS_NEURONAL``
(ENRN -> RF_EN y salud de las familias) y por la fachada ``RFPRMN.py``.
Acepta texto etiquetado (una o varias líneas), dict o JSON; la CLI responde
en texto legible, no en JSON.

Acciones disponibles:
- ``crear base``          : secuencia por defecto: primero ``crear carpeta``
  (con su cálculo neuronal) y después ``crear archivo``.
- ``crear todo``          : secuencia completa: ``crear base`` + ``RedNeuronal``
  + ``MTBGT``.
- ``crear carpeta``       : ``LCtrabajando/src`` con **toda** la jerarquía de
  carpetas detectada en ``TLLDCD/src`` y su cálculo neuronal.
- ``crear red neuronal``  : ``LCtrabajando/src/LC_STM/RedNeuronal`` y sus
  6 familias.  Se invoca con ``python LC_constructor.py red``.
- ``crear mtbgt``         : ``LCtrabajando/src/MTBGT`` con sus carpetas;
  los archivos quedan para una fase posterior.  Se invoca con
  ``python LC_constructor.py mtbgt``.
- ``crear archivo``       : archivo suelto (``Ruta`` + ``Nombre`` + ``Lenguaje``)
  o, sin nombre, la jerarquía de código de ``TLLDCD/src`` recreada vacía bajo
  ``LCtrabajando/src``. Con ``Lenguaje`` y sin ``Nombre`` filtra la jerarquía
  a esa extensión (por ejemplo ``csharp`` crea solo los ``.cs``).
"""
from __future__ import annotations

import os
import sys
from collections.abc import Iterable, Mapping
from pathlib import Path
from typing import Any

__version__ = "0.9.1"

_DIR_MODULO = Path(__file__).resolve().parent
if str(_DIR_MODULO) not in sys.path:
    sys.path.insert(0, str(_DIR_MODULO))

import RFPRMN as _RFPRMN  # noqa: E402
from STM_LC_CONSTRUCTOR_MD import (  # noqa: E402
    COMANDO_ARCHIVO,
    COMANDO_CARPETA,
    COMANDO_MTBGT,
    COMANDO_RED,
    COMANDO_TODO,
    NOMBRE_MTBGT_PREDETERMINADO,
    NOMBRE_PREDETERMINADO,
    NOMBRE_RED_PREDETERMINADO,
    SUBCARPETAS_MTBGT,
    SUBCARPETAS_RED,
    SUBCARPETAS_SRC,
    SUBCARPETAS_SRC_BASE,
    ConstructorLucIA,
    cargar_lcbs_neuronal,
    cargar_modulo_red,
    descubrir_jerarquia_src,
    descubrir_subcarpetas_src,
    localizar_lcbs_neuronal,
    localizar_red_neuronal,
)
from STM_LC_CONSTRUCTOR_MD import (
    RUTA_FUENTE_PREDETERMINADA as _RUTA_FUENTE_MD1,
)
from STM_LC_CONSTRUCTOR_MD import (
    interpretar_orden as _interpretar_orden,
)

RFPRMN = _RFPRMN.RFPRMN


def _resolver_contrrf() -> Path:
    """Localiza ``CONTRRF`` a partir del módulo o del entorno."""
    entorno = os.environ.get("LC_CONTRRF", "").strip()
    if entorno:
        candidata = Path(entorno).expanduser()
        if (candidata / "CONTRUCCION").is_dir():
            return candidata
    for base in (_DIR_MODULO, *_DIR_MODULO.parents):
        for relativa in (Path("CONTRRF"),
                         Path("VersionDe_sesion") / "CONTRRF"):
            candidata = base / relativa
            if (candidata / "CONTRUCCION").is_dir():
                return candidata
    return _DIR_MODULO.parents[1]


def _resolver_ruta_trabajo() -> Path:
    """Directorio de salida: ``CONTRRF/CONTRUCCION/LCtrabajando``."""
    entorno = os.environ.get("LC_TRABAJO", "").strip()
    if entorno:
        return Path(entorno).expanduser()
    return _resolver_contrrf() / "CONTRUCCION" / "LCtrabajando"


_RUTA_TRABAJO = _resolver_ruta_trabajo()

#: Ruta que usa el comando cuando no se indica. El nombre y las subcarpetas
#: viven en ``STM_MD2`` junto al resto de valores por comando.
RUTA_PREDETERMINADA = str(_RUTA_TRABAJO)

#: Segunda acción: ``LCtrabajando/src/LC_STM/RedNeuronal`` y sus familias.
RUTA_RED_PREDETERMINADA = str(_RUTA_TRABAJO / "src" / "LC_STM")

#: Tercera acción: ``LCtrabajando/src/MTBGT`` solo con carpetas por ahora.
RUTA_MTBGT_PREDETERMINADA = str(_RUTA_TRABAJO / "src" / "MTBGT")
#: Sin archivos de momento: el ``project.godot`` queda para una fase posterior.
ARCHIVOS_MTBGT: tuple[str, ...] = ()

#: Modo limpio: crea solo carpetas, pero conserva el cálculo de LCBS_NEURONAL.
#: Poner False solo si se quiere evitar por completo la carga de la red.
USAR_RED_NEURONAL = True

#: Fuente oficial de la estructura de carpetas.
RUTA_FUENTE_PREDETERMINADA = str(_RUTA_FUENTE_MD1)


def _rutas_predeterminadas() -> dict[str, str]:
    """Rutas actuales de cada comando (respetan ``runtime.lc_trabajando``)."""
    return {
        COMANDO_CARPETA: RUTA_PREDETERMINADA,
        COMANDO_RED: RUTA_RED_PREDETERMINADA,
        COMANDO_MTBGT: RUTA_MTBGT_PREDETERMINADA,
        COMANDO_ARCHIVO: RUTA_PREDETERMINADA,
        COMANDO_TODO: RUTA_PREDETERMINADA,
    }


def interpretar_orden(orden: str | Mapping[str, Any]) -> dict[str, Any]:
    """Convierte texto, JSON o dict de la máquina en una orden normalizada.

    Delega en ``STM_MD2`` con las rutas actuales, de modo que respeta
    ``runtime.lc_trabajando`` sin duplicar las reglas del parser.
    """
    return _interpretar_orden(orden, _rutas_predeterminadas())


_CONSTRUCTOR: ConstructorLucIA | None = None


def obtener_constructor() -> ConstructorLucIA:
    """Constructor compartido (carga perezosa de la red y de RFPRMN).

    Se le pasa el proveedor de rutas para que cada ejecución respete
    ``runtime.lc_trabajando`` sin recrear la instancia.
    """
    global _CONSTRUCTOR
    if _CONSTRUCTOR is None:
        _CONSTRUCTOR = ConstructorLucIA(
            rutas=_rutas_predeterminadas, usar_red=USAR_RED_NEURONAL)
    return _CONSTRUCTOR


def ejecutar_orden(orden: str | Mapping[str, Any]) -> dict[str, Any]:
    """Atajo: interpreta y ejecuta una orden con el constructor compartido."""
    return obtener_constructor().ejecutar(orden)


def crear_carpeta(ruta: str | Path = "",
                  nombre: str = NOMBRE_PREDETERMINADO,
                  subcarpetas: Iterable[str] | None = None) -> dict[str, Any]:
    """Atajo programático; sin ruta usa ``RUTA_PREDETERMINADA``."""
    return obtener_constructor().crear_carpeta(ruta, nombre, subcarpetas)


def comando_crear_carpeta(ruta: str | Path = "",
                          nombre: str = NOMBRE_PREDETERMINADO,
                          subcarpetas: Iterable[str] | None = None) -> dict[str, Any]:
    """Alias con la ruta y el nombre predeterminados."""
    return crear_carpeta(ruta, nombre, subcarpetas)


def crear_red_neuronal(ruta: str | Path = "",
                       nombre: str = NOMBRE_RED_PREDETERMINADO,
                       subcarpetas: Iterable[str] | None = None) -> dict[str, Any]:
    """Atajo de la segunda acción; sin ruta usa ``RUTA_RED_PREDETERMINADA``."""
    return obtener_constructor().crear_red_neuronal(ruta, nombre, subcarpetas)


def comando_crear_red_neuronal(
    ruta: str | Path = "", nombre: str = NOMBRE_RED_PREDETERMINADO,
    subcarpetas: Iterable[str] | None = None,
) -> dict[str, Any]:
    """Alias de la segunda acción con sus valores predeterminados."""
    return crear_red_neuronal(ruta, nombre, subcarpetas)


def crear_mtbgt(ruta: str | Path = "",
                subcarpetas: Iterable[str] | None = None,
                archivos: Iterable[str] | None = None) -> dict[str, Any]:
    """Atajo de la tercera acción; crea ``MTBGT`` solo con carpetas."""
    return obtener_constructor().crear_mtbgt(ruta, subcarpetas, archivos)


def comando_crear_mtbgt(
    ruta: str | Path = "", subcarpetas: Iterable[str] | None = None,
    archivos: Iterable[str] | None = None,
) -> dict[str, Any]:
    """Alias de la tercera acción con sus valores predeterminados."""
    return crear_mtbgt(ruta, subcarpetas, archivos)


def crear_archivo(ruta: str | Path = "", nombre: str = "",
                  lenguaje: str = "",
                  archivos: Iterable[str] | None = None) -> dict[str, Any]:
    """Atajo de la cuarta acción; sin nombre recrea la jerarquía de código."""
    return obtener_constructor().crear_archivo(ruta, nombre, lenguaje, archivos)


def comando_crear_archivo(
    ruta: str | Path = "", nombre: str = "", lenguaje: str = "",
    archivos: Iterable[str] | None = None,
) -> dict[str, Any]:
    """Alias de la cuarta acción con sus valores predeterminados."""
    return crear_archivo(ruta, nombre, lenguaje, archivos)


def lenguajes_fuente() -> dict[str, int]:
    """Lenguajes de programación detectados en ``TLLDCD/src``."""
    from STM_LC_CONSTRUCTOR_MD import ConstructorArchivos

    return ConstructorArchivos().lenguajes


def archivos_fuente() -> tuple[str, ...]:
    """Jerarquía de archivos de código detectada en ``TLLDCD/src``."""
    from STM_LC_CONSTRUCTOR_MD import ConstructorArchivos

    return ConstructorArchivos().archivos_fuente()


def crear_todo() -> dict[str, Any]:
    """Ejecuta la secuencia completa: ``src``, ``RedNeuronal`` y ``MTBGT``."""
    return obtener_constructor().crear_todo()


def comando_crear_todo() -> dict[str, Any]:
    """Alias de la secuencia completa."""
    return crear_todo()


def crear_base() -> dict[str, Any]:
    """Secuencia base: ``src`` con su red y después los archivos vacíos."""
    return obtener_constructor().crear_base()


def comando_crear_base() -> dict[str, Any]:
    """Alias de la secuencia base."""
    return crear_base()


def formatear_resultado(resultado: Mapping[str, Any]) -> str:
    """Devuelve el informe de terminal (texto legible, sin JSON)."""
    acciones = resultado.get("acciones")
    if acciones:
        partes = [formatear_resultado(accion) for accion in acciones]
        estado = "OK" if resultado.get("ok") else "CON ERRORES"
        partes.append(
            f"Resultado global: {estado} · {resultado.get('total', 0)} elementos")
        return "\n".join(partes)
    ancho = 74
    comando = resultado.get("comando", COMANDO_CARPETA)
    es_archivo = comando == COMANDO_ARCHIVO
    etiqueta = "Archivo" if es_archivo else "Carpeta"
    lineas = ["=" * ancho, f" LC_constructor · comando {comando}", "=" * ancho]
    if not resultado.get("ok"):
        lineas.append(f"ERROR: {resultado.get('error', 'desconocido')}")
        if resultado.get("ruta"):
            lineas.append(f"Ruta: {resultado['ruta']}")
        if resultado.get("nombre"):
            lineas.append(f"{etiqueta}: {resultado['nombre']}")
        lineas.append("=" * ancho)
        return "\n".join(lineas)
    red = resultado.get("red_general") or {}
    entrada = resultado.get("neurona_entrada") or {}
    diagnostico = red.get("diagnostico") or {}
    lineas.extend([
        f"Ruta           : {resultado.get('ruta_mtbgt') or resultado.get('ruta', '')}",
        f"{etiqueta:<14} : {resultado.get('nombre', '')}",
    ])
    if es_archivo and resultado.get("lenguaje"):
        lineas.append(f"Lenguaje       : {resultado['lenguaje']}")
    if red and not red.get("omitida"):
        lineas.append(
            f"Red general    : LCBS_NEURONAL v{red.get('version', '?')}")
        salud = red.get("salud") or {}
        if salud:
            estados = " | ".join(
                f"{familia}={'OK' if ok else 'ERR'}"
                for familia, ok in salud.items())
            lineas.append(f"Familias       : {estados}")
        if red.get("ok"):
            lineas.extend([
                f"Modo           : {diagnostico.get('modo', '?')} "
                f"({diagnostico.get('neurona_enrn', '?')} -> "
                f"{diagnostico.get('neurona_rfen', '?')})",
                f"Accion         : {red.get('accion', [])}",
                f"Latencia       : {diagnostico.get('latencia_ms', 0):.3f} ms",
                f"Normas         : estado={diagnostico.get('norma_estado', 0):.4f} "
                f"latente={diagnostico.get('norma_latente', 0):.4f} "
                f"accion={diagnostico.get('norma_accion', 0):.4f}",
            ])
        else:
            lineas.append(f"Red general    : ERROR {red.get('error', '')}")
        if entrada:
            lineas.append(
                f"Neurona entrada: impulso={entrada.get('impulso', '?')} "
                f"entropia={entrada.get('entropia', '?')}")
    if es_archivo:
        lineas.append(f"Archivos       : {resultado.get('total', 0)} vacíos")
        lenguajes = resultado.get("lenguajes") or {}
        if lenguajes:
            detalle = ", ".join(f"{extension}={numero}"
                                for extension, numero in lenguajes.items())
            lineas.append(f"Lenguajes      : {detalle}")
    else:
        lineas.append(f"Jerarquia      : {resultado.get('total', 0)} carpetas")
    archivos = resultado.get("archivos") or []
    if archivos:
        nombres = [Path(a).name for a in archivos]
        muestra = ", ".join(nombres[:12])
        if len(nombres) > 12:
            muestra += f" (+{len(nombres) - 12} más)"
        lineas.append(f"Archivos       : {muestra}")
    for paso in resultado.get("pasos", []):
        lineas.append(
            f"  {paso.get('paso', '?'):>2}. "
            f"{paso.get('archivo', paso.get('carpeta', ''))}")
    if resultado.get("advertencia"):
        lineas.append(f"AVISO          : {resultado['advertencia']}")
    if red:
        estado = "OK" if red.get("ok") else "OK (creación; red general con error)"
    else:
        estado = "OK" if resultado.get("ok") else "CON ERRORES"
    lineas.extend([f"Estado         : {estado}", "=" * ancho])
    return "\n".join(lineas)


def main(argv: list[str] | None = None) -> int:
    """Punto de entrada CLI: por argumentos o con los valores predeterminados."""
    argumentos = list(sys.argv[1:] if argv is None else argv)
    pausa = "--pausa" in argumentos
    argumentos = [argumento for argumento in argumentos if argumento != "--pausa"]
    if argumentos:
        resultado = ejecutar_orden(" ".join(argumentos))
    else:
        resultado = crear_base()
    print(formatear_resultado(resultado))
    if pausa or (not argumentos and sys.stdin.isatty()):
        try:
            input("\nPulsa Enter para cerrar...")
        except EOFError:
            pass
    return 0 if resultado.get("ok") else 1


if __name__ == "__main__":
    raise SystemExit(main())


__all__ = [
    "ARCHIVOS_MTBGT",
    "COMANDO_ARCHIVO",
    "COMANDO_CARPETA",
    "COMANDO_MTBGT",
    "COMANDO_RED",
    "COMANDO_TODO",
    "NOMBRE_MTBGT_PREDETERMINADO",
    "NOMBRE_PREDETERMINADO",
    "NOMBRE_RED_PREDETERMINADO",
    "RFPRMN",
    "RUTA_FUENTE_PREDETERMINADA",
    "RUTA_MTBGT_PREDETERMINADA",
    "RUTA_PREDETERMINADA",
    "RUTA_RED_PREDETERMINADA",
    "SUBCARPETAS_MTBGT",
    "SUBCARPETAS_RED",
    "SUBCARPETAS_SRC",
    "SUBCARPETAS_SRC_BASE",
    "USAR_RED_NEURONAL",
    "ConstructorLucIA",
    "__version__",
    "archivos_fuente",
    "cargar_lcbs_neuronal",
    "cargar_modulo_red",
    "comando_crear_archivo",
    "comando_crear_base",
    "comando_crear_carpeta",
    "comando_crear_mtbgt",
    "comando_crear_red_neuronal",
    "comando_crear_todo",
    "crear_archivo",
    "crear_base",
    "crear_carpeta",
    "crear_mtbgt",
    "crear_red_neuronal",
    "crear_todo",
    "descubrir_jerarquia_src",
    "descubrir_subcarpetas_src",
    "ejecutar_orden",
    "formatear_resultado",
    "interpretar_orden",
    "lenguajes_fuente",
    "localizar_lcbs_neuronal",
    "localizar_red_neuronal",
    "main",
    "obtener_constructor",
]