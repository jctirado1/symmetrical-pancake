class_name Pedestrians
extends Node3D
## Fills the pavements with people. They walk round the blocks, cross the
## roads on the zebra crossings (and traffic stops for them), wait for cars
## to pass, step aside for you, and jump out of the way of a car you drive
## at them.
##
## Pedestrians only appear when you play the game, not in the editor.

## The CityGrid node whose pavements they walk on.
@export var city: CityGrid
## How many people are out and about. They stay near you: anyone who ends
## up far away quietly reappears somewhere closer.
@export_range(0, 300) var count := 80
## People further away than this (metres) are moved closer to you.
@export var keep_within := 160.0
## Folder of character models for pedestrians. If it's empty, the characters
## found in the project are used (see assets/found_assets.txt), and if there
## are none, simple coloured people.
@export_dir var characters_folder := ""
## Tick this if your pedestrian models walk backwards.
@export var flip_models := false

var people: Array[Pedestrian] = []
var _check_timer := 0.0
var _rng := RandomNumberGenerator.new()
var _models: Array[Resource] = []


func _ready() -> void:
	if city == null:
		push_warning("Pedestrians: set 'City' in the Inspector.")
		return
	_rng.randomize()
	var paths := ModelLoader.find_model_files(characters_folder, true)
	if paths.is_empty():
		paths = AssetFinder.other_characters()
	for path in paths:
		var model := load(path)
		if model:
			_models.append(model)

	await get_tree().process_frame
	for k in count:
		var person := Pedestrian.new()
		person.manager = self
		person.walk_speed = _rng.randf_range(1.1, 1.6)
		person.height = _rng.randf_range(1.6, 1.9)
		person.side_offset = _rng.randf_range(0.3, 0.9)
		if not _models.is_empty():
			person.model = _models[_rng.randi() % _models.size()]
			person.flip_model = flip_models
		add_child(person)
		people.append(person)
		_place_randomly(person, 0.0, keep_within * 0.8)


## Every second, moves anyone who's wandered far away back near the player.
func _process(delta: float) -> void:
	_check_timer -= delta
	if _check_timer > 0.0:
		return
	_check_timer = 1.0
	var centre := _player_position()
	for person in people:
		if person.global_position.distance_to(centre) > keep_within:
			_place_randomly(person, 50.0, keep_within * 0.75)


func _player_position() -> Vector3:
	var player := get_tree().get_first_node_in_group("player") as Node3D
	if player == null:
		return Vector3.ZERO
	if not player.visible:
		for vehicle in get_tree().get_nodes_in_group("vehicles"):
			if vehicle.driver == player:
				return vehicle.global_position
	return player.global_position


# --- The walking network ----------------------------------------------------------
# Every block has a corner point in the middle of the pavement at each of its
# four corners. People walk from corner to corner round the block, or straight
# across the road to the facing corner of the next block (over a zebra).
# A corner is Vector3i(block x, block z, corner number 0-3):
#   0 = north-west, 1 = north-east, 2 = south-east, 3 = south-west.

const CORNER_SIGNS := [Vector2i(-1, -1), Vector2i(1, -1), Vector2i(1, 1), Vector2i(-1, 1)]


func corner_position(corner: Vector3i) -> Vector3:
	var rect := city.get_block_rect(corner.x, corner.y)
	var inset := city.pavement_width / 2.0
	var signs := CORNER_SIGNS[corner.z] as Vector2i
	var x := rect.position.x + inset if signs.x < 0 else rect.end.x - inset
	var z := rect.position.y + inset if signs.y < 0 else rect.end.y - inset
	return Vector3(x, CityGrid.PAVEMENT_HEIGHT, z)


func _corner_with_signs(bx: int, bz: int, signs: Vector2i) -> Vector3i:
	return Vector3i(bx, bz, CORNER_SIGNS.find(signs))


