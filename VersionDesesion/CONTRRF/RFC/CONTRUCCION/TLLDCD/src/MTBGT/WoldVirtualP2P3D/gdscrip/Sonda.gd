extends Node
## Valida el transporte y la respuesta RPC sin cerrar la sesión aceptada.
const Red = preload("res://gdscrip/Red.gd")
const EcoMundo = preload("res://gdscrip/EcoMundo.gd")
var _conectado := false
var _fallo := false
var _recibido := false
var _world_ready := false
var _plazas := {}
var _eco: Node = null
var _abierta := false


## API temporal anterior; nunca sustituye una sesión ya validada.
func consultar(timeout_ms: int = 2500) -> Array:
	if Red.sesion_validada():
		if _abierta:
			return _indices(_plazas)
		var actual: Dictionary = await validar_conexion(timeout_ms)
		return actual.get("ocupados", [])
	if _abierta:
		return []
	var objetivo := Red.resolver_objetivo()
	if bool(objetivo.get("force_host", false)):
		return []
	var resultado: Dictionary = await _validar(objetivo, timeout_ms, false)
	var ocupados: Array = resultado.get("ocupados", [])
	_cerrar()
	return ocupados


func validar_conexion(timeout_ms: int = 10000) -> Dictionary:
	if _abierta:
		return _resultado(false, "Validación en curso")
	return await _validar(Red.resolver_objetivo(), timeout_ms, true)


func _resultado(ok: bool, error: String = "") -> Dictionary:
	return {"ok": ok, "error": error, "ocupados": _indices(_plazas) if ok else []}


func _validar(objetivo: Dictionary, timeout_ms: int, conservar: bool) -> Dictionary:
	_abierta = true
	_fallo = false
	_recibido = false
	_world_ready = false
	_plazas = {}
	_preparar_eco()
	if not multiplayer.connected_to_server.is_connected(_on_conectado):
		multiplayer.connected_to_server.connect(_on_conectado)
		multiplayer.connection_failed.connect(_on_fallo)
		multiplayer.server_disconnected.connect(_on_fallo)
	var existente = multiplayer.multiplayer_peer
	var reutilizar := Red.sesion_validada() and existente is WebSocketMultiplayerPeer
	if reutilizar and existente.get_connection_status() != MultiplayerPeer.CONNECTION_CONNECTED:
		_cerrar()
		return _resultado(false, "La sesión validada se desconectó")
	if not reutilizar:
		var peer := WebSocketMultiplayerPeer.new()
		var err: int
		if bool(objetivo.get("force_host", false)) and conservar:
			err = peer.create_server(int(objetivo.get("port", Red.PUERTO_WS)))
		else:
			var url := Red.resolver_url_ws(str(objetivo.get("host", "127.0.0.1")),
				int(objetivo.get("port", Red.PUERTO_WS)), str(objetivo.get("invitacion", "")))
			err = peer.create_client(url)
		if err == OK:
			multiplayer.multiplayer_peer = peer
		var limite := Time.get_ticks_msec() + timeout_ms
		while err == OK and peer.get_connection_status() == MultiplayerPeer.CONNECTION_CONNECTING and not _fallo and Time.get_ticks_msec() < limite:
			await get_tree().create_timer(0.05).timeout
		if err != OK or _fallo or peer.get_connection_status() != MultiplayerPeer.CONNECTION_CONNECTED:
			_cerrar()
			var host := str(objetivo.get("host", "127.0.0.1"))
			if conservar and not bool(objetivo.get("force_host", false)) and not bool(objetivo.get("is_client", false)) and host in ["127.0.0.1", "localhost", ""]:
				objetivo["force_host"] = true
				return await _validar(objetivo, timeout_ms, conservar)
			return _resultado(false, "No se pudo conectar al destino WebSocket")
	if multiplayer.is_server():
		_pedir_turnos()
	else:
		rpc_id(1, "_pedir_turnos")
	var limite_rpc := Time.get_ticks_msec() + timeout_ms
	while not _recibido and not _fallo and Time.get_ticks_msec() < limite_rpc:
		await get_tree().create_timer(0.05).timeout
	if not _recibido or _fallo:
		_cerrar()
		return _resultado(false, "El anfitrión no respondió a la consulta RPC")
	if conservar:
		Red.guardar_validacion(objetivo)
	_abierta = false
	return _resultado(true)


