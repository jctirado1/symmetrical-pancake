class_name Pedestrian
extends AnimatableBody3D
## One person walking around the city. The Pedestrians node creates these,
## so you don't place them yourself.

var manager: Pedestrians
var model: Resource = null
var flip_model := false
var walk_speed := 1.4
var height := 1.75
var side_offset := 0.6  ## How far left of the pavement's middle line they keep.

## True while crossing a road, so traffic knows to stop.
var on_road := false
var _from: Vector3i
var _to: Vector3i
var _start := Vector3.ZERO
var _end := Vector3.ZERO
var _progress := 0.0  # Metres walked along the current stretch.
var _waiting := false
var _waited := 0.0
var _blocked_time := 0.0
var _dodge := Vector3.ZERO
var _dodge_time := 0.0
var _shaken_time := 0.0  # Standing still for a moment after jumping clear.
var _anim_player: AnimationPlayer
var _walk_anim := ""
var _idle_anim := ""
var _body: Node3D
var _bob_time := 0.0

const COLOURS := [
	Color(0.15, 0.15, 0.2), Color(0.5, 0.1, 0.1), Color(0.2, 0.3, 0.5), Color(0.6, 0.55, 0.45),
	Color(0.25, 0.35, 0.25), Color(0.7, 0.7, 0.7), Color(0.4, 0.25, 0.15), Color(0.8, 0.6, 0.2),
]
const SKIN := [Color(0.95, 0.8, 0.65), Color(0.8, 0.6, 0.45), Color(0.55, 0.38, 0.25), Color(0.35, 0.24, 0.16)]


func _ready() -> void:
	sync_to_physics = false
	add_to_group("pedestrians")
	# Layer 2 = pedestrians: you bump into them on foot, but cars pass through
	# (people jump out of the way instead).
	collision_layer = 2
	collision_mask = 0
	_build_body()


## Starts walking from corner [param from] to [param to]. [param fraction]
## (0 to 1) says how far along the way to start.
func start_walk(from: Vector3i, to: Vector3i, crosses_road: bool, fraction := 0.0) -> void:
	_from = from
	_to = to
	on_road = false
	_waiting = crosses_road  # Look both ways before crossing.
	var a := manager.corner_position(from)
	var b := manager.corner_position(to)
	# Keep to the left of the pavement's middle, so people coming the other
	# way pass each other.
	var left := Vector3.UP.cross((b - a).normalized())
	_start = a + left * side_offset
	_end = b + left * side_offset
	_progress = _start.distance_to(_end) * fraction
	global_position = _start.lerp(_end, fraction)



func _physics_process(delta: float) -> void:
	if manager == null:
		return
	var length := _start.distance_to(_end)
	var direction := (_end - _start) / maxf(length, 0.001)
	var pos := global_position
	var speed := 0.0

	# A car coming at us? Jump out of the way!
	var danger := manager.dangerous_vehicle_near(pos)
	if danger and _dodge_time <= 0.0:
		var car_direction: Vector3 = danger.linear_velocity.normalized()
		var away := Vector3.UP.cross(car_direction)
		if (pos - danger.global_position).dot(away) < 0.0:
			away = -away
		_dodge = away * 5.0
		_dodge_time = 0.5
		_shaken_time = 1.5
	if _dodge_time > 0.0:
		_dodge_time -= delta
		pos += _dodge * delta
		global_position = pos
		_face(_dodge)
		_animate(true, delta)
		return
	if _shaken_time > 0.0:
		_shaken_time -= delta
		_animate(false, delta)
		return

	if _waiting:
		_waited += delta
		if not manager.is_road_busy(_start, _end, _waited):
			_waiting = false
			_waited = 0.0
			on_road = true
	else:
		speed = walk_speed
		# Don't walk into the player.
		var player := manager.player_on_foot()
		if player:
			var offset := player.global_position - pos
			offset.y = 0.0
			if offset.length() < 1.5 and offset.dot(direction) > 0.2:
				speed = 0.0
		# Don't walk through a car that's stopped on the crossing (but give up
		# after a few seconds, in case it's waiting for us).
		if on_road and manager.is_blocked_by_car(pos, direction):
			_blocked_time += delta
			if _blocked_time < 4.0:
				speed = 0.0
		else:
			_blocked_time = 0.0

	_progress += speed * delta
	var target := _start + direction * minf(_progress, length)
	# Drift back to the path after dodging.
	pos = pos.move_toward(target, (speed + 2.0) * delta) if pos.distance_to(target) > 0.05 else target
	global_position = pos
	_face(direction)
	_animate(speed > 0.0, delta)

	if _progress >= length:
		var next := manager.choose_next(_to, _from)
		start_walk(_to, next[0], next[1])


func _face(direction: Vector3) -> void:
	direction.y = 0.0
	if direction.length() < 0.01:
		return
	var wanted := Basis.looking_at(direction.normalized())
	global_basis = global_basis.slerp(wanted, 0.2).orthonormalized()


# --- Looks and animation -----------------------------------------------------------------

func _animate(moving: bool, delta: float) -> void:
	if _anim_player:
		if not moving and _idle_anim == "":
			_anim_player.pause()  # No idle animation: stand still mid-step.
			return
		var anim := _walk_anim if moving else _idle_anim
		if _anim_player.assigned_animation != anim:
			_anim_player.get_animation(anim).loop_mode = Animation.LOOP_LINEAR
			_anim_player.play(anim, 0.2)
		elif not _anim_player.is_playing():
			_anim_player.play()
		return
	# No animations: a simple bob while walking.
	if moving:
		_bob_time += delta * walk_speed * 6.0
		_body.position.y = absf(sin(_bob_time)) * 0.06
	else:
		_body.position.y = 0.0


func _build_body() -> void:
	_body = Node3D.new()
	add_child(_body)
	if model:
		var instance := ModelLoader.instantiate(model)
		if instance:
			ModelLoader.fit_character(instance, height, flip_model)
			_body.add_child(instance)
			_anim_player = ModelLoader.find_animation_player(instance)
			if _anim_player:
				_walk_anim = _find_animation(["walk"])
				_idle_anim = _find_animation(["idle", "stand", "breath"])
				if _walk_anim == "":
					_anim_player = null  # Can't walk; use the bob instead.
	if _body.get_child_count() == 0:
		_build_simple_person()

	var shape := CapsuleShape3D.new()
	shape.radius = 0.3
	shape.height = height
	var collision := CollisionShape3D.new()
	collision.shape = shape
	collision.position.y = height / 2.0
	add_child(collision)


func _find_animation(keywords: Array) -> String:
	for keyword in keywords:
		for anim_name in _anim_player.get_animation_list():
			if keyword in anim_name.to_lower():
				return anim_name
	return ""


## A simple person made of a capsule body and a ball for a head.
func _build_simple_person() -> void:
	var body_mesh := CapsuleMesh.new()
	body_mesh.radius = 0.25
	body_mesh.height = height * 0.8
	var body_material := StandardMaterial3D.new()
	body_material.albedo_color = COLOURS[randi() % COLOURS.size()]
	var torso := MeshInstance3D.new()
	torso.mesh = body_mesh
	torso.material_override = body_material
	torso.position.y = height * 0.4
	_body.add_child(torso)

	var head_mesh := SphereMesh.new()
	head_mesh.radius = 0.13
	head_mesh.height = 0.26
	var head_material := StandardMaterial3D.new()
	head_material.albedo_color = SKIN[randi() % SKIN.size()]
	var head := MeshInstance3D.new()
	head.mesh = head_mesh
	head.material_override = head_material
	head.position.y = height * 0.8 + 0.12
	_body.add_child(head)
