@tool
extends VehicleBody3D
## A car you can get into and drive.
##
## Walk up to it and press E (or F) to get in, E again to get out.
## W / S to drive and reverse, A / D to steer, Space for the handbrake.

@export_group("Car Model")
## Your car: drag a .glb, .gltf, .fbx or .obj file here. If empty, a car
## found in the project is used (see assets/found_assets.txt); if there are
## none, you get the simple box car.
@export var car_model: Resource:
	set(value):
		car_model = value
		_rebuild_model()
## Optional .png to paint onto the car (for models that show up white).
@export var car_texture: Texture2D:
	set(value):
		car_texture = value
		_rebuild_model()
## Resize the model automatically to the length of a real car (4.3 m) and
## turn it to face along the car. Untick to use Model Scale instead.
@export var fit_to_car_size := true:
	set(value):
		fit_to_car_size = value
		_rebuild_model()
## Make the model bigger or smaller (only when Fit To Car Size is off).
@export_range(0.01, 10.0, 0.01, "or_greater") var model_scale := 1.0:
	set(value):
		model_scale = value
		_rebuild_model()
## Tick this if the car model faces the wrong way.
@export var flip_model_direction := false:
	set(value):
		flip_model_direction = value
		_rebuild_model()
## Move the model up or down if it floats above or sinks into the road.
@export var model_height_offset := 0.0:
	set(value):
		model_height_offset = value
		_rebuild_model()
## Show the simple black wheels as well as your model (untick if your model
## already has wheels).
@export var show_box_wheels := false:
	set(value):
		show_box_wheels = value
		_rebuild_model()

@export_group("Driving")
@export var max_engine_force := 2500.0  ## How hard the car accelerates.
@export var max_brake := 40.0           ## How hard the brakes are.
@export var max_steer_degrees := 30.0   ## How far the front wheels turn.
@export var top_speed_kmh := 120.0
@export var reverse_speed_kmh := 30.0
@export var camera_height := 1.5        ## How high above the car the camera looks.

var driver: Node3D = null  ## The player while they're in the car.
var _model: Node3D

@onready var camera_pivot: Node3D = $CameraPivot
@onready var camera: Camera3D = $CameraPivot/SpringArm3D/Camera3D
@onready var hud: CanvasLayer = $HUD
@onready var speed_label: Label = $HUD/SpeedLabel


func _ready() -> void:
	_rebuild_model()
	if Engine.is_editor_hint():
		return
	add_to_group("vehicles")
	$CameraPivot/SpringArm3D.add_excluded_object(get_rid())
	# The camera follows the car smoothly instead of being bolted to it.
	camera_pivot.top_level = true
	hud.visible = false
	brake = max_brake


## Called by the player when they press E next to this car.
func enter(player: Node3D) -> void:
	driver = player
	camera_pivot.global_position = global_position + Vector3.UP * camera_height
	camera_pivot.rotation.y = atan2(global_basis.z.x, global_basis.z.z) + PI
	camera.make_current()
	hud.visible = true
	brake = 0.0


func exit() -> void:
	var player := driver
	driver = null
	hud.visible = false
	engine_force = 0.0
	brake = max_brake
	# Put the player down beside the driver's door.
	player.leave_vehicle(global_position + global_basis.x * 2.0 + Vector3.UP * 0.5)


func _unhandled_input(event: InputEvent) -> void:
	if Engine.is_editor_hint() or driver == null:
		return
	if event.is_action_pressed("interact"):
		get_viewport().set_input_as_handled()
		exit()


func _physics_process(delta: float) -> void:
	if Engine.is_editor_hint():
		return

	# The car drives along its +Z axis; "forward_speed" is negative when reversing.
	var forward_speed := linear_velocity.dot(global_basis.z)
	var speed_kmh := absf(forward_speed) * 3.6

	if driver:
		var throttle := Input.get_axis("move_back", "move_forward")
		var steer_input := Input.get_axis("move_right", "move_left")

		engine_force = 0.0
		brake = 0.0
		if throttle > 0.0:
			if forward_speed < -0.5:
				brake = max_brake * throttle          # rolling backwards: brake first
			elif speed_kmh < top_speed_kmh:
				engine_force = max_engine_force * throttle
		elif throttle < 0.0:
			if forward_speed > 0.5:
				brake = max_brake * -throttle         # moving forwards: brake first
			elif speed_kmh < reverse_speed_kmh:
				engine_force = max_engine_force * throttle
		else:
			brake = 2.0                                # gentle roll to a stop
		if Input.is_action_pressed("jump"):
			brake = max_brake                          # handbrake

		# Steer less at high speed so the car doesn't flip.
		var max_steer := deg_to_rad(max_steer_degrees) * lerpf(1.0, 0.2, clampf(speed_kmh / 70.0, 0.0, 1.0))
		steering = move_toward(steering, steer_input * max_steer, 1.5 * delta)

		speed_label.text = "%d mph\nE to get out" % roundi(speed_kmh / 1.609)

	# Smooth chase camera.
	camera_pivot.global_position = camera_pivot.global_position.lerp(global_position + Vector3.UP * camera_height, 10.0 * delta)
	var target_yaw := atan2(global_basis.z.x, global_basis.z.z) + PI
	camera_pivot.rotation.y = lerp_angle(camera_pivot.rotation.y, target_yaw, 4.0 * delta)


## Swaps the box car for your own car model, if one is set.
func _rebuild_model() -> void:
	if not is_inside_tree():
		return
	if _model:
		_model.queue_free()
		_model = null
	var model_file := car_model
	if model_file == null:
		var found := AssetFinder.cars()
		if not found.is_empty():
			# Each parked car picks a different one.
			model_file = load(found[get_index() % found.size()])
	$BoxBody.visible = model_file == null
	for wheel in get_children():
		if wheel is VehicleWheel3D:
			wheel.get_child(0).visible = model_file == null or show_box_wheels
	if model_file == null:
		return
	_model = ModelLoader.instantiate(model_file, car_texture)
	if _model == null:
		return
	if fit_to_car_size:
		ModelLoader.fit_vehicle(_model, 4.3, flip_model_direction)
	else:
		_model.scale = Vector3.ONE * model_scale
		if flip_model_direction:
			_model.rotation.y = PI
	_model.position.y += model_height_offset
	add_child(_model)
