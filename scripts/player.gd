@tool
extends CharacterBody3D
## The player character: walks, runs and jumps, with a third-person camera
## that you steer with the mouse.
##
## Every variable marked @export shows up in the Inspector panel when you
## select the Player node, so you can tweak it without touching code.

@export_group("Character Model")
## Your character: drag a .glb, .gltf, .fbx or .obj file here. If you leave
## it empty, the first model in Character Folder is used instead, and if
## that folder is empty too you get the blue capsule.
@export var character_model: Resource:
	set(value):
		character_model = value
		_rebuild_model()
## Folder the game looks in for a character when Character Model is empty.
@export_dir var character_folder := "res://assets/characters":
	set(value):
		character_folder = value
		_rebuild_model()
## Optional .png to paint onto the character (for models that show up white).
@export var character_texture: Texture2D:
	set(value):
		character_texture = value
		_rebuild_model()
## Resize the character automatically so it's 1.8 m tall (the size of the
## capsule) and standing on the ground. Untick to use Model Scale only.
@export var fit_to_height := true:
	set(value):
		fit_to_height = value
		_rebuild_model()
## Make the model bigger or smaller (applied after Fit To Height).
@export_range(0.01, 10.0, 0.01, "or_greater") var model_scale := 1.0:
	set(value):
		model_scale = value
		_rebuild_model()
## Tick this if your character walks backwards.
@export var flip_model_direction := false:
	set(value):
		flip_model_direction = value
		_rebuild_model()
## Names of the model's animations. Leave empty and the game guesses from
## names containing "idle", "walk" and "run".
@export var idle_animation := ""
@export var walk_animation := ""
@export var run_animation := ""

@export_group("Movement")

@export var walk_speed := 4.0         ## Metres per second when walking.
@export var run_speed := 8.0          ## Metres per second while holding Shift.
@export var jump_velocity := 4.5      ## How hard the player jumps.
@export var turn_speed := 10.0        ## How quickly the body turns to face where it walks.
@export var mouse_sensitivity := 0.003

## How close you need to be to a car to get in (metres).
@export var enter_vehicle_distance := 4.0

## Lowest and highest the camera may tilt, in degrees.
@export var camera_min_pitch := -70.0
@export var camera_max_pitch := 20.0

var gravity: float = ProjectSettings.get_setting("physics/3d/default_gravity")
var spawn_position: Vector3
var _model: Node3D
var _anim_player: AnimationPlayer
var _anim_names := {}
var _nearby_vehicle: Node3D = null

@onready var camera_pivot: Node3D = $CameraPivot
@onready var spring_arm: SpringArm3D = $CameraPivot/SpringArm3D
@onready var body: Node3D = $Body
@onready var camera: Camera3D = $CameraPivot/SpringArm3D/Camera3D
@onready var hint_label: Label = $HUD/HintLabel


func _ready() -> void:
	_rebuild_model()
	if Engine.is_editor_hint():
		return
	add_to_group("player")
	spawn_position = global_position
	# Stop the camera arm from bumping into the player's own capsule.
	spring_arm.add_excluded_object(get_rid())
	Input.mouse_mode = Input.MOUSE_MODE_CAPTURED


func _unhandled_input(event: InputEvent) -> void:
	if Engine.is_editor_hint():
		return
	if event.is_action_pressed("interact") and _nearby_vehicle:
		get_viewport().set_input_as_handled()
		_enter_vehicle(_nearby_vehicle)
		return

	# Esc frees the mouse; clicking in the game window captures it again.
	if event.is_action_pressed("ui_cancel"):
		Input.mouse_mode = Input.MOUSE_MODE_VISIBLE
	elif event is InputEventMouseButton and event.pressed:
		Input.mouse_mode = Input.MOUSE_MODE_CAPTURED

	# Moving the mouse swings the camera around the player.
	if event is InputEventMouseMotion and Input.mouse_mode == Input.MOUSE_MODE_CAPTURED:
		camera_pivot.rotation.y -= event.relative.x * mouse_sensitivity
		camera_pivot.rotation.x = clamp(
			camera_pivot.rotation.x - event.relative.y * mouse_sensitivity,
			deg_to_rad(camera_min_pitch),
			deg_to_rad(camera_max_pitch)
		)


