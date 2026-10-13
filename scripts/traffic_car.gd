class_name TrafficCar
extends AnimatableBody3D
## One computer-driven car (or bus). It follows the left-hand lane from junction
## to junction, picking a random way to go at each one. The Traffic node
## creates these, so you don't place them yourself.

const SECONDS_STUCK_BEFORE_RESPAWN := 8.0
const ACCELERATION := 4.0     # m/s²
const BRAKING := 9.0          # m/s²
const SAFE_GAP := 2.5         # metres to leave behind whatever is in front
const TURN_SPEED := 5.0       # m/s through corners

var manager: Traffic
var is_bus := false
var model: Resource = null
var flip_model := false
var cruise_speed := 11.0
var half_length := 2.2

var waiting_at_junction := false
var in_junction := false

var _from: Vector2i
var _to: Vector2i
var _next: Vector2i
var _path := PackedVector3Array()
var _target := 0          # Index of the path point we're driving towards.
var _speed := 0.0
var _turning := false
var _stuck_time := 0.0

static var _shared := {}  # Meshes and materials shared by all traffic cars.

const PAINT := [
	Color(0.05, 0.05, 0.06),  # black cab
	Color(0.05, 0.05, 0.06),
	Color(0.75, 0.75, 0.78),  # silver
	Color(0.9, 0.9, 0.9),     # white
	Color(0.12, 0.2, 0.45),   # blue
	Color(0.5, 0.08, 0.08),   # dark red
	Color(0.15, 0.3, 0.2),    # green
	Color(0.3, 0.3, 0.32),    # grey
]


func _ready() -> void:
	# We move the car ourselves every physics step; this stops the physics engine
	# from undoing it when a car is placed somewhere new.
	sync_to_physics = false
	_build_body()


# --- Driving --------------------------------------------------------------------

## Starts driving from junction [param from] towards [param to], then on to
## [param next].
func start_route(from: Vector2i, to: Vector2i, next: Vector2i) -> void:
	_from = from
	_to = to
	_next = next
	_build_path()
	var start := _path[0]
	global_transform = Transform3D(_basis_facing(_path[1] - start), start)
	_target = 1
	_speed = cruise_speed * 0.5
	_stuck_time = 0.0
	in_junction = false
	waiting_at_junction = false


## The route for one stretch of road: the lane up to the junction, then a
## curve (or straight line) across it into the next road.
func _build_path() -> void:
	# Positions are for the middle of the car, so allow for its length: it
	# stops with its front bumper at the junction and leaves once its tail is out.
	var start := manager.lane_start(_from, _to, half_length)
	var direction := (manager.junction_position(_to) - manager.junction_position(_from)).normalized()
	var stop_line := manager.junction_position(_to) - direction * (manager.junction_half_size() + half_length) \
		+ Vector3.UP.cross(direction) * manager.lane_offset()
	var exit := manager.lane_start(_to, _next, half_length)

	_path = PackedVector3Array([start, stop_line])
	var next_direction := (manager.junction_position(_next) - manager.junction_position(_to)).normalized()
	_turning = next_direction.dot(direction) < 0.9
	if _turning:
		# A smooth curve whose corner is where the two lanes would meet.
		var corner := stop_line + direction * (exit - stop_line).dot(direction)
		for k in range(1, 9):
			var t := k / 8.0
			_path.append(stop_line.lerp(corner, t).lerp(corner.lerp(exit, t), t))
	else:
		_path.append(exit)


func _physics_process(delta: float) -> void:
	if _path.is_empty():
		return

	# How fast do we want to go?
	var wanted := cruise_speed
	if _target >= 2 and _turning:
		wanted = TURN_SPEED

	# Slow down for whatever is in front.
	var gap: float = manager.free_distance_ahead(self) - SAFE_GAP
	wanted = minf(wanted, _speed_to_stop_within(gap))

	# Approaching the junction: we need it to ourselves before we cross.
	waiting_at_junction = false
	if _target == 1:
		var to_line := global_position.distance_to(_path[1])
		# Only go if there's room on the far side, so we never block the junction.
		if to_line < 12.0 and (gap < 10.0 or not manager.try_reserve_junction(_to, self)):
			wanted = minf(wanted, _speed_to_stop_within(to_line - 0.5))
			waiting_at_junction = to_line < 3.0

	if wanted < _speed:
		_speed = maxf(wanted, _speed - BRAKING * delta)
	else:
		_speed = minf(wanted, _speed + ACCELERATION * delta)
	_drive(_speed * delta)

	# Stuck for ages somewhere the player can't see? Start again elsewhere.
	_stuck_time = _stuck_time + delta if _speed < 0.3 else 0.0
	if _stuck_time > SECONDS_STUCK_BEFORE_RESPAWN:
		_stuck_time = 0.0
		if global_position.distance_to(manager.player_position()) > 70.0:
			manager.respawn(self, 70.0)


func _speed_to_stop_within(distance: float) -> float:
	if distance <= 0.0:
		return 0.0
	return sqrt(2.0 * BRAKING * 0.6 * distance)


