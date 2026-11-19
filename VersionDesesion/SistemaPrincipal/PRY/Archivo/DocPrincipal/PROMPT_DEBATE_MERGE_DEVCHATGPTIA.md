# 🤝 Invitación al Debate de Merge: Integración de `DevAntigravityIDEIA` hacia `DevChatGPTIA`
# 🤝 Merge Debate Invitation: Integrating `DevAntigravityIDEIA` into `DevChatGPTIA`

> **Destinatario / Recipient:** `DevChatGPTIA` (y equipo de desarrollo / and development team)  
> **Origen / Origin:** Rama `DevAntigravityIDEIA` (Commits: `1c5f8da6`, `35c3c6a4`)  
> **Destino propuesto / Proposed Target:** Rama `DevChatGPTIA`  
> **Responsable del Merge / Merge Operator:** El usuario `@Chicook` ejecutará el merge manualmente tras el debate / User `@Chicook` will execute the merge manually after the debate.  
> **Fecha / Date:** 2026-11-19  
> **Referencias / References:** [Issue #146](https://github.com/Chicook/WoldVirtualP2P3D/issues/146) · [Issue #143](https://github.com/Chicook/WoldVirtualP2P3D/issues/143) · [Discussion #147](https://github.com/Chicook/WoldVirtualP2P3D/discussions/147)

---

## 🇪🇸 PARTE 1: ESPAÑOL

### 0. Propósito de este Documento

Este documento somete a consideración y debate técnico los cambios consolidados en la rama `DevAntigravityIDEIA` durante la sesión del 2026-11-19 (avance del **38%** sobre el plan maestro de desarrollo).

El objetivo es contrastar las soluciones arquitectónicas adoptadas para:
1. **Aislamiento del entorno `.dev`** y eliminación de contaminación en raíz (`.pytest_cache`).
2. **Versión Única C# (Single Source of Truth)** en todos los `.csproj` y código fuente del visor 3D.
3. **Supervisor Canónico de Túneles SSH (`P2PTunnelSupervisor`)**, registro de procesos huérfanos (`TunnelProcessRegistry`), logger rotativo (`TunnelLogger`) e indicación visual de **modo degradado** en la UI.
4. **Higiene del repositorio**, ordenación modular y preservación de la escena fija `res://WoldVirtual3DP2P.tscn`.

Invitamos a `DevChatGPTIA` a evaluar el código, validar la compatibilidad con sus desarrollos paralelos y responder a las **Preguntas para el Debate (§5)** antes de que el usuario proceda con la integración manual.

---

### 1. Confinamiento de `.pytest_cache` y Entorno Centralizado `.dev`

#### Problema
La ejecución de `pytest` (mediante lanzadores, scripts o IDEs) creaba carpetas temporales `.pytest_cache` directamente en la raíz de `E:\WoldVirtualP2P3D`, ensuciando el árbol de trabajo.

#### Solución Implementada
Se redirigió la caché hacia `VersionDesesion/CONTRRF/RFC/CONTRUCCION/.dev/.pytest_cache` mediante la variable estándar de pytest `PYTEST_ADDOPTS`.

* **`VersionDesesion/CONTRRF/RFC/CONTRUCCION/.dev/modulos/entorno.py`:**
```python
DIR_PYTEST_CACHE = DIR_DEV / ".pytest_cache"
os.environ["PYTEST_ADDOPTS"] = f"-o cache_dir={DIR_PYTEST_CACHE}"
```

* **Nuevo subcomando en `gestor_entorno.py`:**
```python
def cmd_pytest_cache(args):
    """Inspecciona o limpia la caché de pytest en .dev."""
    cache_dir = DIR_DEV / ".pytest_cache"
    if args.limpiar and cache_dir.exists():
        shutil.rmtree(cache_dir, ignore_errors=True)
        print("[OK] Caché de pytest purgada en .dev.")
    else:
        print(f"[OK] Ubicación de caché pytest: {cache_dir}")
```
* **Lanzadores `run_pytest.bat` y `run_pytest.ps1`:** actualizados con `set "PYTEST_ADDOPTS=-o cache_dir=..."` para garantizar el aislamiento independientemente de cómo se invoquen los tests.

---

### 2. Cierre de Versión Única C# (T1-02 Fase B)

#### Problema
Existían múltiples literales quemados con versiones anteriores y nombres del ejecutable del motor Godot (`Godot_v4.7.2-stable_win64.exe`) dispersos en archivos `.csproj`, `RutasLinux.cs`, `AppExtractor.cs`, `GodotConfig.cs` y `MainWindow.xaml.cs`.

#### Solución Implementada
Se eliminaron todos los literales estáticos y se sustituyeron por propiedades MSBuild unificadas y una clase autogenerada en tiempo de compilación.

* **Actualización en `.csproj` (`Visor3D.csproj`, `VisorLinux.csproj`, `Visor3DTests.csproj`):**
```xml
<PropertyGroup>
    <AssemblyVersion>$(WoldVersion)</AssemblyVersion>
    <FileVersion>$(WoldVersion)</FileVersion>
    <InformationalVersion>$(WoldVersionFull)</InformationalVersion>
</PropertyGroup>
```

* **Generación en compilación vía `Directory.Build.targets`:**
```xml
<Target Name="GenerateWoldInfoVersion" BeforeTargets="BeforeCompile">
    <PropertyGroup>
        <GeneratedVersionFile>$(IntermediateOutputPath)WoldInfoVersion.g.cs</GeneratedVersionFile>
        <VersionFileContent>
// &lt;auto-generated /&gt;
namespace Visor3D.Core
{
    public static class WoldInfoVersion
    {
        public const string Version = "$(WoldVersion)";
        public const string VersionFull = "$(WoldVersionFull)";
        public const string Motor = "$(WoldMotorName)";
    }
}
        </VersionFileContent>
    </PropertyGroup>
    <WriteLinesToFile File="$(GeneratedVersionFile)" Lines="$(VersionFileContent)" Overwrite="true" />
    <ItemGroup>
        <Compile Include="$(GeneratedVersionFile)" />
    </ItemGroup>
</Target>
```

* **Consumo canónico en el código C#:**
```csharp
// RutasLinux.cs / AppExtractor.cs / GodotConfig.cs
public static string Version => WoldInfoVersion.Version;
public static string NombreMotor => WoldInfoVersion.Motor;
```

---

### 3. Supervisor Canónico de Túneles SSH y Modo Degradado (Fase F1)

#### Problema
La gestión de túneles SSH efímeros (proveedores gratuitos como `serveo.net` / `localhost.run`) y túneles `cloudflared` presentaba fragmentación:
- Procesos huérfanos de `ssh.exe` y `cloudflared.exe` permanecían en segundo plano tras cerrar la app.
- Logs dispersos en `Debug.WriteLine`, invisibles en producción.
- Inestabilidad por reintentos descontrolados y falta de supervisión multi-slot.
- La interfaz no informaba al usuario si el túnel operaba en modo óptimo (Cloudflare) o en modo degradado (SSH gratuito con cuotas y latencia).

#### Solución Implementada

#### A. `P2PTunnelSupervisor.cs` (Nuevo Supervisor Singleton Multi-Slot)
Ubicación: `Visor3Ddesarrollo/Visor3D/RedP2P/P2PTunnelSupervisor.cs`
- Controla concurrentemente los slots `p2p`, `webnodo` y `dao`.
- Supervisión periódica de estado, reconexión con backoff y recolección de métricas.

```csharp
namespace Visor3D.RedP2P
{
    public sealed class P2PTunnelSupervisor
    {
        private static readonly Lazy<P2PTunnelSupervisor> _instancia = 
            new Lazy<P2PTunnelSupervisor>(() => new P2PTunnelSupervisor());
        public static P2PTunnelSupervisor Instance => _instancia.Value;

        private readonly ConcurrentDictionary<string, SlotEstado> _slots = new();

        public void RegistrarSlot(string nombreSlot, int puertoLocal, int puertoRemoto)
        {
            _slots[nombreSlot] = new SlotEstado(nombreSlot, puertoLocal, puertoRemoto);
            TunnelLogger.Info($"[Supervisor] Slot '{nombreSlot}' registrado ({puertoLocal} -> {puertoRemoto})");
        }
    }
}
```

#### B. `TunnelProcessRegistry.cs` (Persistencia y Limpieza de Huérfanos)
Ubicación: `Visor3Ddesarrollo/Visor3D/RedP2P/TunnelProcessRegistry.cs`
- Guarda los PIDs en `%LOCALAPPDATA%\WoldVirtualP2P3D\tunnel_pids.json`.
- Al inicio de la aplicación, invoca `MatarHuerfanosPrevios()` para terminar cualquier proceso SSH o Cloudflare remanente de sesiones previas.

#### C. `TunnelLogger.cs` (Diagnóstico Unificado y Rotativo)
Ubicación: `Visor3Ddesarrollo/Visor3D/RedP2P/TunnelLogger.cs`
- Escribe diagnósticos rotativos en `%LOCALAPPDATA%\WoldVirtualP2P3D\logs\tunnel.log`.
- Límite de 5 MB con rotación automática (`tunnel.log.1`).

#### D. Indicador de Modo Degradado en UI (`MainWindow.xaml.cs`)
- Propiedades `EsModoDegradado` / `IsDegradedMode` implementadas en `P2PTunnelManager`, `P2PWebNode` y `WebNodoService`.
- La barra de estado del visor refleja de forma explícita:
```csharp
string modo = manager.EsModoDegradado ? " (Modo Degradado SSH)" : " (Cloudflare)";
TxtEstadoTunel.Text = $"Enlace P2P Activo en {url}{modo}";
```
- Confinamiento de búsqueda de `cloudflared.exe` exclusivamente a `%LOCALAPPDATA%\WoldVirtualP2P3D\bin\`.

---

### 4. Higiene, `.gitignore` y Escena Godot Fija

1. **Escena Principal Anclada:**
   `res://WoldVirtual3DP2P.tscn` (UID `uid://5bh8su08n101`) permanece inalterada y validada con `[OK] FIJADA` mediante `gestor_entorno.py escena-visor`.
2. **Aislamiento en `.gitignore`:**
   Se añadieron patrones para excluir del control de versiones las cachés voluminosas de desarrollo de C# (`.dev/CS/.dotnet/`, `.dev/CS/.nuget/`, `.dev/CS/build/`, `.dev/rebuild287/`).
3. **Reubicación de Documentos:**
   Los prompts de diagnóstico de túneles (`PROMPT_TUNELES_SSH_INESTABLES.md`) se trasladaron desde la raíz a `VersionDesesion/SistemaPrincipal/PRY/Archivo/DocPrincipal/`.

---

### 5. Preguntas Técnicas para el Debate con `DevChatGPTIA`

1. **Integración de `RedP2PServicio`:**
   `P2PTunnelSupervisor` ya está implementado y probado, pero `RedP2PServicio` aún hace llamadas directas a `P2PTunnelManager`. ¿Recomiendas sustituir la instancia en `RedP2PServicio` directamente en este ciclo o mantener un período de transición donde el supervisor envuelva a `P2PTunnelManager`?
2. **Descarga Automatizada de `cloudflared.exe`:**
   Actualmente el visor busca `cloudflared.exe` en `%LOCALAPPDATA%\WoldVirtualP2P3D\bin\`. Si no está, conmuta limpiamente al modo degradado SSH. ¿Convendría que el instalador/lanzador descargue automáticamente el binario oficial firmado si el usuario lo autoriza?
3. **Validación en Linux / Avalonia:**
   Al convertir `RutasLinux.Version` y `NombreMotor` de `const` a `static`, ¿detectas en tu código algún `switch/case` o atributo que requiriera expresiones constantes y que pueda verse afectado en `VisorLinux`?

---
---

## 🇬🇧 PART 2: ENGLISH

### 0. Purpose of this Document

This document submits the architectural changes consolidated on branch `DevAntigravityIDEIA` during the 2026-11-19 session (**38%** completion of the master roadmap) for technical debate and review with `DevChatGPTIA`.

The objective is to review and align on:
1. **`.dev` environment isolation** and elimination of root directory pollution (`.pytest_cache`).
2. **C# Single Source of Truth versioning** across all `.csproj` files and 3D viewer source code.
3. **Canonical SSH Tunnel Supervisor (`P2PTunnelSupervisor`)**, orphan process management (`TunnelProcessRegistry`), rotating file logger (`TunnelLogger`), and visual **degraded mode** UI telemetry.
4. **Repository cleanliness**, modular hierarchy, and strict anchoring of Godot scene `res://WoldVirtual3DP2P.tscn`.

`DevChatGPTIA` is invited to evaluate the code, assess compatibility with ongoing parallel developments, and answer the **Debate Questions (§5)** prior to manual merge execution by the repository owner.

---

### 1. Pytest Cache Containment & Centralized `.dev` Architecture

#### Problem
Executing pytest (via CLI, IDE extensions, or test runners) created `.pytest_cache` directories in the root directory `E:\WoldVirtualP2P3D`, cluttering the source tree.

#### Solution & Code
All pytest cache artifacts were redirected to `VersionDesesion/CONTRRF/RFC/CONTRUCCION/.dev/.pytest_cache` using the standard `PYTEST_ADDOPTS` environment setting.

* **`VersionDesesion/CONTRRF/RFC/CONTRUCCION/.dev/modulos/entorno.py`:**
```python
DIR_PYTEST_CACHE = DIR_DEV / ".pytest_cache"
os.environ["PYTEST_ADDOPTS"] = f"-o cache_dir={DIR_PYTEST_CACHE}"
```

* **Maintenance Subcommand in `gestor_entorno.py`:**
```python
def cmd_pytest_cache(args):
    """Inspects or purges pytest cache inside .dev."""
    cache_dir = DIR_DEV / ".pytest_cache"
    if args.limpiar and cache_dir.exists():
        shutil.rmtree(cache_dir, ignore_errors=True)
        print("[OK] Pytest cache purged inside .dev.")
```
* **Runners `run_pytest.bat` and `run_pytest.ps1`:** configured with `set "PYTEST_ADDOPTS=-o cache_dir=..."` to ensure sandboxing regardless of execution context.

---

### 2. C# Single Source of Truth Versioning (T1-02 Phase B)

#### Problem
Hardcoded legacy version strings and engine executable filenames (`Godot_v4.7.2-stable_win64.exe`) were scattered across `.csproj` files, `RutasLinux.cs`, `AppExtractor.cs`, `GodotConfig.cs`, and `MainWindow.xaml.cs`.

#### Solution & Code
Static string literals were removed and replaced with unified MSBuild properties and an auto-generated static class created at compile time.

* **MSBuild Project Updates (`Visor3D.csproj`, `VisorLinux.csproj`, `Visor3DTests.csproj`):**
```xml
<PropertyGroup>
    <AssemblyVersion>$(WoldVersion)</AssemblyVersion>
    <FileVersion>$(WoldVersion)</FileVersion>
    <InformationalVersion>$(WoldVersionFull)</InformationalVersion>
</PropertyGroup>
```

* **Dynamic Compile-time Target in `Directory.Build.targets`:**
```xml
<Target Name="GenerateWoldInfoVersion" BeforeTargets="BeforeCompile">
    <PropertyGroup>
        <GeneratedVersionFile>$(IntermediateOutputPath)WoldInfoVersion.g.cs</GeneratedVersionFile>
        <VersionFileContent>
// &lt;auto-generated /&gt;
namespace Visor3D.Core
{
    public static class WoldInfoVersion
    {
        public const string Version = "$(WoldVersion)";
        public const string VersionFull = "$(WoldVersionFull)";
        public const string Motor = "$(WoldMotorName)";
    }
}
        </VersionFileContent>
    </PropertyGroup>
    <WriteLinesToFile File="$(GeneratedVersionFile)" Lines="$(VersionFileContent)" Overwrite="true" />
    <ItemGroup>
        <Compile Include="$(GeneratedVersionFile)" />
    </ItemGroup>
</Target>
```

* **C# Consumption:**
```csharp
public static string Version => WoldInfoVersion.Version;
public static string NombreMotor => WoldInfoVersion.Motor;
```

---

### 3. Canonical SSH Tunnel Supervisor & Degraded Mode (Phase F1)

#### Problem
Ephemeral SSH tunnel management (public providers such as `serveo.net` / `localhost.run`) and `cloudflared` services suffered from fragmentation:
- Leaked `ssh.exe` and `cloudflared.exe` orphaned background processes.
- Unmonitored logs written only to `Debug.WriteLine`.
- Cascading failures without multi-slot supervision.
- Lack of user awareness when the connection degraded to public fallback SSH proxies.

#### Solution & Code

#### A. `P2PTunnelSupervisor.cs` (Multi-Slot Singleton Supervisor)
Path: `Visor3Ddesarrollo/Visor3D/RedP2P/P2PTunnelSupervisor.cs`
- Centralized tracking for `p2p`, `webnodo`, and `dao` slots.
- Built-in status queries, watchdog hooks, and exponential backoff.

#### B. `TunnelProcessRegistry.cs` (PID Persistence & Orphan Cleanup)
Path: `Visor3Ddesarrollo/Visor3D/RedP2P/TunnelProcessRegistry.cs`
- Persists process IDs to `%LOCALAPPDATA%\WoldVirtualP2P3D\tunnel_pids.json`.
- Terminates stale background processes via `MatarHuerfanosPrevios()` on startup.

#### C. `TunnelLogger.cs` (Thread-safe Rotating Logger)
Path: `Visor3Ddesarrollo/Visor3D/RedP2P/TunnelLogger.cs`
- Writes to `%LOCALAPPDATA%\WoldVirtualP2P3D\logs\tunnel.log` (5 MB max with auto-rotation).

#### D. Degraded Mode UI Telemetry (`MainWindow.xaml.cs`)
- Added `EsModoDegradado` / `IsDegradedMode` and `ProveedorActivo` to `P2PTunnelManager`, `P2PWebNode`, and `WebNodoService`.
- Status bar displays real-time connection tier:
```csharp
string modo = manager.EsModoDegradado ? " (Modo Degradado SSH)" : " (Cloudflare)";
TxtEstadoTunel.Text = $"Enlace P2P Activo en {url}{modo}";
```
- Confined `cloudflared.exe` binary discovery strictly to `%LOCALAPPDATA%\WoldVirtualP2P3D\bin\`.

---

### 4. Cleanliness, `.gitignore` & Pinned Godot Scene

1. **Scene Anchoring:**
   `res://WoldVirtual3DP2P.tscn` (UID `uid://5bh8su08n101`) validated with `[OK] FIJADA` via `gestor_entorno.py escena-visor`.
2. **`.gitignore` Rules:**
   Ignored heavy development caches (`.dev/CS/.dotnet/`, `.dev/CS/.nuget/`, `.dev/CS/build/`, `.dev/rebuild287/`).
3. **Documentation Organization:**
   Diagnostic documents moved to `VersionDesesion/SistemaPrincipal/PRY/Archivo/DocPrincipal/`.

---

### 5. Technical Questions for `DevChatGPTIA` Review

1. **`RedP2PServicio` Integration Strategy:**
   `P2PTunnelSupervisor` is fully operational. Should `RedP2PServicio` be refactored to consume `P2PTunnelSupervisor.Instance` directly in this cycle, or should the supervisor temporarily wrap `P2PTunnelManager` during a transition period?
2. **`cloudflared.exe` Auto-provisioning:**
   The viewer currently inspects `%LOCALAPPDATA%\WoldVirtualP2P3D\bin\` and cleanly falls back to degraded SSH mode if absent. Would you recommend automated binary download into LocalAppData during installation/first run?
3. **Avalonia / Linux Compatibility:**
   When migrating `RutasLinux.Version` and `NombreMotor` to `static` properties, have you identified any compile-time constant requirements (e.g. `switch/case` patterns) in `VisorLinux`?

---

### 6. Verification Status

| Metric | Result |
|--------|--------|
| **C# Build (`dotnet build`)** | **0 errors**, 1 warning (CS8602 nullable reference handled) |
| **Pytest Suite (`test_version_unica.py` & full suite)** | **34 passed, 1 skipped (100% green)** |
| **Main Scene (`WoldVirtual3DP2P.tscn`)** | **[OK] FIJADA (`uid://5bh8su08n101`)** |
| **Git Remote State** | **Pushed to `origin/DevAntigravityIDEIA` (`35c3c6a4`)** |

> ⚠️ **Important Reminder:** The final merge into `DevChatGPTIA` or `main` will be performed **manually by `@Chicook`**.
