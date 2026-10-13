@tool
extends CharacterBody3D
## The player character: walks, runs and jumps, with a third-person camera
## that you steer with the mouse.
##
## Every variable marked @export shows up in the Inspector panel when you
## select the Player node, so you can tweak it without touching code.

@export_group("Character Model")
## Your character: drag a .glb, .gltf, .fbx or .obj file here. Leave empty
## to use the blue capsule.
@export var character_model: Resource:
	set(value):
		character_model = value
		_rebuild_model()
## Optional .png to paint onto the character (for models that show up white).
@export var character_texture: Texture2D:
	set(value):
		character_texture = value
		_rebuild_model()
## Make the model bigger or smaller. The capsule is 1.8 m tall.
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

## Lowest and highest the camera may tilt, in degrees.
@export var camera_min_pitch := -70.0
@export var camera_max_pitch := 20.0

var gravity: float = ProjectSettings.get_setting("physics/3d/default_gravity")
var spawn_position: Vector3
var _model: Node3D
var _anim_player: AnimationPlayer
var _anim_names := {}

@onready var camera_pivot: Node3D = $CameraPivot
@onready var spring_arm: SpringArm3D = $CameraPivot/SpringArm3D
@onready var body: Node3D = $Body


func _ready() -> void:
	_rebuild_model()
	if Engine.is_editor_hint():
		return
	spawn_position = global_position
	# Stop the camera arm from bumping into the player's own capsule.
	spring_arm.add_excluded_object(get_rid())
	Input.mouse_mode = Input.MOUSE_MODE_CAPTURED


func _unhandled_input(event: InputEvent) -> void:
	if Engine.is_editor_hint():
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

	# Fell off the edge of the world? Put the player back at the start.
	if global_position.y < -20.0:
		global_position = spawn_position
		velocity = Vector3.ZERO


## Swaps the blue capsule for your own character model, if one is set.
func _rebuild_model() -> void:
	if not is_inside_tree():
		return
	if _model:
		_model.queue_free()
		_model = null
		_anim_player = null
	$Body/Mesh.visible = character_model == null
	$Body/Face.visible = character_model == null
	if character_model == null:
		return

	_model = ModelLoader.instantiate(character_model, character_texture)
	if _model == null:
		return
	_model.scale = Vector3.ONE * model_scale
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
