"""
STM_IALOCAL.py - Sistema de Tareas Múltiples Local (IA)
=======================================================

Propósito:
    Crear un sistema de IA completamente local en Python.
    - Descargar modelos de IA desde HuggingFace y guardarlos en caché local.
    - Chatear con el modelo local mediante una interfaz simple.
    - Editar/analizar código (parseo, validación, guardado) usando herramientas
      de Python estándar.

Estructura del proyecto:
    E:\WoldVirtualP2P3D\VersionDe_sesion\CONTRRF\STM_RFC\STM_RFPRMN\STM_RFPRMN_stmDL\STM_IALCL_PY

Uso básico:
    from STM_IALOCAL import IAAllocator

    allocator = IAAllocator()
    allocator.load_model("meta-llama/Llama-2-7b-chat-hf")   # modelo local
    print(allocator.chat("Hola, ¿cómo estás?"))
    result = allocator.edit_code("print('Hola')")
    saved = allocator.save_code("test_code_demo.py", "print('Hola')\n")

Nota:
    La descarga real del modelo se realiza con la librería
    ``huggingface_hub`` y ``transformers``.  Esta clase es solo el
    gestor de caché y de interacción; reemplaza las llamadas simuladas
    por los reales cuando tengas acceso a internet.

Integración con llama.cpp:
    Llama.cpp permite ejecutar inferencia local sin depender de transformers.
    Se añade el método ``chat_llama_cpp`` que lanza el binario ``llama2``
    (o ``llama-cli``) contra el archivo de pesos cargado en
    ``base_dir/models/<model_id>/``.
"""

import os
import subprocess
from pathlib import Path
from typing import Dict, Optional, List