func _physics_process(delta: float) -> void:
	if Engine.is_editor_hint():
		return
	# Gravity pulls the player down whenever they are in the air.
	if not is_on_floor():
		velocity.y -= gravity * delta

	if Input.is_action_just_pressed("jump") and is_on_floor():
		velocity.y = jump_velocity

	# Read WASD / arrow keys and turn them into a direction relative to the camera,
	# so "forward" always means "away from the camera".
	var input_dir := Input.get_vector("move_left", "move_right", "move_forward", "move_back")
	var direction := Vector3(input_dir.x, 0.0, input_dir.y).rotated(Vector3.UP, camera_pivot.rotation.y)

	var speed := run_speed if Input.is_action_pressed("sprint") else walk_speed

	if direction.length() > 0.01:
		direction = direction.normalized()
		velocity.x = direction.x * speed
		velocity.z = direction.z * speed
		# Smoothly turn the body to face the way we're walking.
		var target_angle := atan2(-direction.x, -direction.z)
		body.rotation.y = lerp_angle(body.rotation.y, target_angle, turn_speed * delta)
	else:
		velocity.x = move_toward(velocity.x, 0.0, speed)
		velocity.z = move_toward(velocity.z, 0.0, speed)

	move_and_slide()
	_update_animation()
	_look_for_vehicle()

	# Fell off the edge of the world? Put the player back at the start.
	if global_position.y < -20.0:
		global_position = spawn_position
		velocity = Vector3.ZERO


## Finds the nearest car within reach and shows the "Press E" hint.
func _look_for_vehicle() -> void:
	_nearby_vehicle = null
	var best_distance := enter_vehicle_distance
	for vehicle in get_tree().get_nodes_in_group("vehicles"):
		var distance := global_position.distance_to(vehicle.global_position)
		if distance < best_distance:
			best_distance = distance
			_nearby_vehicle = vehicle
	hint_label.visible = _nearby_vehicle != null


func _enter_vehicle(vehicle: Node3D) -> void:
	# Hide the player and switch them off while they're driving.
	visible = false
	hint_label.visible = false
	velocity = Vector3.ZERO
	$CollisionShape3D.disabled = true
	process_mode = Node.PROCESS_MODE_DISABLED
	vehicle.enter(self)


## Called by the car when the player gets out.
func leave_vehicle(exit_position: Vector3) -> void:
	global_position = exit_position
	visible = true
	$CollisionShape3D.disabled = false
	process_mode = Node.PROCESS_MODE_INHERIT
	camera.make_current()


## Swaps the blue capsule for your own character model, if one is set.
func _rebuild_model() -> void:
	if not is_inside_tree():
		return
	if _model:
		_model.queue_free()
		_model = null
		_anim_player = null
	var model_file := character_model
	if model_file == null:
		var found := ModelLoader.find_model_files(character_folder, true)
		if not found.is_empty():
			model_file = load(found[0])
			if not Engine.is_editor_hint():
				print("Using character from folder: ", found[0])
	$Body/Mesh.visible = model_file == null
	$Body/Face.visible = model_file == null
	if model_file == null:
		return

	_model = ModelLoader.instantiate(model_file, character_texture)
	if _model == null:
		return
	var final_scale := model_scale
	if fit_to_height:
		var aabb := ModelLoader.get_aabb(_model)
		if aabb.size.y > 0.0:
			final_scale *= 1.8 / aabb.size.y
			_model.position.y = -aabb.position.y * final_scale
	_model.scale = Vector3.ONE * final_scale
	# Most character files face "forwards" towards the camera (+Z), while
	# Godot's forward is -Z, so turn them round unless told otherwise.
	if not flip_model_direction:
		_model.rotation.y = PI
	$Body.add_child(_model)

	_anim_player = ModelLoader.find_animation_player(_model)
	if _anim_player:
		_anim_names = {
			"idle": _pick_animation(idle_animation, ["idle", "stand", "breath"]),
			"walk": _pick_animation(walk_animation, ["walk"]),
			"run": _pick_animation(run_animation, ["run", "sprint", "jog"]),
		}
		if _anim_names.run == "":
			_anim_names.run = _anim_names.walk
		print("Character animations found: ", _anim_player.get_animation_list())
		print("Using: ", _anim_names)


## Returns [param chosen] if the model has it, otherwise the first animation
## whose name contains one of [param keywords].
func _pick_animation(chosen: String, keywords: Array) -> String:
	var names := _anim_player.get_animation_list()
	if chosen != "" and _anim_player.has_animation(chosen):
		return chosen
	for keyword in keywords:
		for anim_name in names:
			if keyword in anim_name.to_lower():
				return anim_name
	return ""


## Plays idle, walk or run depending on how fast the player is moving.
func _update_animation() -> void:
	if _anim_player == null:
		return
	var horizontal_speed := Vector2(velocity.x, velocity.z).length()
	var state := "idle"
	if horizontal_speed > walk_speed + 0.5:
		state = "run"
	elif horizontal_speed > 0.1:
		state = "walk"
	var anim_name: String = _anim_names.get(state, "")
	if anim_name == "" or _anim_player.current_animation == anim_name:
		return
	# Make sure the animation repeats instead of stopping after one go.
	_anim_player.get_animation(anim_name).loop_mode = Animation.LOOP_LINEAR
	_anim_player.play(anim_name, 0.2)
