$ErrorActionPreference = "Stop"

# Raíz del taller vivo: HerraMientas -> Visor3Ddesarrollo -> VSC# -> src
$src = (Resolve-Path (Join-Path $PSScriptRoot "..\..\..")).Path

$py = Join-Path $src "LC_STM\python\pythonw.exe"
if (-not (Test-Path $py)) {
    if ($env:WVP_PYTHONW) { $py = $env:WVP_PYTHONW }
    elseif ($env:WVP_PYTHON) { $py = $env:WVP_PYTHON -replace 'python\.exe$', 'pythonw.exe' }
    else {
        $cmd = Get-Command pythonw -ErrorAction SilentlyContinue
        if (-not $cmd) { $cmd = Get-Command python -ErrorAction SilentlyContinue }
        if ($cmd) { $py = $cmd.Source }
    }
}

$srv = Join-Path $src "MTBGT\WoldVirtualP2P3D\lucia3D\GLTF_lucIA\celebro_3D\puente_3D\servidor_lucia.py"

if (-not (Test-Path $py)) { throw "No se encuentra Python para LucIA: $py" }
if (-not (Test-Path $srv)) { throw "No se encuentra el servidor de LucIA: $srv" }

$env:PYTHONPATH = "$src;$env:PYTHONPATH"
$env:WVP_SRC_DIR = $src

$log = Join-Path $env:LOCALAPPDATA "WoldVirtualP2P3D\logs\lucia_ps.log"
New-Item -ItemType Directory -Force -Path (Split-Path $log) | Out-Null

Start-Process -FilePath $py `
    -ArgumentList @("`"$srv`"", "--modelo", "qwen2.5:7b", "--puerto", "8765", "--sin-precalentar") `
    -WorkingDirectory (Split-Path $srv) `
    -WindowStyle Hidden `
    -Wait `
    -RedirectStandardOutput $log `
    -RedirectStandardError "$log.err"
