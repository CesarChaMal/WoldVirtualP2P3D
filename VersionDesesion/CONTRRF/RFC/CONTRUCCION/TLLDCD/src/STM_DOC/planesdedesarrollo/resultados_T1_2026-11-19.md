# Resultados T1 — tramo inicial del plan (DevQoderIA, 2026-11-19)

Primer ~18% del [plan_desarrollo_boy_2026-11-19.md](plan_desarrollo_boy_2026-11-19.md).
Todo sobre DevQoderIA; sin merge a main. Verificación real ejecutada en cada punto.

## T1-04 · Triar `transport.py` (veredicto)

`STM_HRTS/STM_DEF/utils/transport.py` **no es un vector vivo**: es el stub
neutralizado del incidente R-01 (hallazgo **R01b**). Su docstring lo declara:
«el cargador remoto fue retirado; se conserva el nombre para que un reintento
de reinyección resulte evidente en el diff». La función `cargar()` lanza
`RuntimeError` si alguien intenta reutilizarla. **No se toca**: es evidencia
intencional, no deuda a eliminar.

## T1-05 · Localizar `SshTunnelWatchdog.cs` (veredicto)

No está en el HEAD de DevQoderIA, pero **sí existe en el historial** (commits
`a7288cc5` / `039b5fa7`), solo en la rama **DevAntigravityIDEIA** (y su backup
`backup/pre-update-20261119/DevAntigravityIDEIA`). Conclusión: el watchdog SSH
se implementó en otra rama y **nunca se integró a DevQoderIA** — caso concreto
de la deriva B-04. El trabajo de unificación del plan no puede apoyarse en él
desde esta rama; hay que portarlo o reimplementarlo.

## T1-03 · Inventario de copias del árbol C#

Conteo de `.cs` (bajo `VSC#/Visor3Ddesarrollo`):

| Árbol | `.cs` | Nota |
|---|---|---|
| `HerraMientas/` | 72 | visor activo (build real) |
| `Actualizaciones/LCDMWoldVirtualP2P3Dv2.8.5/` | 130 | snapshot completo, viejo |
| `Actualizaciones/LCDMWoldVirtualP2P3Dv2.8.6/` | 142 | snapshot completo, viejo |
| `Actualizaciones/DAOLCDMWoldVirtualP2P3Dv2.9.3/` | 1 | fragmento |
| `VisorLinux/` | 27 | proyecto hermano Avalonia |
| `Chat/` `RedP2P/` `Services/` `Seguridad/` `p2pipfsCS/` | 7/5/12/37/6 | módulos compartidos |

Duplicados confirmados (3 copias cada uno): `AppExtractor.cs`, `GodotConfig.cs`,
`MainWindow.xaml.cs` en `HerraMientas/` **y** en ambos snapshots 2.8.5/2.8.6.
Los snapshots hardcodean `Godot_v4.7.2-stable_win64.exe` (ver T1-02 abajo).

## T1-02 · Versión única (B-02 / B-04) — parcial, verificada

Estado previo: el sistema U5.1 estaba **diseñado y testeado pero sin artefactos**
(`version_unica.py` + `test_version_unica.py` existían; faltaban `VERSION.json`,
`Directory.Build.props` y `Directory.Build.targets`, y las rutas habían derivado).

Hecho hoy (verificado):

- `src/VERSION.json` creado: `version 2.9.5`, `motor 4.7.2`, ancla del PCK real
  `CopiaDeSeguridad/wvp3d.pck` (sha256 `9c943921…`, 509.257.972 B).
- `VSC#/Directory.Build.props` creado: lee la tabla con `ReadAllText` y expone
  `$(WoldVersion)` / `$(WoldMotor)` / `$(WoldVersionFull)`; guardas fail-closed
  (`<Error>` si falta la tabla o el campo).
- `VSC#/Directory.Build.targets` creado: materializa `WoldInfoVersion` en
  `obj/WoldInfoVersion.g.cs` (tarea inline Roslyn; `WriteLinesToFile` recortaba
  los `;`). El fichero generado es C# válido.
- Verificado: `version_unica.py --leer` → `2.9.5`;
  `dotnet msbuild -getProperty:WoldVersion` → `2.9.5`;
  `dotnet build -p:SkipGodotAssets=true` → **0 errores**.

Pendiente (fase B, siguiente sesión) — listado exacto de `--comprobar`:

- `version_unica.CSPROJ` apunta a rutas viejas (`Visor3D.csproj`, `Nucleo/Nucleo.csproj`
  inexistentes): reescribir a `Visor3Ddesarrollo/HerraMientas/Visor3D.csproj`,
  `Visor3Ddesarrollo/VisorLinux/VisorLinux.csproj`, `Visor3Ddesarrollo/Tests/Visor3DTests.csproj`.
- Convertir literales de versión en los csproj: `HerraMientas/Visor3D.csproj`
  (AssemblyName/Product/Version/AssemblyVersion/FileVersion/Title/Description con `v2.9.5`)
  y `VisorLinux/VisorLinux.csproj` (`<Version>2.8.0</Version>`).
- `RutasLinux.cs` (líneas 15-16) lleva `Version = "2.7.9"` y `NombreMotor`
  literal: derivar de `WoldInfoVersion` y cablear el `.g.cs` al Compile de VisorLinux.
- Motor `Godot_v4.7.2-stable_win64.exe` hardcodeado en: `HerraMientas/AppExtractor.cs`
  (60, 173), `HerraMientas/GodotConfig.cs` (204), `HerraMientas/MainWindow.xaml.cs`
  (1683, 1685, 1707) y las mismas 3 posiciones en los snapshots 2.8.5/2.8.6.
- Al final: `test_version_unica.py` debe quedar verde (hoy sigue rojo por la deriva;
  por eso el CI raíz lo excluye con `-k "not version_unica"`).

## T1-01 · CI raíz

Creados `.github/workflows/ci.yml` y `.github/workflows/stm_def.yml` (raíz del
repo, único lugar que GitHub Actions ejecuta). Corrigen la ruta canónica
`VersionDesesion/CONTRRF/RFC/CONTRUCCION/TLLDCD/src` (la plantilla anidada usaba
`CONTRRF/TLLDCD/src`). Jobs: puerta STM_DEF (reutilizable), Python (H1 +
STM_DEF estrictas + suite completa no bloqueante) y C# (build `SkipGodotAssets=true`
+ 4 selftests). La suite completa excluye `version_unica` hasta cerrar la fase B.

## T2-05 · Regex y opciones SSH del túnel

- `p2pipfsCS/P2PWebNode.cs:232`: `@"https://[\w\-]+\.lhr\\.rocks"` → `@"https://[\w\-]+\.lhr\.life"`
  (doble barra + dominio obsoleto; localhost.run ya publica `.lhr.life`).
- Añadido `-o ServerAliveCountMax=3 -o ExitOnForwardFailure=yes` a los 3 candidatos
  SSH de `P2PWebNode.cs` y a los 2 de `MainWindow.xaml.cs` (4400-4401), alineándolos
  con `RedP2P/P2PTunnelManager.cs` que ya los tenía.
