extends Node
## Arranca primero para montar la base 2.8.6 antes de cargar autoloads y la escena.
## El PCK principal de 2.8.7 contiene solo el delta DAO y tiene prioridad sobre la base.

func _init() -> void:
	var ruta_base := OS.get_environment("WOLD_BASE_PCK").strip_edges()
	if ruta_base.is_empty() or not FileAccess.file_exists(ruta_base):
		push_error("[DAOBasePackLoader] No se encontró el PCK base 2.8.6: " + ruta_base)
		return

	if not ProjectSettings.load_resource_pack(ruta_base, false):
		push_error("[DAOBasePackLoader] No se pudo montar el PCK base 2.8.6")
		return

	print("[DAOBasePackLoader] Base 2.8.6 montada; el PCK delta DAO 2.8.7 mantiene prioridad")
