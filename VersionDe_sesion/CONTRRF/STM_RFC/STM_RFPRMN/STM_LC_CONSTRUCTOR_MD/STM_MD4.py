"""Archivos de código vacíos para LucIA (STM_MD4).

Descubre los lenguajes de programación presentes en ``TLLDCD/src`` y crea
archivos vacíos con la misma jerarquía que ``crear carpeta`` (``STM_MD1``).

El comando ``crear archivo`` (traído de ``RFPRMN``) puede:

- crear un archivo suelto ``ruta/nombre(.extensión)``;
- recrear vacía toda la jerarquía de código de la fuente, igual que las carpetas.

La clase principal ``ConstructorArchivos`` y su clase anidada ``Operaciones``
fusionan el descubrimiento, la validación y la creación de archivos.
"""

from __future__ import annotations

import os
from collections.abc import Iterable
from pathlib import Path
from typing import Any

from .STM_MD1 import _EXCLUIR_CARPETAS, RUTA_FUENTE_PREDETERMINADA

#: Extensiones de código que el constructor recrea vacías.
EXTENSIONES_CODIGO: tuple[str, ...] = (
    ".py", ".cs", ".gd", ".gdshader", ".tscn", ".tres",
    ".xaml", ".csproj", ".html", ".mjs", ".js", ".ts",
    ".sh", ".bat", ".ps1", ".cmd",
)

#: Lenguaje asociado a cada extensión (para informes y diagnósticos).
LENGUAJES_POR_EXTENSION: dict[str, str] = {
    ".py": "python",
    ".cs": "csharp",
    ".gd": "gdscript",
    ".gdshader": "gdshader",
    ".tscn": "godot",
    ".tres": "godot",
    ".xaml": "xaml",
    ".csproj": "csharp",
    ".html": "html",
    ".mjs": "javascript",
    ".js": "javascript",
    ".ts": "typescript",
    ".sh": "shell",
    ".bat": "batch",
    ".ps1": "powershell",
    ".cmd": "batch",
}

#: Alias de lenguaje → extensión (resolución de ``crear archivo``).
EXTENSIONES_POR_LENGUAJE: dict[str, str] = {
    "python": ".py", "py": ".py",
    "csharp": ".cs", "cs": ".cs", "c#": ".cs",
    "gdscript": ".gd", "gd": ".gd", "godot": ".gd",
    "gdshader": ".gdshader", "shader": ".gdshader",
    "xaml": ".xaml",
    "html": ".html",
    "javascript": ".js", "js": ".js", "mjs": ".mjs",
    "typescript": ".ts", "ts": ".ts",
    "shell": ".sh", "sh": ".sh",
    "batch": ".bat", "bat": ".bat",
    "powershell": ".ps1", "ps1": ".ps1",
    "cmd": ".cmd",
}

#: Nombre por defecto cuando se pide un archivo sin nombre.
NOMBRE_ARCHIVO_PREDETERMINADO = "archivo"


def _rutas_programacion(
    ruta_fuente: str | Path | None,
    extensiones: Iterable[str] | None,
) -> list[Path]:
    """Recorre la fuente y devuelve los archivos de código, sin cachés."""
    base = Path(ruta_fuente or RUTA_FUENTE_PREDETERMINADA)
    deseadas = frozenset(str(ext).lower() for ext in (extensiones or EXTENSIONES_CODIGO))
    encontrados: list[Path] = []
    if not base.is_dir():
        return encontrados
    try:
        for actual, dirs, ficheros in os.walk(base):
            dirs[:] = sorted(d for d in dirs if d not in _EXCLUIR_CARPETAS)
            for fichero in ficheros:
                if Path(fichero).suffix.lower() in deseadas:
                    encontrados.append(Path(actual) / fichero)
    except OSError:
        return encontrados
    return encontrados


