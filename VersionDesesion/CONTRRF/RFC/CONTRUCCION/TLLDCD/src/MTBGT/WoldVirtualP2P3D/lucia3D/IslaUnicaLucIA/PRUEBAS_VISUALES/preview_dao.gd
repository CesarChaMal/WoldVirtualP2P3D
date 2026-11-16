extends Node3D
## Prueba visual de la Isla Única LucIA (DAO): única, en el origen, con su
## malla GLB, su carta DAO y las cuatro estaciones de registro.
##
## Uso:
##   godot --path <proyecto> res://lucia3D/IslaUnicaLucIA/PRUEBAS_VISUALES/PreviewDAO.tscn
## Guarda capturas en res://lucia3D/IslaUnicaLucIA/PRUEBAS_VISUALES/capturas/
## y termina con código 1 si alguna comprobación falla.

const ESCENA_DAO := "res://lucia3D/IslaUnicaLucIA/ESCENAS/IslaUnicaLucIA.tscn"
const SALIDA := "res://lucia3D/IslaUnicaLucIA/PRUEBAS_VISUALES/capturas"
const NIVEL_MAR := 39.2
const GRUPO := "isla_unica_lucia"

var _dao: Node3D = null
var _fallos: Array[String] = []


func _ready() -> void:
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(SALIDA))
	_entorno()
	_oceano()
	_cargar_dao()
	await _validar()
	await _capturar_todo()
	print("[PRUEBA] fallos: %d" % _fallos.size())
	get_tree().quit(0 if _fallos.is_empty() else 1)


func _entorno() -> void:
	var cielo := ProceduralSkyMaterial.new()
	cielo.sky_top_color = Color(0.14, 0.3, 0.52)
	cielo.sky_horizon_color = Color(0.6, 0.71, 0.8)
	cielo.ground_bottom_color = Color(0.07, 0.11, 0.15)
	cielo.ground_horizon_color = Color(0.28, 0.36, 0.42)
	var sky := Sky.new()
	sky.sky_material = cielo
	var env := Environment.new()
	env.background_mode = Environment.BG_SKY
	env.sky = sky
	env.ambient_light_source = Environment.AMBIENT_SOURCE_SKY
	env.ambient_light_energy = 0.9
	env.tonemap_mode = Environment.TONE_MAPPER_FILMIC
	var mundo := WorldEnvironment.new()
	mundo.environment = env
	add_child(mundo)

	var sol := DirectionalLight3D.new()
	sol.rotation_degrees = Vector3(-48.0, -38.0, 0.0)
	sol.light_energy = 1.5
	sol.shadow_enabled = true
	add_child(sol)


func _oceano() -> void:
	var agua := MeshInstance3D.new()
	agua.name = "MarPrueba"
	var plano := PlaneMesh.new()
	plano.size = Vector2(6000.0, 6000.0)
	agua.mesh = plano
	var material := StandardMaterial3D.new()
	material.albedo_color = Color(0.05, 0.22, 0.33)
	material.metallic = 0.35
	material.roughness = 0.15
	agua.material_override = material
	agua.position = Vector3(0.0, NIVEL_MAR, 0.0)
	add_child(agua)


func _cargar_dao() -> void:
	if not ResourceLoader.exists(ESCENA_DAO):
		_fallos.append("No existe la escena " + ESCENA_DAO)
		return
	var recurso := load(ESCENA_DAO)
	if not (recurso is PackedScene):
		_fallos.append("La escena DAO no carga como PackedScene")
		return
	_dao = (recurso as PackedScene).instantiate() as Node3D
	if _dao == null:
		_fallos.append("No se pudo instanciar la escena DAO")
		return
	add_child(_dao)


