# Resultados T1 y Consolidación Inicial — Plan de Desarrollo (2026-11-19)

Avance verificado del tramo inicial (~18%) del [plan_desarrollo_boy_2026-11-19.md](plan_desarrollo_boy_2026-11-19.md).
Consolidado e integrado en la rama canónica **`DevAntigravityIDEIA`** tras resolver el merge de `DevQoderIA` y centralizar la jerarquía modular en `.dev`. Sin merge a `main`.

---

## 1. Lo Ya Hecho (Completado y Verificado en `DevAntigravityIDEIA`)

### 1.1 T1-04 · Triar `transport.py` (Resuelto y neutralizado)
- `STM_HRTS/STM_DEF/utils/transport.py` fue auditado: se confirmó como el stub neutralizado del incidente R-01 (hallazgo **R01b**).
- Se retiró la importación residual `import utils.transport` de `STM_HRTS/STM_DEF/__main__.py`.
- La función inerte `cargar()` lanza `RuntimeError` explícito ante cualquier invocación. Se conserva el stub inerte como evidencia auditada sin vulnerabilidad activa.

### 1.2 T1-05 · Localización e Integración de `SshTunnelWatchdog.cs`
- Se localizó `SshTunnelWatchdog.cs` en la rama `DevAntigravityIDEIA`.
- Al realizar el merge de `DevQoderIA` hacia `DevAntigravityIDEIA` (commit `0a4dd01c`), se unificó e integró formalmente en el árbol canónico bajo `VSC#/Visor3Ddesarrollo/RedP2P/SshTunnelWatchdog.cs`.
- Se resolvió la brecha de deriva B-04 para este componente.

### 1.3 T1-03 · Inventario de copias del árbol C# (Archivado)
- Conteo y mapeo completado bajo `VSC#/Visor3Ddesarrollo`:
  - `HerraMientas/`: 72 ficheros `.cs` (visor activo, compilación real).
  - `Actualizaciones/LCDMWoldVirtualP2P3Dv2.8.5/`: 130 ficheros `.cs` (snapshot viejo).
  - `Actualizaciones/LCDMWoldVirtualP2P3Dv2.8.6/`: 142 ficheros `.cs` (snapshot viejo).
  - `Actualizaciones/DAOLCDMWoldVirtualP2P3Dv2.9.3/`: 1 fichero `.cs` (fragmento).
  - `VisorLinux/`: 27 ficheros `.cs` (proyecto Avalonia).
  - Módulos compartidos: `Chat/`, `RedP2P/`, `Services/`, `Seguridad/`, `p2pipfsCS/`.
- Identificados duplicados (`AppExtractor.cs`, `GodotConfig.cs`, `MainWindow.xaml.cs`) para consolidación progresiva sin mover rutas canónicas.

### 1.4 T1-02 (Fase A) · Fuente Única de Versión (Verificada)
- `src/VERSION.json` creado: `version 2.9.5`, `motor 4.7.2`, ancla del PCK `CopiaDeSeguridad/wvp3d.pck` (sha256 `9c943921…`, 509.257.972 B).
- `VSC#/Directory.Build.props` creado: expone `$(WoldVersion)`, `$(WoldMotor)` y `$(WoldVersionFull)` con guardas fail-closed (`<Error>` si falta tabla o campo).
- `VSC#/Directory.Build.targets` creado: genera `obj/WoldInfoVersion.g.cs` de forma reproducible (tarea inline Roslyn).
- Verificado:
  - `version_unica.py --leer` → `2.9.5`.
  - `dotnet msbuild -getProperty:WoldVersion` → `2.9.5`.
  - `dotnet build -p:SkipGodotAssets=true` → **0 errores**.

### 1.5 T1-01 & Centralización Modular de CI en `.dev/.github`
- Workflows de CI (`ci.yml` y `stm_def.yml`) configurados para la ruta canónica `VersionDesesion/CONTRRF/RFC/CONTRUCCION/TLLDCD/src`.
- **Centralización estricta en `.dev`**: siguiendo las directrices de contención del repositorio, la carpeta `.github` se trasladó y confinó en `.dev/.github/workflows/`, eliminándola de la raíz del repositorio para no tener dependencias ni `.yml` desperdigados.
- Se configuró `.gitignore` para rastrear `!VersionDesesion/**/.dev/.github/`.
- `gestor_entorno.py` y sus submódulos gestionan la centralización mediante los subcomandos `python gestor_entorno.py github [centralizar|desplegar-ci]`.

### 1.6 Fijación de Escena Canónica `res://WoldVirtual3DP2P.tscn`
- Escena fijada con UID `uid://5bh8su08n101` en `MTBGT/WoldVirtualP2P3D/WoldVirtual3DP2P.tscn`.
- Verificado `run/main_scene="uid://5bh8su08n101"` en `project.godot`.
- Invariante protegido en `GodotConfig.cs` (`GetScenePath() => "res://WoldVirtual3DP2P.tscn"`) y `MainWindow.xaml.cs`.
- Módulo `visor_mgr.py` incluye `verificar_fijacion_escena()` y el comando `python gestor_entorno.py escena-visor` (verificación: `[OK] FIJADA`, sin acoplamiento a versiones previas).

