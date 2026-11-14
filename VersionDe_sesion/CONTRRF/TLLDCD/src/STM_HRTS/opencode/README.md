# OpenCode en STM_HRTS (WoldVirtualP2P3D)

Instalación limpia, local y autónoma de **OpenCode** (v1.19.5) desde cero.

---

## 🚀 Formas de Ejecución

Puedes ejecutar OpenCode de cualquiera de las siguientes formas:

1. **Doble clic (Consola interactiva):**
   - Ejecuta [`ejecutar_opencode.bat`](file:///E:/WoldVirtualP2P3D/VersionDe_sesion/CONTRRF/TLLDCD/src/STM_HRTS/opencode/ejecutar_opencode.bat).
   - Abre la ventana con codificación UTF-8 e inicia OpenCode directamente.

2. **Desde la línea de comandos / terminal (en esta carpeta):**
   ```powershell
   cd E:\WoldVirtualP2P3D\VersionDe_sesion\CONTRRF\TLLDCD\src\STM_HRTS\opencode
   .\opencode.exe
   ```
   o también:
   ```cmd
   opencode.bat
   ```

3. **Globalmente desde cualquier consola:**
   ```powershell
   opencode
   ```

---

## 🛠️ Errores detectados y soluciones aplicadas

1. **Falta de `npm` y runtime Node.js incompleto:**
   - Previamente sólo existía `node.exe` en `C:\Program Files\nodejs` sin `npm` ni `npx`.
   - Se reinstaló de forma limpia **Node.js LTS (v26.13.0)** con **npm (v11.20.0)**.

2. **Error de directiva de ejecución en PowerShell (`UnauthorizedAccess`):**
   - PowerShell bloqueaba los scripts `.ps1` al tener directiva `Restricted`.
   - Se configuró `Set-ExecutionPolicy -Scope CurrentUser RemoteSigned` para permitir la ejecución legítima de scripts de herramientas locales y npm.

3. **Versiones desfasadas de dependencias y plugins de OpenCode:**
   - La configuración en `~/.config/opencode` tenía dependencias obsoletas (`@opencode-ai/plugin 1.18.29` contra el motor 1.19.x) y carpetas temporales corruptas en npm.
   - Se actualizaron a la versión unificada **1.19.5** con compatibilidad OpenAI / Ollama (`@ai-sdk/openai-compatible 3.0.82`).

4. **Entorno local en `STM_HRTS/opencode`:**
   - Se generó `package.json` y `package-lock.json` limpios.
   - Se instalaron las dependencias locales y se copió el binario ejecutable `opencode.exe` (1.19.5) en la raíz de esta carpeta para máxima autonomía.

---

## 🔐 Permisos de edición configurados (2026-10-31)

Todo el bloque vive en [`opencode.jsonc`](file:///E:/WoldVirtualP2P3D/VersionDe_sesion/CONTRRF/TLLDCD/src/STM_HRTS/opencode/opencode.jsonc), a nivel de proyecto, y sobreescribe lo que haya en `~/.config/opencode`.

| permiso | valor | efecto |
| --- | --- | --- |
| `edit` | `allow` con `deny` para `*.env`, `.env.*`, `*.pem`, `*.key`, `*credentials*`, `*secret*`, `node_modules/*`, `*.exe` | escribe y parchea código **sin pedir aprobación** |
| `read`, `glob`, `grep`, `list`, `lsp`, `skill`, `task`, `todowrite`, `question` | `allow` | explorar el proyecto sin preguntas |
| `bash` | `ask` con lista blanca de lectura/build (`git status`, `ls`, `cat`, `node`, `npm`, `npx`…) | comandos normales sí, el resto pregunta |
| `bash` (deny explícito) | `rm -rf`, `rm -r`, `del`, `erase`, `rd /s`, `git push`, `git reset --hard`, `git clean`, `shutdown`, `format`, `npm publish`, `npm uninstall` | bloqueados aunque se apruebe otra cosa |
| `webfetch`, `websearch` | `ask` | salida a red siempre preguntada |
| `external_directory` | `ask` (sin cambiar del default) | tocar algo fuera del directorio pide permiso |

Reglas de opencode: gana **la última que casa**, por eso los `deny` van detrás de los `allow`.

**Cómo comprobarlas sin lanzar el TUI:**

```powershell
.\opencode.exe debug agent build    # ruleset efectivo (101 reglas al 2026-10-31)
```

### Pruebas ejecutadas (evidencia real)

| prueba | resultado |
| --- | --- |
| `opencode run "Crea PRUEBA_PERMISO.txt…"` | fichero creado, `← Write PRUEBA_PERMISO.txt / Wrote file successfully`, **sin pedir aprobación** |
| escribir `tokens.env` | ✗ bloqueado, opencode devuelve las reglas activas (`edit *.env deny`) |
| bash `del prueba_e2e.log` | ✗ bloqueado (`bash "del *" deny`); el fichero siguió existiendo |
| captura con un proxy temporal en el puerto 11435 (ya retirado) | opencode envía **10 tools** con `tool_choice: "auto"` y un system de **20.050 caracteres** |

---

## 🧩 Lo que impedía editar (no eran los permisos)

1. **Modelos que no existen.** El config global fijaba `ollama/cogito:3b` y `ollama/qwen3:1.7b` en los agentes `build`/`general`/`plan`, y ninguno está descargado en el Ollama de este PC. Se sobrescribieron con el bloque `agent` del config de proyecto.
2. **Contexto de Ollama demasiado pequeño (el bloqueador real).** Opencode manda ~6k tokens de system + schemas de tools, pero Ollama truncaba a `num_ctx` por defecto: se procesaban **2050 tokens** de 5850. Sin las tools en la ventana, el modelo *narraba* el comando en texto (`{"name":"write",…}`) en vez de llamarlo, y decía «no tengo acceso al sistema de archivos».

Mediciones del 2026-10-31 con el mismo payload (system de 20k + 10 tools):

| vía | contexto | tokens de prompt procesados | ¿`tool_calls`? |
| --- | --- | --- | --- |
| `/v1/chat/completions` | default del servidor | 2050 | no |
| `/v1/chat/completions` | `num_ctx: 32768` en el body | 2050 | no (**Ollama lo ignora**) |
| `/api/chat` nativo | `options.num_ctx: 4096` | 2050 | no |
| `/api/chat` nativo | `options.num_ctx: 32768` | 5850 | **sí** (`["write"]`) |
| `/v1/chat/completions` | alias `wvp-opencode` (num_ctx en el modelo) | 5647 | **sí** (`["write"]`) |

Como `/v1` ignora `num_ctx` del body y opencode habla por `/v1`, la solución pasa por subir el contexto **en el modelo**:

```powershell
# Modelfile (esta carpeta): FROM qwen3.5:latest + PARAMETER num_ctx 32768
ollama create wvp-opencode -f Modelfile
node comprobar_ollama.mjs wvp-opencode   # debe decir EDITABLE
```

El alias reutiliza las capas ya descargadas (no duplica los 6,6 GB) y no requiere reiniciar Ollama ni cambiar variables de entorno del sistema.

---

## 🧠 Modelos locales: estado verificado el 2026-11-14

| modelo | instalado | tool calling |
| --- | --- | --- |
| `wvp-opencode:latest` (alias 32k de `qwen2.5:7b`) | sí | **funciona** → modelo por defecto de opencode |
| `qwen2.5:7b` (7,6B Q4_K_M) | sí | emite `tool_calls` en `/v1` con `num_ctx 32768` |
| `codellama:7b` | sí | sin probar (no se usa como agente) |
| `qwen3.5:latest`, `qwen2.5-coder:*`, `llama3.1`, `cogito:3b`, etc. | **no** | ya no están descargados; se quitaron del config |

LM Studio (`http://localhost:1234/v1`) sigue apagado: su bloque de `provider` no se ha probado.

---

## 🆕 Puesta a punto de la IA local (sesión 2026-11-04)

Estado final: **la IA local lee y edita código de forma end-to-end, verificado**. Resumen de lo desarrollado en esta sesión:

1. **Restauración de dependencias (`node_modules`).**
   - Durante una limpieza previa se borró `node_modules` de esta carpeta; se restauró con:
     ```powershell
     npm ci --no-audit --no-fund
     ```
     → **37 paquetes, exit 0**. El `postinstall` regeneró `opencode.exe` (201,8 MB, v1.19.5) en la raíz.
   - **Regla:** esta carpeta no se limpia; si por lo que sea se pierde `node_modules`, se recupera con `npm ci` (usa `package-lock.json`, reproducible). La carpeta completa ocupa ~870 MB y se conserva íntegra.

2. **Solución del `ProviderHeaderTimeoutError` (bloqueador real tras restaurar).**
   - Con el `headerTimeout` por defecto (300.000 ms) el primer `opencode run` abortaba: el modelo con razonamiento genera **>3.400 tokens** incluso para una tarea trivial, y con el prompt real de opencode (~6k tokens) se superaba el tiempo de cabecera.
   - Fix en [`opencode.jsonc`](file:///E:/WoldVirtualP2P3D/VersionDe_sesion/CONTRRF/TLLDCD/src/STM_HRTS/opencode/opencode.jsonc) → `provider.ollama.options`:
     ```jsonc
     "timeout": false,
     "headerTimeout": false,
     "chunkTimeout": false
     ```
   - El modelo **sí generaba** (14,25 t/s); no era fallo de red ni de GPU.

3. **Precarga del modelo (`keep_alive`).**
   - Se precarga `wvp-opencode` con `keep_alive` largo (≈24 h) para evitar el arranque en frío de cada turno. Verificado con `/api/ps`: **5,89 GB de VRAM**, `expires_at` a ~24 h.

4. **Verificación end-to-end (evidencia real).**
   - Tool-calling: `prompt_tokens: 5647` **sin truncado**, `tool_calls: ["write"]` → veredicto **EDITABLE**.
   - Escritura real: opencode creó `PRUEBA_IA_OK.txt` con el contenido `edicion local OK`; el log mostró `permission=edit → allow` y `touching file`.
   - Test aislado del modelo: 267 tokens, 14,8 t/s, 19,3 s.
   - Tras la prueba se eliminó el fichero `PRUEBA_IA_OK.txt` residual.

5. **Hallazgo de rendimiento (pendiente de optimizar).**
   - Cada turno tarda **minutos** porque el modelo gasta miles de tokens de "razonamiento" antes de responder. No impide editar, pero hace lento el uso interactivo.
   - Propuestas: limitar `num_predict` en el `Modelfile`, o usar un modelo sin cadena de razonamiento (`qwen2.5:7b` ya confirmado para tool-calling).

**Estado de la carpeta tras la sesión:** intacta y operativa (~870 MB), con `opencode.exe`, `node_modules`, `Modelfile`, `comprobar_ollama.mjs`, `opencode.jsonc` y este README.

---

## 🔧 Reparación de la IA local (sesión 2026-11-14)

Al retomar el trabajo se detectó que el flujo RFPRMN había vaciado `TLLDCD/src`: no quedaban ni `node_modules` ni `opencode.exe`, y en Ollama sólo sobrevivían `codellama:7b` y `qwen2.5:7b`. El alias `wvp-opencode` y `qwen3.5:latest` habían desaparecido, así que el config apuntaba a modelos inexistentes.

Cambios aplicados:

1. **`Modelfile` reescrito** (3 copias: `TLLDCD/src` + las dos `actualizacion_*`):
   ```dockerfile
   FROM qwen2.5:7b
   PARAMETER num_ctx 32768
   ```
2. **Alias recreado** (reutiliza las capas de `qwen2.5:7b`, no duplica los 4,7 GB):
   ```powershell
   ollama create wvp-opencode -f Modelfile
   ```
3. **`opencode.jsonc` corregido** (3 copias + config global):
   - `provider.ollama.models` ahora sólo lista modelos existentes: `wvp-opencode:latest`, `qwen2.5:7b`, `codellama:7b`.
   - `model`, `small_model` y los agentes `build`/`general`/`plan` apuntan a `ollama/wvp-opencode:latest`.
   - Se añadieron `timeout`/`headerTimeout`/`chunkTimeout: false` también en la config global.
4. **Config global** (`~/.config/opencode/opencode.jsonc`): antes fijaba `ollama/cogito:3b` y `ollama/qwen3:1.7b`, inexistentes. Ahora usa `ollama/wvp-opencode:latest` en todos los agentes.

**Detalle importante de OpenCode v2:** el ID del modelo debe coincidir con el que reporta Ollama (`/api/tags`), incluido el sufijo `:latest`. Referenciar `ollama/wvp-opencode` (sin `:latest`) falla con `Model unavailable`.

**Verificación (evidencia real):**

| prueba | resultado |
| --- | --- |
| `node comprobar_ollama.mjs wvp-opencode` | `prompt_tokens: 5966`, `tool_calls: ["write"]`, veredicto **EDITABLE** |
| `opencode run "di OK"` desde `TLLDCD/src/STM_HRTS/opencode` | `> build · wvp-opencode:latest` → `OK` |
| `opencode run "di OK"` desde la raíz del repo (config global) | `> build · wvp-opencode:latest` → `OK` |
| `/api/ps` tras precarga | `wvp-opencode:latest`, `ctx=32768`, `5,99 GB VRAM`, `keep_alive` ~24 h |

**Nota sobre la instalación local:** `node_modules` y `opencode.exe` siguen ausentes en esta carpeta. Los `.bat` detectan que no existe el binario local y caen al `opencode` global (v2.3.0), que es el que se ha validado. Si se quiere la copia autónoma, `npm ci` reinstala `opencode-ai@1.19.5` (versión anterior a la global).

---

## ⏭️ Pendientes y límites honestos

- **Rendimiento:** `qwen2.5:7b` no es un modelo de razonamiento, así que el turno es mucho más rápido que con el antiguo `qwen3.5` (que gastaba miles de tokens en "thinking"). Aun así, un 7B local es limitado para tareas grandes; para código complejo sigue siendo mejor un modelo en la nube.
- **Instalación autónoma:** `node_modules` y `opencode.exe` no están en la carpeta. Los `.bat` usan el `opencode` global (v2.3.0). `npm ci` reinstala la copia local, pero fija `opencode-ai@1.19.5`, anterior a la global.
- **`codellama:7b`** está instalado pero no se ha verificado su `tool_calling`; no se usa como agente.
- **LM Studio** sigue sin probar (apagado).
- La **lista blanca de `bash`** está cargada y evaluada (aparece en los errores de denegación), pero no se ha verificado de forma end-to-end que un comando `allow` se ejecute en modo no interactivo.
- `external_directory` se deja en `ask`: no se ha comprobado cómo clasifica opencode una ruta `../…` dentro del repo pero fuera de esta carpeta.
- Con `opencode --auto` se saltan también los `ask` de bash; no es el modo por defecto de esta carpeta.
- Los ficheros de esta carpeta viven bajo `TLLDCD/src`, que el flujo RFPRMN vacía al actualizar: el `Modelfile`, `comprobar_ollama.mjs` y este README son reproducibles pero conviene revisarlos tras cada actualización. Las plantillas `Sistema_Principal/PRY/actualizacion_*/src/STM_HRTS/opencode/` se han sincronizado con los mismos cambios.