func _validar() -> void:
	await get_tree().process_frame
	await get_tree().process_frame
	if _dao == null:
		return

	var grupo := get_tree().get_nodes_in_group(GRUPO)
	_check(grupo.size() == 1, "isla única en el grupo", "nodos=%d" % grupo.size())
	_check(_dao.global_position.is_equal_approx(Vector3.ZERO), "anclada en el origen",
			str(_dao.global_position))
	_check(bool(_dao.get("malla_cargada")), "malla GLB cargada",
			"usa plataforma de respaldo" if not bool(_dao.get("malla_cargada")) else "ok")

	var carta: Dictionary = _dao.get("datos_dao")
	_check(not carta.is_empty(), "carta DAO leída", "claves=%d" % carta.size())
	_check(str(carta.get("id", "")) == str(_dao.get("id_dao")), "id de carta coincide",
			"id=%s" % str(carta.get("id", "")))
	print("[PRUEBA] sede de LucIA: ", _dao.call("sede_lucia"))
	_medir_mallas(_dao)
	await _sondear_suelo()

	var plaza = _dao.call("plaza_registro")
	_check(plaza != null, "plaza de registro presente")
	if plaza == null:
		return

	var claves: Array = plaza.call("estaciones")
	_check(claves.size() == 4, "cuatro estaciones", str(claves))
	await get_tree().physics_frame
	await get_tree().physics_frame
	for clave in claves:
		var marcador: Marker3D = plaza.call("marcador", clave)
		if marcador == null:
			_check(false, "estación %s" % clave, "marcador ausente")
			continue
		print("[PRUEBA] estación %s en %s" % [clave, marcador.global_position])
		_check(marcador.get_child_count() >= 2, "estación %s con piso y etiqueta" % clave,
				"hijos=%d" % marcador.get_child_count())
		var suelo := _altura_suelo(marcador.global_position)
		_check(absf(suelo - marcador.global_position.y) < 1.5,
				"estación %s apoyada en el terreno" % clave,
				"suelo=%.2f marcador=%.2f" % [suelo, marcador.global_position.y])

	# El trámite completo debe cerrar en las cuatro estaciones.
	for clave in claves:
		plaza.call("abrir", clave)
		plaza.call("completar", clave, {"prueba": true})
	_check(bool(plaza.call("registro_completo")), "registro de las 4 estaciones completo",
			"progreso=%d" % int(plaza.call("progreso")))
	plaza.call("reiniciar")


func _capturar_todo() -> void:
	if _dao == null:
		return
	await _vista("01_aerea", Vector3(150, 165, 150), Vector3(0, 42, 0), 60.0)
	await _vista("02_plaza_cenital", Vector3(0, 320, 40), Vector3(0, 40, 0), 40.0)
	await _vista("03_llegada_norte", Vector3(0, 45, -48), Vector3(0, 44, 0), 65.0)
	await _vista("04_estacion_este", Vector3(50, 49, 8), Vector3(34, 42, 0), 55.0)
	await _vista("05_perfil_mar", Vector3(185, 46, 0), Vector3(-20, 41, 0), 40.0)
	await _vista("06_llegada_sur", Vector3(0, 49, 52), Vector3(0, 42, 0), 60.0)


func _vista(nombre: String, desde: Vector3, hacia: Vector3, fov: float) -> void:
	var cam := Camera3D.new()
	cam.name = "Cam_" + nombre
	cam.fov = fov
	cam.far = 3000.0
	add_child(cam)
	cam.global_position = desde
	cam.look_at(hacia, Vector3.UP)
	cam.current = true
	for i in 4:
		await get_tree().process_frame
	await RenderingServer.frame_post_draw
	var imagen := get_viewport().get_texture().get_image()
	var ruta := SALIDA + "/" + nombre + ".png"
	var error := imagen.save_png(ruta)
	_check(error == OK, "captura " + nombre, "error=%d" % error)
	cam.queue_free()


func _medir_mallas(nodo: Node, sangria: String = "") -> void:
	if nodo is MeshInstance3D and (nodo as MeshInstance3D).mesh != null:
		var caja: AABB = (nodo as MeshInstance3D).global_transform * (nodo as MeshInstance3D).mesh.get_aabb()
		if caja.size.y > 1.0:
			print("[PRUEBA] malla %s%s centro=%s tamanio=%s" % [sangria, nodo.name,
					caja.get_center(), caja.size])
	for hijo in nodo.get_children():
		_medir_mallas(hijo, sangria + "-")


func _sondear_suelo() -> void:
	await get_tree().physics_frame
	await get_tree().physics_frame
	for radio in [36, 50, 60]:
		var puntos := [Vector2(0, -radio), Vector2(radio, 0), Vector2(0, radio), Vector2(-radio, 0)]
		var nombres := ["N", "E", "S", "O"]
		for i in puntos.size():
			var p: Vector2 = puntos[i]
			var altura := _altura_suelo(Vector3(p.x, 0, p.y))
			print("[PRUEBA] suelo r=%d %s y=%.2f" % [radio, nombres[i], altura])


func _altura_suelo(punto: Vector3) -> float:
	var espacio := get_world_3d().direct_space_state
	var consulta := PhysicsRayQueryParameters3D.create(
			Vector3(punto.x, 200, punto.z), Vector3(punto.x, -40, punto.z))
	var golpe := espacio.intersect_ray(consulta)
	return float(golpe["position"].y) if not golpe.is_empty() else NAN


func _check(ok: bool, titulo: String, detalle: String = "") -> void:
	if ok:
		print("[PRUEBA] OK   %s %s" % [titulo, detalle])
	else:
		_fallos.append(titulo)
		print("[PRUEBA] FALLO %s %s" % [titulo, detalle])
