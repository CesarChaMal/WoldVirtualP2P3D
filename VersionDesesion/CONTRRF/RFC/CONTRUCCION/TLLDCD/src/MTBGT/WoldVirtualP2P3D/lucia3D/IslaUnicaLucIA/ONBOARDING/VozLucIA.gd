extends Node
## Voz oficial de LucIA para el alta de la DAO.
##
## Reproduce el WAV pregrabado con el sintetizador del sistema general
## (`voz_lucia.py`: gTTS es-ES + DSP 1.16x). Si una frase no está pregrabada,
## avisa al llamador para que use la voz nativa al momento y la sintetiza en
## segundo plano para dejarla en caché (`user://voz_lucia`).

const DIR_FIJA := "res://lucia3D/IslaUnicaLucIA/ONBOARDING/voz"
const DIR_CACHE := "user://voz_lucia"
const RUTA_SCRIPT := "res://lucia3D/GLTF_lucIA/celebro_3D/puente_3D/voz_lucia.py"

var _reproductor: AudioStreamPlayer
var _python := ""
var _cola: Array[String] = []
var _sintetizando := false
var _pid := -1
var _destino := ""


func _ready() -> void:
	_reproductor = AudioStreamPlayer.new()
	_reproductor.name = "ReproductorVozLucIA"
	add_child(_reproductor)
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(DIR_CACHE))
	_python = _buscar_python()


## Devuelve true si ya estaba disponible el audio oficial y se está oyendo.
func decir(texto: String) -> bool:
	if _reproductor == null:
		return false
	_reproductor.stop()
	var ruta := _ruta_wav(texto)
	if ruta.is_empty():
		_pedir_sintesis(texto)
		return false
	print("[VozLucIA] voz oficial: ", ruta.get_file())
	var audio := _cargar(ruta)
	if audio == null:
		return false
	_reproductor.stream = audio
	_reproductor.play()
	return true


func _process(_delta: float) -> void:
	if not _sintetizando:
		return
	if FileAccess.file_exists(_destino):
		_sintetizando = false
		_siguiente()
		return
	if _pid > 0 and not OS.is_process_running(_pid):
		# Falló o no hay red: se descarta la frase y sigue la cola.
		_sintetizando = false
		_siguiente()


func _ruta_wav(texto: String) -> String:
	var nombre := texto.sha256_text() + ".wav"
	var fija := DIR_FIJA + "/" + nombre
	if ResourceLoader.exists(fija):
		return fija
	var cache := DIR_CACHE + "/" + nombre
	if FileAccess.file_exists(cache):
		return cache
	return ""


func _cargar(ruta: String) -> AudioStream:
	if ruta.begins_with("res://"):
		return load(ruta) as AudioStream
	return AudioStreamWAV.load_from_file(ruta)


## True mientras el WAV oficial sigue reproduciéndose.
func esta_hablando() -> bool:
	return _reproductor != null and _reproductor.playing


func callar() -> void:
	if _reproductor != null:
		_reproductor.stop()


func _pedir_sintesis(texto: String) -> void:
	if _python.is_empty() or _cola.has(texto):
		return
	_cola.append(texto)
	_siguiente()


func _siguiente() -> void:
	if _sintetizando or _cola.is_empty():
		return
	var texto: String = _cola.pop_front()
	if not _ruta_wav(texto).is_empty():
		_siguiente()
		return
	_destino = ProjectSettings.globalize_path(
			DIR_CACHE + "/" + texto.sha256_text() + ".wav")
	var guion := ProjectSettings.globalize_path(RUTA_SCRIPT)
	if not FileAccess.file_exists(guion):
		return
	_pid = OS.create_process(_python, [guion, "--texto", texto, "--salida", _destino])
	if _pid <= 0:
		return
	_sintetizando = true


func _buscar_python() -> String:
	var entorno := OS.get_environment("WVP_PYTHON")
	if not entorno.is_empty() and FileAccess.file_exists(entorno):
		return entorno
	if not FileAccess.file_exists(ProjectSettings.globalize_path(RUTA_SCRIPT)):
		return ""
	var raiz := ProjectSettings.globalize_path("res://")
	for relativa in [
		"../../../../.dev/.python/python.exe",
		"../../../../.dev/venv/Scripts/python.exe",
		"LC_STM/python/python.exe",
		"LC_STM/python/pythonw.exe",
	]:
		var ruta := raiz.path_join(relativa).simplify_path()
		if FileAccess.file_exists(ruta):
			return ruta
	return ""