## Moves [param distance] metres along the path, turning to face where we go.
func _drive(distance: float) -> void:
	var pos := global_position
	while distance > 0.0:
		var target := _path[_target]
		var to_target := pos.distance_to(target)
		if to_target > distance:
			pos += (target - pos) / to_target * distance
			break
		pos = target
		distance -= to_target
		_target += 1
		if _target == 2:
			in_junction = true
		if _target >= _path.size():
			_finish_stretch()
			return

	var heading := _path[_target] - pos
	heading.y = 0.0
	var turned := global_basis
	if heading.length() > 0.01:
		var wanted := _basis_facing(heading)
		turned = global_basis.slerp(wanted, clampf(10.0 * get_physics_process_delta_time(), 0.0, 1.0)).orthonormalized()
	global_transform = Transform3D(turned, pos)


## Reached the far side of a junction: release it and head for the next one.
func _finish_stretch() -> void:
	manager.release_junction(_to, self)
	var keep_basis := global_basis
	var keep_speed := _speed
	var from := _to
	var to := _next
	start_route(from, to, manager.choose_next(from, to))
	global_basis = keep_basis
	_speed = keep_speed


## A rotation that points the car's front (+Z) along [param direction].
func _basis_facing(direction: Vector3) -> Basis:
	var z := Vector3(direction.x, 0.0, direction.z).normalized()
	return Basis(Vector3.UP.cross(z), Vector3.UP, z)


# --- Looks ------------------------------------------------------------------------

func _build_body() -> void:
	var size := Vector3(2.5, 4.4, 10.0) if is_bus else Vector3(1.8, 1.4, 4.2)
	half_length = size.z / 2.0

	if model and not is_bus:
		var instance := ModelLoader.instantiate(model)
		if instance:
			var aabb := ModelLoader.get_aabb(instance)
			# Scale the model to the length of a real car (about 4.3 m).
			var length := maxf(aabb.size.z, 0.001)
			var model_scale := 4.3 / length
			instance.scale = Vector3.ONE * model_scale
			if flip_model:
				instance.rotation.y = PI
			var bottom_centre := Vector3(aabb.get_center().x, aabb.position.y, aabb.get_center().z)
			instance.position = -(instance.basis * bottom_centre)
			add_child(instance)
			size = aabb.size * model_scale
			half_length = size.z / 2.0
			_add_collision(size)
			return

	if is_bus:
		_add_part(Vector3(2.5, 4.1, 10.0), Vector3(0, 2.35, 0), _material(Color(0.78, 0.06, 0.14), 0.5))
		_add_part(Vector3(2.56, 0.9, 9.0), Vector3(0, 2.0, 0.2), _material(Color(0.15, 0.2, 0.25), 0.1))
		_add_part(Vector3(2.56, 0.9, 9.4), Vector3(0, 3.6, 0), _material(Color(0.15, 0.2, 0.25), 0.1))
	else:
		var paint: Color = PAINT[randi() % PAINT.size()]
		_add_part(Vector3(1.8, 0.6, 4.2), Vector3(0, 0.65, 0), _material(paint, 0.35))
		_add_part(Vector3(1.6, 0.55, 2.2), Vector3(0, 1.22, -0.25), _material(Color(0.15, 0.2, 0.25), 0.1))
	# Wheels.
	var wheel_x := size.x / 2.0 - 0.1
	var wheel_z := size.z / 2.0 - (1.0 if not is_bus else 1.8)
	for x in [-wheel_x, wheel_x]:
		for z in [-wheel_z, wheel_z]:
			var wheel := MeshInstance3D.new()
			wheel.mesh = _wheel_mesh()
			wheel.position = Vector3(x, 0.38, z)
			wheel.rotation.z = PI / 2.0
			add_child(wheel)
	_add_collision(size)


func _add_collision(size: Vector3) -> void:
	var shape := BoxShape3D.new()
	shape.size = Vector3(size.x, size.y - 0.3, size.z)
	var collision := CollisionShape3D.new()
	collision.shape = shape
	collision.position.y = 0.3 + shape.size.y / 2.0
	add_child(collision)


func _add_part(size: Vector3, pos: Vector3, material: Material) -> void:
	var key := "box %s" % size
	if not _shared.has(key):
		var mesh := BoxMesh.new()
		mesh.size = size
		_shared[key] = mesh
	var part := MeshInstance3D.new()
	part.mesh = _shared[key]
	part.material_override = material
	part.position = pos
	add_child(part)


func _material(color: Color, roughness: float) -> Material:
	var key := "mat %s %s" % [color, roughness]
	if not _shared.has(key):
		var material := StandardMaterial3D.new()
		material.albedo_color = color
		material.roughness = roughness
		material.metallic = 0.3
		_shared[key] = material
	return _shared[key]


func _wheel_mesh() -> Mesh:
	if not _shared.has("wheel"):
		var mesh := CylinderMesh.new()
		mesh.top_radius = 0.38
		mesh.bottom_radius = 0.38
		mesh.height = 0.25
		mesh.material = _material(Color(0.05, 0.05, 0.05), 0.9)
		_shared["wheel"] = mesh
	return _shared["wheel"]