## Corners you can walk to from [param corner], and whether that walk crosses a road.
func next_corners(corner: Vector3i) -> Array:
	var result := []
	var signs := CORNER_SIGNS[corner.z] as Vector2i
	# Along the two sides of this block.
	result.append([_corner_with_signs(corner.x, corner.y, Vector2i(-signs.x, signs.y)), false])
	result.append([_corner_with_signs(corner.x, corner.y, Vector2i(signs.x, -signs.y)), false])
	# Across the road to the neighbouring blocks.
	var across_x := corner.x + signs.x
	if across_x >= 0 and across_x < city.blocks_x:
		result.append([_corner_with_signs(across_x, corner.y, Vector2i(-signs.x, signs.y)), true])
	var across_z := corner.y + signs.y
	if across_z >= 0 and across_z < city.blocks_z:
		result.append([_corner_with_signs(corner.x, across_z, Vector2i(signs.x, -signs.y)), true])
	return result


## Picks where to walk after reaching [param corner], not straight back to
## [param came_from] unless there's no other way.
func choose_next(corner: Vector3i, came_from: Vector3i) -> Array:
	var options := next_corners(corner)
	if options.size() > 1:
		options = options.filter(func(option): return option[0] != came_from)
	return options[_rng.randi() % options.size()]


## Puts [param person] on a random pavement between [param min_distance] and
## [param max_distance] metres from the player.
func _place_randomly(person: Pedestrian, min_distance: float, max_distance: float) -> void:
	var centre := _player_position()
	var corner := Vector3i.ZERO
	for attempt in 30:
		corner = Vector3i(_rng.randi() % city.blocks_x, _rng.randi() % city.blocks_z, _rng.randi() % 4)
		var distance := corner_position(corner).distance_to(centre)
		if distance >= min_distance and distance <= max_distance:
			break
	var options := next_corners(corner).filter(func(option): return not option[1])
	var target: Vector3i = options[_rng.randi() % options.size()][0]
	person.start_walk(corner, target, false, _rng.randf())


# --- What people need to know about the world -----------------------------------------

## True if it isn't safe to step onto the road between [param from] and [param to].
## After waiting [param patience_used] seconds people get impatient and only
## wait for cars that are really close (cars stop for people on zebras).
func is_road_busy(from: Vector3, to: Vector3, patience_used := 0.0) -> bool:
	var middle := (from + to) / 2.0
	var traffic := get_tree().get_first_node_in_group("traffic")
	if traffic:
		for car in traffic.cars:
			var distance: float = car.global_position.distance_to(middle)
			if patience_used > 15.0:
				if distance < 9.0 + car.half_length:
					return true
				continue
			# Close, or coming fast, or turning through the junction next to us.
			if distance < 10.0 + car.half_length + car._speed * 1.5:
				return true
			if car.in_junction and distance < 25.0:
				return true
	for vehicle in get_tree().get_nodes_in_group("vehicles"):
		if vehicle.linear_velocity.length() > 1.0 and vehicle.global_position.distance_to(middle) < 15.0:
			return true
	return false


## True if a stopped traffic car is right in front of [param pos], in the way
## of someone walking along [param direction].
func is_blocked_by_car(pos: Vector3, direction: Vector3) -> bool:
	var traffic := get_tree().get_first_node_in_group("traffic")
	if traffic == null:
		return false
	var step := pos + direction * 0.8
	for car in traffic.cars:
		if car._speed > 0.5:
			continue
		var offset: Vector3 = step - car.global_position
		if absf(offset.dot(car.global_basis.z)) < car.half_length + 0.4 \
				and absf(offset.dot(car.global_basis.x)) < 1.3:
			return true
	return false


## A car being driven fast towards [param pos], if there is one.
func dangerous_vehicle_near(pos: Vector3) -> Node3D:
	for vehicle in get_tree().get_nodes_in_group("vehicles"):
		var velocity: Vector3 = vehicle.linear_velocity
		velocity.y = 0.0
		if velocity.length() < 3.0:
			continue
		var offset: Vector3 = pos - vehicle.global_position
		offset.y = 0.0
		var ahead := offset.dot(velocity.normalized())
		if ahead < -1.0 or ahead > 4.0 + velocity.length() * 0.6:
			continue
		var side := (offset - velocity.normalized() * ahead).length()
		if side < 2.2:
			return vehicle
	return null


func player_on_foot() -> Node3D:
	var player := get_tree().get_first_node_in_group("player") as Node3D
	return player if player and player.visible else null