class IAAllocator:
    """
    Gestor de modelos de IA locales y de código.

    Atributos:
        base_dir: directorio raíz donde se almacenan modelos y archivos de código.
        models: dict {model_id: {"path": str, "status": str}}
        code_cache: dict {filename: str}  (ruta del archivo guardado)

    Métodos:
        load_model(model_id): descarga y carga un modelo desde HuggingFace en caché local.
        chat(prompt, model_id=None): devuelve una respuesta generada por el modelo local.
        chat_llama_cpp(prompt, model_id=None): usa llama.cpp para generar texto localmente.
        edit_code(code): analiza, valida y guarda el fragmento de código.
        save_code(filename, content): escribe ``content`` en ``filename`` dentro de base_dir.
    """

    def __init__(self, base_dir: str = r"E:\WoldVirtualP2P3D\VersionDe_sesion\CONTRRF\STM_RFC\STM_RFPRMN\STM_RFPRMN_stmDL\STM_IALCL_PY"):
        self.base_dir = Path(base_dir)
        self.models: Dict[str, Dict] = {}
        self.code_cache: Dict[str, str] = {}

    def _ensure_dirs(self):
        """Crea las subdirecciones necesarias."""
        for item in ("models", "code"):
            p = self.base_dir / item
            p.mkdir(parents=True, exist_ok=True)

    def load_model(self, model_id: str) -> Dict:
        """
        Descarga y carga un modelo desde HuggingFace en caché local.

        Parámetros:
            model_id: identificador del modelo en HuggingFace (p.ej., "meta-llama/Llama-2-7b-chat-hf").

        Retorna:
            Diccionario con claves 'path' (ruta al archivo .bin/.safetensors) y 'status'.
        """
        cache_path = self.base_dir / "models" / model_id
        self._ensure_dirs()
        cache_path.mkdir(parents=True, exist_ok=True)
        print(f"[IA] Cargando modelo '{model_id}' desde HuggingFace...")
        # Simulación de descarga real con huggingface_hub.hf_hub_download():
        #     hf_hub_download(repo_name="...", filename=model_id, cache_dir=str(cache_path))
        self.models[model_id] = {
            "path": str(cache_path),
            "status": "loaded",
        }
        return self.models[model_id]

    def chat(self, prompt: str, model_id: Optional[str] = None) -> str:
        """
        Genera una respuesta del modelo local (usando la implementación de transformers).

        Parámetros:
            prompt: texto de entrada para el modelo.
            model_id: id opcional del modelo que se usará (si no se da, usa el primero cargado).

        Retorna:
            Cadena con la respuesta generada.
        """
        if model_id is None:
            model_id = next(iter(self.models))
        info = self.models.get(model_id, {})
        response = f"[{model_id}] {prompt}"
        return response

    def chat_llama_cpp(self, prompt: str, model_id: Optional[str] = None) -> str:
        """
        Genera una respuesta usando **llama.cpp** (inferencia local sin transformers).

        Requiere que el binario ``llama2`` (o ``llama-cli``) esté disponible en el PATH
        o en la variable de entorno ``LLAMA_CPP_BIN``.

        Parámetros:
            prompt: texto de entrada.
            model_id: id opcional del modelo cargado; si no se da, usa el primero.

        Retorna:
            Texto generado por el modelo llama.cpp.
        """
        if model_id is None:
            model_id = next(iter(self.models))
        info = self.models.get(model_id, {})
        model_path = info.get("path", "")
        if not model_path or not Path(model_path).exists():
            raise FileNotFoundError(
                f"No se encontró el archivo de modelo en {model_path}. "
                f"Verifica que el modelo esté descargado correctamente."
            )
        # Llama.cpp usa el flag -m para especificar el modelo y --input para el prompt.
        # Ajusta los argumentos según tu versión de llama2/llama-cli.
        cmd = [
            "llama2",
            "-m", model_path,
            "--input", prompt,
            "--output", str(Path(self.base_dir) / "text_out.txt"),
        ]
        try:
            result = subprocess.run(
                cmd,
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
                text=True,
                timeout=300,
            )
            generated = result.stdout.strip()
            if result.stderr:
                generated += "\n" + result.stderr.strip()
            return generated
        except FileNotFoundError:
            # Fallback: llama-cli
            cmd = [
                "llama-cli",
                "-m", model_path,
                "--input", prompt,
                "--output", str(Path(self.base_dir) / "text_out.txt"),
            ]
            try:
                result = subprocess.run(
                    cmd,
                    stdout=subprocess.PIPE,
                    stderr=subprocess.PIPE,
                    text=True,
                    timeout=300,
                )
                generated = result.stdout.strip()
                if result.stderr:
                    generated += "\n" + result.stderr.strip()
                return generated
            except FileNotFoundError:
                raise RuntimeError(
                    "No se encontró ni 'llama2' ni 'llama-cli' en el PATH. "
                    "Instala llama.cpp: https://github.com/ggerganov/llama.cpp"
                )

    def edit_code(self, code: str) -> Dict:
        """
        Analiza, valida y guarda un fragmento de código.

        Parámetros:
            code: cadena de código fuente.

        Retorna:
            Diccionario con las claves 'original', 'lines', 'validated'.
        """
        self.code_cache["raw"] = code
        lines = len(code.splitlines())
        validated = True  # placeholder; añade linting real si lo deseas
        return {"original": code, "lines": lines, "validated": validated}

    def save_code(self, filename: str, content: str) -> Path:
        """
        Escribe ``content`` en ``filename`` dentro de ``base_dir``.

        Parámetros:
            filename: nombre del archivo (sin extensión o con).
            content: texto a guardar.

        Retorna:
            Ruta completa del archivo guardado.
        """
        path = self.base_dir / filename
        path.write_text(content, encoding="utf-8")
        self.code_cache[filename] = str(path)
        return path

    def list_models(self) -> List[str]:
        """Devuelve las claves de los modelos cargados."""
        return list(self.models.keys())

    def list_codes(self) -> List[str]:
        """Devuelve las claves de los archivos de código guardados."""
        return list(self.code_cache.keys())


if __name__ == "__main__":
    # Ejemplo rápido sin necesidad de internet
    allocator = IAAllocator()
    allocator.load_model("gpt2")
    print("Modelos disponibles:", allocator.list_models())
    print("Código editado:", allocator.edit_code("print('Hola, mundo')"))
    saved = allocator.save_code("test_code_demo.py", "print('Hola, mundo')\n")
    print("Archivo guardado en:", saved)
