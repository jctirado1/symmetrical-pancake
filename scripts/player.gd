extends CharacterBody3D
## The player character: walks, runs and jumps, with a third-person camera
## that you steer with the mouse.
##
## Every variable marked @export shows up in the Inspector panel when you
## select the Player node, so you can tweak it without touching code.

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

@onready var camera_pivot: Node3D = $CameraPivot
@onready var spring_arm: SpringArm3D = $CameraPivot/SpringArm3D
@onready var body: Node3D = $Body


func _ready() -> void:
	spawn_position = global_position
	# Stop the camera arm from bumping into the player's own capsule.
	spring_arm.add_excluded_object(get_rid())
	Input.mouse_mode = Input.MOUSE_MODE_CAPTURED


func _unhandled_input(event: InputEvent) -> void:
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

	# Fell off the edge of the world? Put the player back at the start.
	if global_position.y < -20.0:
		global_position = spawn_position
		velocity = Vector3.ZERO