def descubrir_lenguajes_fuente(
    ruta_fuente: str | Path | None = None,
    extensiones: Iterable[str] | None = None,
) -> dict[str, int]:
    """Cuenta los archivos de código por extensión en ``TLLDCD/src``.

    Resultado ordenado por cantidad descendente; útil para informar de los
    lenguajes que el constructor va a recrear.
    """
    conteo: dict[str, int] = {}
    for archivo in _rutas_programacion(ruta_fuente, extensiones):
        extension = archivo.suffix.lower()
        conteo[extension] = conteo.get(extension, 0) + 1
    return dict(sorted(conteo.items(), key=lambda par: (-par[1], par[0])))


def descubrir_archivos_fuente(
    ruta_fuente: str | Path | None = None,
    extensiones: Iterable[str] | None = None,
) -> tuple[str, ...]:
    """Rutas relativas (POSIX) de los archivos de código de la fuente."""
    base = Path(ruta_fuente or RUTA_FUENTE_PREDETERMINADA)
    try:
        return tuple(sorted(
            archivo.relative_to(base).as_posix()
            for archivo in _rutas_programacion(ruta_fuente, extensiones)
        ))
    except (OSError, ValueError):
        return ()


def resolver_extension(nombre: str, lenguaje: str = "") -> str:
    """Añade la extensión del lenguaje si el nombre no la lleva."""
    limpio = str(nombre).strip()
    pedido = str(lenguaje or "").strip().lower()
    if pedido:
        extension = EXTENSIONES_POR_LENGUAJE.get(pedido, f".{pedido}")
        if extension and not limpio.endswith(extension):
            return limpio + extension
    return limpio


def crear_archivo_vacio(
    ruta: str | Path,
    nombre: str,
    lenguaje: str = "",
    maquina: Any | None = None,
) -> str:
    """Crea un archivo vacío ``ruta/nombre(.ext)`` y devuelve su ruta.

    Si se pasa ``maquina`` (fachada RFPRMN) se delega en su ``crear_archivo``
    para respetar la máquina de comandos; si no, se crea directamente.
    """
    limpio = str(nombre).strip()
    if not limpio or Path(limpio).name != limpio:
        raise ValueError(f"Nombre de archivo no válido: {nombre!r}")
    creador = getattr(maquina, "crear_archivo", None) if maquina is not None else None
    if callable(creador):
        return str(creador(str(ruta), limpio, str(lenguaje or "")))
    limpio = resolver_extension(limpio, lenguaje)
    destino = Path(ruta).expanduser() / limpio
    destino.parent.mkdir(parents=True, exist_ok=True)
    if not destino.exists():
        destino.touch()
    return str(destino)


def _partes_validas(relativa: str) -> tuple[str, ...] | None:
    """Partes de una ruta relativa, o ``None`` si es absoluta o insegura."""
    partes = Path(str(relativa).replace("\\", "/")).parts
    if (not partes or any(parte in {".", ".."} for parte in partes)
            or Path(partes[-1]).suffix == ""):
        return None
    return partes


def crear_jerarquia_archivos(
    base: str | Path,
    raiz: str,
    archivos: Iterable[str],
    maquina: Any | None = None,
) -> dict[str, Any]:
    """Crea ``base/raiz`` y los archivos relativos dentro; devuelve la traza."""
    raiz_ruta = Path(base) / str(raiz).strip() if str(raiz).strip() else Path(base)
    raiz_ruta.mkdir(parents=True, exist_ok=True)
    creados: list[str] = []
    pasos: list[dict[str, Any]] = []
    errores: list[str] = []
    for numero, relativa in enumerate(archivos, start=1):
        partes = _partes_validas(relativa)
        if partes is None:
            errores.append(f"{relativa!r}: ruta no válida")
            continue
        destino = raiz_ruta.joinpath(*partes)
        try:
            destino.parent.mkdir(parents=True, exist_ok=True)
            if maquina is not None and callable(getattr(maquina, "crear_archivo", None)):
                maquina.crear_archivo(str(destino.parent), destino.name)
            elif not destino.exists():
                destino.touch()
        except OSError as error:
            errores.append(f"{relativa!r}: {error}")
            continue
        creados.append(str(destino))
        pasos.append({"paso": numero, "archivo": str(relativa),
                      "ruta": str(destino)})
    return {"ok": not errores, "raiz": str(raiz_ruta), "archivos": creados,
            "total": len(creados), "pasos": pasos, "errores": errores}