### 1.7 Entorno Python Canónico (.dev/.python)
- Intérprete canónico verificado: **Python 3.15.2 (amd64)** en `.dev/.python/python.exe`.
- Suite de pruebas de seguridad Zero Trust ejecutada bajo Python 3.15.2: **20 pruebas pasadas (100% verde)**.
- Integrado en `gestor_entorno.py dependencias` e informe general.

### 1.8 T2-05 · Regex y Opciones SSH del Túnel (Anticipada y Verificada)
- `P2PWebNode.cs`: regex actualizada a `@"https://[\w\-]+\.lhr\.life"`.
- Opciones de robustez añadidas en `MainWindow.xaml.cs` y `P2PWebNode.cs`: `-o ServerAliveCountMax=3 -o ExitOnForwardFailure=yes` para candidatos `localhost.run` y `serveo.net`.

---

## 2. Lo Pendiente Especificado (Backlog Inmediato)

### 2.1 Cierre de T1-02 (Fase B — Adaptación Completa de csproj)
Listado exacto de comprobaciones pendientes para dejar `test_version_unica.py` en verde:
1. **Actualizar rutas en `version_unica.CSPROJ`**: sustituir referencias a `Visor3D.csproj` y `Nucleo/Nucleo.csproj` inexistentes por:
   - `Visor3Ddesarrollo/HerraMientas/Visor3D.csproj`
   - `Visor3Ddesarrollo/VisorLinux/VisorLinux.csproj`
   - `Visor3Ddesarrollo/Tests/Visor3DTests.csproj`
2. **Convertir literales de versión en `.csproj`**:
   - `HerraMientas/Visor3D.csproj`: retirar versiones hardcodeadas `v2.9.5` en AssemblyName, Product, Version, AssemblyVersion, FileVersion, Title, Description.
   - `VisorLinux/VisorLinux.csproj`: reemplazar `<Version>2.8.0</Version>` por la variable unificada.
3. **`RutasLinux.cs` (líneas 15-16)**: reemplazar `Version = "2.7.9"` y motor hardcodeado por `WoldInfoVersion`.
4. **Motor Godot hardcodeado**: extraer `Godot_v4.7.2-stable_win64.exe` desde `WoldInfoVersion.NombreMotor` en `AppExtractor.cs` (60, 173), `GodotConfig.cs` (204) y `MainWindow.xaml.cs` (1683, 1685, 1707).
5. **Comprobación verde**: ejecutar `pytest tests/STM_test3/test_version_unica.py` y verificar salida sin exclusiones.

### 2.2 F1 — Túneles SSH Estables en Distribución (#141, #143, #131)
- **T2-01**: Resolver empaquetado o descarga de `cloudflared.exe` a `%LOCALAPPDATA%\WoldVirtualP2P3D\` al primer arranque (sin incluir `.exe` en git).
- **T2-02**: Unificar `P2PTunnelManager` y `SshTunnelWatchdog` bajo un único supervisor unificado de túneles (todos los slots delegan en él; PIDs en `TunnelProcessRegistry`).
- **T2-03**: Watchdog para reconexión automática de WebNodo con visualización de estado en UI.
- **T2-04**: Redirigir trazas de túneles a `%LOCALAPPDATA%\WoldVirtualP2P3D\logs\tunnel.log` en lugar de `Debug.WriteLine`.
- **T2-06**: UI: indicar «Modo degradado (SSH gratuito)» vs «Cloudflare» en la barra de estado.

### 2.3 F2 — IPC Visor ↔ Godot sin Archivos (#140)
- **T3-01**: Definir sustituto de `user://*.json` (Named Pipes `\\.\pipe\WoldVirtualIPC` o WebSocket loopback).
- **T3-02**: Máquina de estados de red unificada (`Desconectado/Conectando/TúnelActivo/Error/Reconectando`) desacoplada del panel de teletransporte.
- **T3-03**: Validación de esquema y timeouts con backoff exponencial.
- **T3-04**: Limpieza de artefactos obsoletos de sesiones previas en `user://`.

### 2.4 Fases Subsiguientes del Plan Master (F3 a F7)
- **F3 — Zero-Trust y SMTPLC**: `NamedMutex` cross-process en `AlmacenSeguro`, provisionamiento TPM 2.0 (`--provision-tpm`), checkpoints canónicos (`ICheckpointTestigo`).
- **F4 — Fortalecer pruebas**: suites de `medicion_modos.py`, `stm_flujo.py`, `stm_gestion.py` y cobertura xUnit.
- **F5 — Godot / Mundo**: Re-exportar `wvp3d.pck` limpio sin `AVATARES/` y corregir `PackedInt32Array` en `Personalizacion.gd:952`.
- **F6 — LucIA**: Versionado limpio de `puente_3D` y pruebas de `LucIAPuenteComandos` con agente falso inyectado (sin Ollama).
- **F7 — Empaquetado y Distribución**: Pipeline reproducible de un comando para `.exe` + PCK + ZIP firmado con SHA256SUMS.
