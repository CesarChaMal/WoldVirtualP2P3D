# Wold Virtual P2P 3D — Plan de desarrollo (Boy) · 2026-11-19

| dato | valor |
|---|---|
| **Rama / HEAD del análisis** | `DevQoderIA` · `81c1b2a8` (merge de `DevChatGPTIA`, 1 conflicto resuelto) |
| **Estado vs origin** | 0 ahead / 0 behind; solo `.dev/` sin rastrear |
| **Versión en el árbol** | `2.9.5` (`Visor3D.csproj`, `AssemblyName = DAOLCDMWoldVirtualP2P3Dv2.9.5`) |
| **Ruta canónica** | `VersionDesesion/CONTRRF/RFC/CONTRUCCION/TLLDCD/src` |
| **Fecha del análisis** | 2026-11-19 |
| **Issues abiertos de referencia** | 23 (ver §8) |
| **PRs de debate abiertos** | 7 (#124, #130, #135, #136, #137, #141, #145) — **no son merge, son RFC/debate** |
| **Documento de cabecera que respeta** | `STM_DOC/Desarrollo/README_DESARROLLO.md` + `README_PLAN_DE_DESARROLLO.md` (plan v2.7.8/v2.7.9, histórico) |

---

## 0. Línea base verificada (2026-11-19)

El árbol actual **no** es el del plan anterior (`actualizacion_4e0f1cf6`, v2.7.9). Desde entonces se
consolidó en `DevQoderIA`: Zero-Trust nativo C#, isla DAO única, LucIA conversacional, túneles
efímeros y el refactor MD8–MD10. Medido hoy sobre el árbol trackeado:

| Elemento | Estado real (verificado) |
|---|---|
| Zero-Trust (`SeguridadSesionUnica/`) | Presente: `GestorClavesTPM.cs`, `AlmacenSeguro.cs`, `AntiRollbackGuardia.cs`, `FronteraSeguraZeroTrust.cs`… |
| LucIA + DAO 3D | Presentes: `IslaUnicaLucIA.gd`, `LucIAprc.py`, `OnboardingDAO.gd`, `ChunkManager.gd`, `TeleportManager.gd` |
| Refactor C# MD8–MD10 | Absorbido; `MainWindow.xaml.cs` vuelve a **4955 líneas** |
| `.github/workflows/` en raíz | **No existe** (DT-01 sigue abierto) |
| `VERSION.json` / `Directory.Build.*` | **No existen** (versión única sigue pendiente) |
| `cloudflared.exe` empaquetado | **No** (no está en el `.csproj`; `*.exe` en `.gitignore`) |
| `SshTunnelWatchdog.cs` | Referido en #143; **no localizado en el árbol trackeado** (solo `P2PTunnelManager.cs` y `TunnelProcessRegistry.cs`) — a localizar antes de F1 |
| R-01 (`transport.py`) | Sigue un `STM_HRTS/STM_DEF/utils/transport.py` en el árbol — triar si es el vector o módulo legítimo de STM_DEF |
| IPFS | Solo `.gitkeep` trackeados; `ipfs.exe` (Kubo) es gitignored |

**Lo importante:** la base es mucho más sana que en el plan de noviembre 7 — el núcleo unificado ya
está promocionado. Lo que queda es **deuda de producto y de empaquetado**, no de restauración.

---

## 1. Cómo leer este documento

| Prefijo | Significado |
|---|---|
| **B-nn** | Hallazgo bloqueante |
| **F-n** | Fase (orden de ejecución) |
| **T-nn.mm** | Tarea concreta dentro de una fase |
| **D-nn** | Decisión pendiente (heredadas del plan anterior o nuevas) |
| **E-nn** | Evidencia exigida para cerrar una tarea |
| **S / M / L / XL** | Esfuerzo: S ≤ 2 h · M ≈ medio día · L ≈ 1–3 días · XL > 3 días |

**Reglas no negociables (heredadas y vigentes):**

1. **No cambiar rutas canónicas.** `.dev` = dependencias + temporales + bytecode; código en `TLLDCD/src`;
   `Rutas.json` es la fuente única de rutas; `AVATARES/` es copia de recuperación: no borrar, no mover,
   no commitear.
2. **Git**: `git add <fichero>` (nunca `-A`); rama por tarea; commit en español con ID de tarea; push
   solo a la rama que el usuario nombre. `main` no se toca.
3. **Nada de publicar/OTA/tag** sin confirmación explícita (`REGLAS_DE_ACTUALIZACION.md`, R4/R14/R16).
4. **Nada de secretos** en código ni logs. Prohibido silenciar pruebas con `continue-on-error`/`--ignore`
   sin motivo escrito.
5. **Idioma**: docs, comentarios y commits en español. Tab para `.gd`, 4 espacios para `.cs`/`.py`.

---

## 2. Objetivo del ciclo

Convertir la **deuda prioritaria** (túneles, IPC por archivos, empaquetado, pruebas) en **releases
verificables**, sin tocar rutas canónicas ni publicar sin confirmación. El norte es dejar el visor
distribuido **tan estable como en local** (cierra la brecha de #141) y con **un solo comando de build
reproducible** (#90).

---

## 3. Hallazgos bloqueantes

### B-01 · No hay CI en la raíz (DT-01, vive desde #48)
`.github/workflows/` no existe. Nada de lo que sigue se detectaría solo. **Bloquea todo lo demás.**
*Cierre:* T1-01 con jobs verdes.

### B-02 · No hay fuente única de versión
`2.9.5` está a mano en el `.csproj`; `VERSION.json`/`Directory.Build.*` no existen. La próxima release
repetirá el error de subir la versión en N sitios (#76 DT-17). *Cierre:* T1-02.

### B-03 · La distribución es más frágil que el local (#141 H1)
`cloudflared.exe` no viaja en la descarga (no empaquetado, `*.exe` gitignored) y no hay watchdog del
túnel WebNodo: el binario descargado cae a SSH gratuito intermitente. Es **la** brecha que un usuario
real nota. *Cierre:* F1 completa + evidencia de túnel con dos redes.

### B-04 · Deriva de código entre copias del árbol
Coexisten `HerraMientas/`, `Desarrollo/` (clon con enlaces relativos) y `Actualizaciones/LCDMWold…v2.8.5|v2.8.6`
con duplicados de `MainWindow.xaml.cs`, `RedP2P/`, `SeguridadSesionUnica/`… (#143, #90, #140). Riesgo
de corregir en una copia y no en otra. *Cierre:* T1-03 (inventario + consolidación sin mover rutas).

---

## 4. Fases y tareas

```
T1 higiene+CI+fuente única ─▶ F1 túneles SSH ─▶ F2 IPC visor↔Godot ─▶ F3 Zero-Trust/SMTPLC
                                  │                                   └▶ F5 Godot/mundo ─▶ F7 empaquetado
                                  └▶ F4 pruebas (paralelo) ─▶ F6 LucIA (paralelo)
```

### T1 — Higiene, CI y fuente única (bloqueante · M/L)

| ID | Tarea | Esf. |
|---|---|---|
| T1-01 | Crear `.github/workflows/ci.yml` en la raíz: build C# + xUnit, pytest estricta, `STM_DEF puerta`, selftests con `WOLD_RUNTIME_DIR` temporal. Reutilizar plantilla `STM_DEF/.github/workflows/stm_def.yml` | M |
| T1-02 | `VERSION.json` + `Directory.Build.props/targets`; eliminar literales del `.csproj`; `version_unica --comprobar` + prueba negativa | M |
| T1-03 | Inventario de copias (`HerraMientas/` vs `Desarrollo/` vs `Actualizaciones/*`): tabla fichero→duplicado→decisión (*consolidar enlaces* o *aceptar*). **Sin mover directorios canónicos**; preguntar antes de cualquier reordenación | M |
| T1-04 | Triar `STM_HRTS/STM_DEF/utils/transport.py`: ¿vector R-01 reintroducido o módulo legítimo? Documentar veredicto y actuar (cuarentena si procede) | S |
| T1-05 | Localizar `SshTunnelWatchdog.cs` (referido en #143): si solo vive en la copia de trabajo no commiteada, confirmar con el usuario antes de versionarlo | S |

**Evidencia:** CI verde en un PR de prueba; `version_unica --comprobar` OK; tabla T1-03 archivada.

### F1 — Túneles SSH estables en distribución (#141, #143, #131 · L)

| ID | Tarea | Esf. |
|---|---|---|
| T2-01 | Empaquetar o descargar `cloudflared.exe` a `%LOCALAPPDATA%\WoldVirtualP2P3D\` al primer arranque (decidir D2); no depender de que exista en la carpeta | M |
| T2-02 | Unificar `P2PTunnelManager` + watchdog bajo **un solo supervisor** (todos los slots p2p/webnodo/dao delegan en él); centralizar PIDs en `TunnelProcessRegistry` | L |
| T2-03 | Watchdog del túnel WebNodo: relanzar proceso caído y estado «caído» visible (H5 de #141) | M |
| T2-04 | Observabilidad: log a fichero (`%LOCALAPPDATA%\…\logs\tunnel.log`) en vez de `Debug.WriteLine`/`Console.WriteLine` (perdidos en WinExe) | S |
| T2-05 | Revisar regex/orden de proveedores en `P2PWebNode` (patrón `lhr\.rocks` obsoleto) + opciones SSH (`ServerAliveCountMax`, `ExitOnForwardFailure=yes`) | S |
| T2-06 | UI: indicar «Modo degradado (SSH gratuito)» vs «Cloudflare» en la barra de estado | S |

**Evidencia (E-F1):** prueba con dos redes externas — túnel se publica, reconecta tras caída, URL rota
con caducidad, cierre seguro; captura de log con causa (no «se cayó» a secas). Cierra #141 y la mitad de #143.

### F2 — IPC visor↔Godot sin archivos (#140 · L)

| ID | Tarea | Esf. |
|---|---|---|
| T3-01 | Decidir D3 (Named Pipes vs WebSocket loopback) y sustituir `user://*.json` (`tunel_solicitud`, `tunel_lucia_url`, `tunel_dao_url`, `validacion_resultado`) | L |
| T3-02 | Máquina de estados de red unificada (`Desconectado/Conectando/TúnelActivo/Error/Reconectando`) con señales, desacoplada del panel de teletransporte | M |
| T3-03 | Validación de esquema (tipos fuertes / JSON Schema) y timeouts con backoff exponencial + cancelación cooperativa | M |
| T3-04 | Limpieza de artefactos `user://` obsoletos al arrancar (evitar leer túneles de sesiones anteriores) | S |

**Evidencia:** cero polling por timers en el camino crítico; caída del host C# no deja `user://` en
estado inconsistente; reconexión automática sin intervención.

### F3 — Zero-Trust y SMTPLC (#122, #26 · L)

| ID | Tarea | Esf. |
|---|---|---|
| T4-01 | `NamedMutex` cross-process en `AlmacenSeguro` (`Global\WVP3D_Storage_Mutex`) contra contienda sobre `cadena_local.jsonl` | M |
| T4-02 | CLI `--provision-tpm` con elevación UAC (índice NV `0x01500001`) | M |
| T4-03 | Checkpoints canónicos cada 100 bloques (`ICheckpointTestigo`) | M |
| T4-04 | Vistas WPF `RegistroCuentaView` / `RecuperacionNivel3View` conectadas solo a `INucleoSeguro` | L |
| T4-05 | SMTPLC: cerrar D5/D8; extraer interfaces (`INetworkEnvironmentProvider`, `ISystemInfoProvider`) para pruebas con mocks | L |

**Evidencia:** `--seguridad-selftest` en verde; pruebas sintéticas de VM/VPN/colisión sin hardware real.

### F4 — Fortalecer pruebas (paralelo · #79–#83, #48 · M)

| ID | Tarea | Esf. |
|---|---|---|
| T5-01 | `medicion_modos.py`: JSON consistente, `time.monotonic()`, escritura atómica, limpieza de procesos | M |
| T5-02 | `stm_flujo.py`: eliminar `ejecutar()` duplicado, fronteras de fragmentos, solape real, nunca truncar en `ensamblar()` | M |
| T5-03 | `stm_gestion.py`: reemplazo transaccional, verificación por hash (no solo conteo), logs atómicos | L |
| T5-04 | `test_stage1_s1_identidad.py`: pruebas de conducta (replay, TTL, doble claim) además de inspección estática | M |
| T5-05 | `test_memoria.py`: aislamiento entre sesiones, persistencia/recuperación, ciclo de vida del singleton | M |
| T5-06 | xUnit: recuperar/ampliar cobertura (`EstadoGlobal`, `PerfilUsuario`, `ReinicioCuenta`, `LocationAccountLedger`, `VmDetector`/`VpnDetector`) | M |

**Evidencia:** suites en verde; criterios de aceptación de cada issue marcados uno a uno.

### F5 — Godot / mundo (#1, #66, #131 · L)

| ID | Tarea | Esf. |
|---|---|---|
| T6-01 | Re-exportar `wvp3d.pck` del árbol canónico y `--registrar-pck` (RU5) — sin arrastrar `AVATARES/` | M |
| T6-02 | `Personalizacion.gd:952` (`Nil` → `PackedInt32Array`) y los dos sitios con el mismo patrón | S |
| T6-03 | Regenerar UIDs/importaciones glTF (UIDs inválidos/duplicados) | M |
| T6-04 | Prueba LAN con dos equipos reales (DT-04, cierre de #1) + IPv6 literal `[fe80::1]:9000` (DT-09) | M |
| T6-05 | Fugas `owner_names`/`island_owners` nunca borrados y topes de prims por isla | M |

**Evidencia:** PCK con `sha256` registrado en `VERSION.json`; capturas LAN de dos máquinas con avatar+isla.

### F6 — LucIA (#16, #85, #131 · M)

| ID | Tarea | Esf. |
|---|---|---|
| T7-01 | Versionar `lucia3D/puente_3D/` (~4 ficheros, ~20 KB) sin el proyecto Godot completo | S |
| T7-02 | Pruebas de `LucIAPuenteComandos` con agente falso inyectado (sin Ollama) | M |
| T7-03 | Intención: negación corta (`no/ni/sin`) o clasificador; `_ruta_en()` robusto (`git ls-files` + fuzzy) | S |
| T7-04 | Ejecución en hilo con `presupuesto_segundos` + eventos `estado` intermedios (el chat no se queda «pensando») | M |
| T7-05 | `autoanalisis()` con `default=str` (bug real: `WindowsPath is not JSON serializable`) | S |
| T7-06 | Regresión lingüística: respuestas en español (`qwen2.5:7b` produjo chino) | S |

**Evidencia:** puente con pruebas verdes; chat 3D responde sin bloquear; sin fragmentos multilingües.

### F7 — Empaquetado y distribución (#90, #87, #15, #131 · L/XL)

| ID | Tarea | Esf. |
|---|---|---|
| T8-01 | Pipeline reproducible: un comando genera `.exe` + PCK + ZIP desde una única fuente | L |
| T8-02 | `SHA256SUMS` + firma de código junto a la release | M |
| T8-03 | Métrica de huella instalada (binario+runtime+motor+PCK) y reducción: deduplicar `gltfimport/` (112 MiB duplicados) antes de recomprimir | L |
| T8-04 | Barrido de `wvp_vol_*` huérfanos en `%TEMP%` al arrancar | S |
| T8-05 | Evitar doble compresión (ZIP de 1,16 GB que contiene PCK de 1,28 GB) | M |

**Evidencia:** release desde un commit con hash y firma; tamaño medido contra presupuesto acordado (D4).

---

## 5. Decisiones pendientes

| ID | Decisión | Opciones | Recomendación | Bloquea |
|---|---|---|---|---|
| **D1** (nueva) | ¿Boy trabaja sobre `DevQoderIA` o rama por tarea? | (a) rama por tarea desde `DevQoderIA`; (b) directo en `DevQoderIA` | **(a)** — una tarea = una rama = un PR = una evidencia | todo |
| **D2** (nueva) | `cloudflared.exe`: ¿embebido o descarga runtime? | (a) descargar a `%LOCALAPPDATA%` en el launcher; (b) embebido como recurso | **(a)** — evita engordar el `.exe` y el `*.exe` en git | T2-01 |
| **D3** (nueva) | IPC visor↔Godot: ¿Named Pipes o WebSocket loopback? | (a) Named Pipes `\\.\pipe\WoldVirtualIPC`; (b) WS `127.0.0.1` | **(a)** — nativo Windows, sin puerto en conflicto | T3-01 |
| **D4** (heredada) | Meta numérica de #15 | ≤ 250 MiB vs otro número | Medir primero (T8-03) y decidir con la cifra | T8-03 |
| **D5** (heredada) | SMTPLC: ¿bloquea o puntúa? | bloqueo duro vs puntuación+fricción | Sin decidir (producto) | T4-05 |
| **D8** (heredada) | Destino del ledger de ubicación | integrar / degradar / eliminar | Sin decidir | T4-05 |
| **D9** (heredada) | Destino de la evidencia forense R-01 | archivo firmado fuera del repo vs manifiesto | Sin decidir | T1-04 |

---

## 6. Secuencia sugerida

| Tramo | Trabajo | Deja en verde |
|---|---|---|
| 1 | T1-01…T1-05 (CI, versión única, inventario, triar transport.py) | CI + `version_unica --comprobar` |
| 2 | F1 (túneles) — prioridad de usuario real | túnel estable en distribución |
| 3 | F2 (IPC) — desacopla visor↔Godot | sin polling ni archivos `user://` |
| 4 | F4 (pruebas) en paralelo | pytest/xUnit ampliados |
| 5 | F3 (Zero-Trust/SMTPLC) + F5 (Godot/mundo) | selftests + PCK fresco |
| 6 | F6 (LucIA) en paralelo | puente versionado y con pruebas |
| 7 | F7 (empaquetado) | release reproducible firmada |

**Nota de paralelismo:** F4 y F6 no dependen de F1/F2 y pueden arrancar en paralelo; F7 es la última
porque su evidencia (release desde commit) depende de T1-02 y F5.

---

## 7. Protocolo de ingeniería (obligatorio)

- Una tarea = una rama (`boy/T2-01-cloudflared`) = un commit en español con ID = una evidencia en el PR.
- Antes de commitear: `git status` y revisar staged; prohibido `\.env$|ghp_|*.exe|*.pck|*.zip`.
- Puertas antes de dar por hecho cualquier tarea de visor:
  ```bash
  cd VersionDesesion/CONTRRF/RFC/CONTRUCCION/TLLDCD/src
  python -m pytest -q
  python -m STM_HRTS.STM_DEF escanear
  cd VSC#/Visor3Ddesarrollo && dotnet build -p:Platform=x64
  # y tras tocar red/OTA/túneles: las 6 banderas --*-selftest
  ```
- Tras tocar `.gd`: re-exportar PCK es **decisión de dueño** (RU5) — no se hace unilateralmente.
- Lo que no se pueda verificar se marca **«no verificado»**; nada de afirmar verde sin salida adjunta.

---

## 8. Correspondencia issue ↔ tarea

| Issue | Tareas que lo cierran (total o parcial) |
|---|---|
| #1 (LAN) | T6-01, T6-04 |
| #15 (peso) | T8-03, D4 |
| #16 (lucIA) | T7-01…T7-06 |
| #26 (SMTPLC) | T4-05, D5, D8 |
| #48 (DT-01…DT-16) | T1-01, T1-02, T5-06, T6-04 |
| #65 (unificación) | T1-03 (consolidación sin mover rutas) |
| #66 (v2.7.7) | T1-02, T6-01, T8-01/T8-02 |
| #69 (STM_DEF) | T1-01, T1-04 |
| #76 (v2.7.9) | T1-02, T6-01, T8-01 |
| #79 (medicion_modos) | T5-01 |
| #80 (stm_flujo) | T5-02 |
| #81 (stm_gestion) | T5-03 |
| #82 (stage1 identidad) | T5-04 |
| #83 (memoria_sesion) | T5-05 |
| #85 (RFPRMN/IPFS) | T7-01…T7-05 |
| #87 (empaquetado volátil) | T8-04, T8-05 |
| #90 (2.8.3) | T1-03, T8-01…T8-03 |
| #101 (MD8-MD10) | T1-03 (verificar absorción y deuda #1/#4 del issue) |
| #120 (rutas .dev) | T1-03 |
| #122 (hardening) | T4-01…T4-04 |
| #131 (DAO v2.8.7–2.9.1) | T2-01…T2-06, T6-03, T7-06, T8-01 |
| #140 (IPC archivos) | T3-01…T3-04 |
| #143 (Secciones/SSH) | T1-03, T1-05, T2-02, T2-06 |

**PRs de debate (#124, #130, #135, #136, #137, #141, #145):** no se mergean. Son RFC/debate. Su
contenido técnico queda absorbido por las fases de este plan (#141 → F1; #143 → F1/T1; #124/#135/#130
→ F3 ya consolidada). Se cierran solo cuando el usuario lo pida y con confirmación.

---

## 9. Métricas objetivo

| Métrica | Base 2026-11-19 | Meta |
|---|---|---|
| CI en raíz | 0 jobs | 4–5 jobs verdes |
| Fuente única de versión | literal `2.9.5` a mano | `VERSION.json` + `version_unica --comprobar` |
| Túnel en distribución | SSH gratuito intermitente | Cloudflare con watchdog + log con causa |
| IPC visor↔Godot | `user://*.json` + polling | Named Pipes / WS + FSM |
| PCK alineado con commit | sin ancla | `pck_sha256` en `VERSION.json` |
| Release reproducible | manual, `VSC#` rompe `gh` | 1 comando + SHA256SUMS + firma |

---

## 10. Riesgos

| # | Riesgo | Prob. | Impacto | Mitigación |
|---|---|---|---|---|
| R1 | Consolidar copias (T1-03) rompe una ruta canónica | Media | Alto | Sin mover directorios; tabla + confirmación del dueño antes de cada cambio |
| R2 | `transport.py` de STM_DEF es el vector R-01 reintroducido | Media | Alto | T1-04 antes de cualquier merge; `STM_DEF escanear` + `historial` |
| R3 | Re-exportar PCK arrastra `AVATARES/` o rompe el mundo | Media | Alto | Preset sin `AVATARES`; recorrido carrusel→registro→isla con capturas |
| R4 | Watchdog unificado (T2-02) duplica reintentos con los existentes | Media | Medio | Un solo supervisor; eliminar lógica paralela en `MainWindow`/`P2PWebNode` |
| R5 | Probar cambios de red sin red real (D6) | Media | Medio | Evidencia con dos redes externas antes de dar F1 por cerrada |

---

*Generado el 2026-11-19 a partir de la medición directa del árbol en `DevQoderIA` (`81c1b2a8`) y de la
lectura de los 23 issues abiertos y 7 PRs de debate. Lo verificado se cita con ruta/comando; lo no
verificado se declara explícitamente. No se modificó ningún fichero del producto para escribir este plan.*