## El cliente conserva el socket mientras el anfitrión termina su alta.
func esperar_mundo(timeout: float = 180.0) -> Dictionary:
	_preparar_eco()
	if multiplayer.multiplayer_peer == null or multiplayer.multiplayer_peer.get_connection_status() != MultiplayerPeer.CONNECTION_CONNECTED:
		return _resultado(false, "Sesión desconectada antes de entrar al mundo")
	if multiplayer.is_server():
		return _resultado(true)
	var limite := Time.get_ticks_msec() + int(timeout * 1000.0)
	while Time.get_ticks_msec() < limite:
		if multiplayer.multiplayer_peer.get_connection_status() != MultiplayerPeer.CONNECTION_CONNECTED:
			return _resultado(false, "El anfitrión se desconectó")
		_recibido = false
		_world_ready = false
		rpc_id(1, "_pedir_turnos")
		var respuesta := mini(limite, Time.get_ticks_msec() + 2000)
		while not _recibido and Time.get_ticks_msec() < respuesta:
			await get_tree().create_timer(0.05).timeout
		if _recibido and _world_ready:
			return _resultado(true)
		await get_tree().create_timer(0.5).timeout
	return _resultado(false, "El anfitrión no preparó el mundo a tiempo")


@rpc("any_peer", "reliable")
func _pedir_turnos() -> void:
	if not multiplayer.is_server():
		return
	var sender := multiplayer.get_remote_sender_id()
	var mundo := get_parent().get_node_or_null("ISLA3Dblock")
	var listo := mundo != null and mundo.has_method("_claim_island")
	var plazas := {}
	if listo:
		if sender > 0 and mundo.has_method("marcar_sonda"):
			mundo.call("marcar_sonda", sender)
		if mundo.has_method("turnos_snapshot"):
			plazas = mundo.call("turnos_snapshot")
	if sender == 0:
		_turnos_recibidos(plazas, listo)
	else:
		rpc_id(sender, "_turnos_recibidos", plazas, listo)


@rpc("authority", "reliable")
func _turnos_recibidos(plazas: Dictionary, world_ready: bool = false) -> void:
	_plazas = plazas.duplicate()
	_world_ready = world_ready
	_recibido = true


func _on_conectado() -> void:
	_conectado = true


func _on_fallo() -> void:
	_fallo = true


func _preparar_eco() -> void:
	if get_parent().get_node_or_null("ISLA3Dblock") != null:
		return
	_eco = EcoMundo.new()
	_eco.name = "ISLA3Dblock"
	var eco_sync := EcoMundo.EcoPlayerSync.new()
	eco_sync.name = "PlayerSync"
	_eco.add_child(eco_sync)
	get_parent().add_child(_eco)


func liberar_eco() -> void:
	if is_instance_valid(_eco):
		_eco.free()
	_eco = null


func _cerrar() -> void:
	if multiplayer.connected_to_server.is_connected(_on_conectado):
		multiplayer.connected_to_server.disconnect(_on_conectado)
	if multiplayer.connection_failed.is_connected(_on_fallo):
		multiplayer.connection_failed.disconnect(_on_fallo)
	if multiplayer.server_disconnected.is_connected(_on_fallo):
		multiplayer.server_disconnected.disconnect(_on_fallo)
	if multiplayer.multiplayer_peer != null:
		multiplayer.multiplayer_peer.close()
	multiplayer.multiplayer_peer = null
	Red.limpiar_validacion()
	liberar_eco()
	_abierta = false


func _indices(plazas: Dictionary) -> Array:
	var ocupados := []
	for k in plazas.keys():
		var i := int(k)
		if i >= 0:
			ocupados.append(i)
	ocupados.sort()
	return ocupados