class ConstructorArchivos:
    """Descubre y crea archivos de código vacíos con la jerarquía de la fuente."""

    class Operaciones:
        """Funciones puras de validación, resolución y creación."""

        @staticmethod
        def validar_nombre(nombre: str) -> bool:
            limpio = str(nombre).strip()
            return bool(limpio) and limpio not in {".", ".."} and Path(limpio).name == limpio

        @staticmethod
        def resolver_extension(nombre: str, lenguaje: str = "") -> str:
            return resolver_extension(nombre, lenguaje)

        @staticmethod
        def crear_uno(ruta: str | Path, nombre: str, lenguaje: str = "",
                      maquina: Any | None = None) -> str:
            return crear_archivo_vacio(ruta, nombre, lenguaje, maquina)

        @staticmethod
        def crear_jerarquia(base: str | Path, raiz: str,
                            archivos: Iterable[str],
                            maquina: Any | None = None) -> dict[str, Any]:
            return crear_jerarquia_archivos(base, raiz, archivos, maquina)

    def __init__(
        self,
        ruta_fuente: str | Path | None = None,
        maquina: Any | None = None,
        extensiones: Iterable[str] | None = None,
    ) -> None:
        self.ruta_fuente = (Path(ruta_fuente) if ruta_fuente
                            else RUTA_FUENTE_PREDETERMINADA)
        self.maquina = maquina
        self.extensiones = tuple(extensiones or EXTENSIONES_CODIGO)
        self._lenguajes: dict[str, int] | None = None
        self._archivos: tuple[str, ...] | None = None

    @property
    def lenguajes(self) -> dict[str, int]:
        """Lenguajes detectados en la fuente (extensión → número de archivos)."""
        if self._lenguajes is None:
            self._lenguajes = descubrir_lenguajes_fuente(
                self.ruta_fuente, self.extensiones)
        return self._lenguajes

    def archivos_fuente(self) -> tuple[str, ...]:
        """Jerarquía de archivos de código de la fuente (relativa y ordenada)."""
        if self._archivos is None:
            self._archivos = descubrir_archivos_fuente(
                self.ruta_fuente, self.extensiones)
        return self._archivos

    def crear(self, ruta_base: str | Path = "", raiz: str = "src",
              archivos: Iterable[str] | None = None) -> dict[str, Any]:
        """Recrea vacía la jerarquía de código bajo ``ruta_base/raiz``."""
        rutas = (tuple(archivos) if archivos is not None
                 else self.archivos_fuente())
        return crear_jerarquia_archivos(ruta_base, raiz, rutas, self.maquina)

    def crear_uno(self, ruta_base: str | Path = "",
                  nombre: str = NOMBRE_ARCHIVO_PREDETERMINADO,
                  lenguaje: str = "") -> str:
        """Crea un único archivo vacío (semántica de ``RFPRMN``)."""
        return crear_archivo_vacio(ruta_base, nombre, lenguaje, self.maquina)

    def __repr__(self) -> str:
        return (f"ConstructorArchivos(fuente={str(self.ruta_fuente)!r}, "
                f"extensiones={len(self.extensiones)})")


__all__ = [
    "ConstructorArchivos",
    "EXTENSIONES_CODIGO",
    "EXTENSIONES_POR_LENGUAJE",
    "LENGUAJES_POR_EXTENSION",
    "NOMBRE_ARCHIVO_PREDETERMINADO",
    "crear_archivo_vacio",
    "crear_jerarquia_archivos",
    "descubrir_archivos_fuente",
    "descubrir_lenguajes_fuente",
    "resolver_extension",
]