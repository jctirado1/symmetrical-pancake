class_name Traffic
extends Node3D
## Fills the city's roads with traffic: cars and red buses that drive on the
## left, turn at junctions, queue behind each other and stop for you.
##
## Traffic only appears when you play the game, not in the editor.

## The CityGrid node whose roads the traffic drives on.
@export var city: CityGrid
## How many cars and buses to put on the roads.
@export_range(0, 200) var car_count := 30
@export_range(0, 50) var bus_count := 3
## Folder of car models to use for traffic (one is picked at random for each
## car). If it's empty, every car found in the project is used instead (see
## assets/found_assets.txt), and if there are none, simple box cars.
@export_dir var car_models_folder := "res://assets/models/cars"
## Tick this if your car models drive backwards.
@export var flip_car_models := false
@export var cruise_speed_kmh := 40.0

var cars: Array[TrafficCar] = []
var _junction_owner := {}  # Junction (Vector2i) -> the car crossing it.
var _junction_queue := {}  # Junction (Vector2i) -> cars waiting, first come first served.
var _rng := RandomNumberGenerator.new()
var _car_models: Array[Resource] = []


func _ready() -> void:
	add_to_group("traffic")
	if city == null:
		push_warning("Traffic: set 'City' in the Inspector.")
		return
	_rng.randomize()
	var paths := ModelLoader.find_model_files(car_models_folder, true)
	if paths.is_empty():
		paths = AssetFinder.cars()
	for path in paths:
		var model := load(path)
		if model:
			_car_models.append(model)
	# Wait one frame so the city and the player are ready.
	await get_tree().process_frame
	for k in car_count + bus_count:
		var car := TrafficCar.new()
		car.manager = self
		car.is_bus = k < bus_count
		car.cruise_speed = cruise_speed_kmh / 3.6 * (0.8 if car.is_bus else _rng.randf_range(0.85, 1.15))
		if not car.is_bus and not _car_models.is_empty():
			car.model = _car_models[_rng.randi() % _car_models.size()]
			car.flip_model = flip_car_models
		add_child(car)
		cars.append(car)
		respawn(car, 12.0)


# --- Routes -------------------------------------------------------------------

func lane_offset() -> float:
	return city.road_width / 4.0


func junction_half_size() -> float:
	return city.road_width / 2.0


## How far from the middle of a junction cars stop: just before the zebra.
func stop_line_distance() -> float:
	return city.crossing_offset() + 2.0


func junction_position(junction: Vector2i) -> Vector3:
	return city.get_junction_position(junction.x, junction.y)


## Junctions you can drive to from [param junction].
func neighbours(junction: Vector2i) -> Array[Vector2i]:
	var size := city.get_junction_count()
	var result: Array[Vector2i] = []
	for step in [Vector2i(1, 0), Vector2i(-1, 0), Vector2i(0, 1), Vector2i(0, -1)]:
		var next: Vector2i = junction + step
		if next.x >= 0 and next.y >= 0 and next.x < size.x and next.y < size.y:
			result.append(next)
	return result


## Picks where to go after [param to], never turning back unless it must.
func choose_next(from: Vector2i, to: Vector2i) -> Vector2i:
	var options := neighbours(to)
	if options.size() > 1:
		options.erase(from)
	return options[_rng.randi() % options.size()]


## Where the middle of a car [param half_length] metres long is once it has
## fully left junction [param from] towards [param to], in the left-hand lane.
func lane_start(from: Vector2i, to: Vector2i, half_length: float) -> Vector3:
	var a := junction_position(from)
	var direction := (junction_position(to) - a).normalized()
	return a + direction * (junction_half_size() + half_length) + Vector3.UP.cross(direction) * lane_offset()


## Puts [param car] somewhere on the road at least [param min_player_distance]
## metres from the player and away from other cars.
func respawn(car: TrafficCar, min_player_distance: float) -> bool:
	var size := city.get_junction_count()
	var player_pos := player_position()
	for attempt in 50:
		var from := Vector2i(_rng.randi() % size.x, _rng.randi() % size.y)
		var options := neighbours(from)
		var to: Vector2i = options[_rng.randi() % options.size()]
		var start := lane_start(from, to, car.half_length)
		if start.distance_to(player_pos) < min_player_distance:
			continue
		if _is_spot_taken(start, car):
			continue
		release_junctions(car)
		car.start_route(from, to, choose_next(from, to))
		return true
	return false


func _is_spot_taken(spot: Vector3, ignore: Node3D) -> bool:
	for other in cars:
		if other != ignore and other.is_inside_tree() and other.global_position.distance_to(spot) < 14.0:
			return true
	for vehicle in get_tree().get_nodes_in_group("vehicles"):
		if vehicle.global_position.distance_to(spot) < 8.0:
			return true
	return false


# --- Junctions: one car at a time ----------------------------------------------

## Asks to cross [param junction]. Cars queue up and go in the order they
## asked, so nobody waits for ever.
func try_reserve_junction(junction: Vector2i, car: TrafficCar) -> bool:
	if _junction_owner.get(junction) == car:
		return true
	if not _junction_queue.has(junction):
		_junction_queue[junction] = []
	var queue: Array = _junction_queue[junction]
	if car not in queue:
		queue.append(car)
	if not _junction_owner.has(junction) and queue[0] == car:
		queue.pop_front()
		_junction_owner[junction] = car
		return true
	return false


func release_junction(junction: Vector2i, car: TrafficCar) -> void:
	if _junction_owner.get(junction) == car:
		_junction_owner.erase(junction)


## Forgets every junction [param car] was crossing or waiting for.
func release_junctions(car: TrafficCar) -> void:
	for junction in _junction_owner.keys():
		if _junction_owner[junction] == car:
			_junction_owner.erase(junction)
	for queue in _junction_queue.values():
		queue.erase(car)


# --- Seeing what's ahead --------------------------------------------------------

## Where the player is: on foot, or the car they're driving.
func player_position() -> Vector3:
	var player := get_tree().get_first_node_in_group("player") as Node3D
	if player == null:
		return Vector3(INF, INF, INF)
	if not player.visible:
		for vehicle in get_tree().get_nodes_in_group("vehicles"):
			if vehicle.driver == player:
				return vehicle.global_position
	return player.global_position


## How far [param car] can drive before hitting something in its lane
## (INF if the road ahead is clear).
func free_distance_ahead(car: TrafficCar) -> float:
	var forward := car.global_basis.z
	var nearest := INF

	# Other traffic, ignoring cars coming the other way and cars waiting at a
	# junction we're already crossing (otherwise we'd wait for each other).
	for other in cars:
		if other == car or not other.is_inside_tree():
			continue
		if other.global_basis.z.dot(forward) < -0.3:
			continue
		if car.in_junction and other.waiting_at_junction:
			continue
		nearest = minf(nearest, _gap_to(car, forward, other.global_position, other.half_length, 1.9))

	# Parked or player-driven cars, and the player on foot.
	for vehicle in get_tree().get_nodes_in_group("vehicles"):
		nearest = minf(nearest, _gap_to(car, forward, vehicle.global_position, 2.2, 1.9))
	var player := get_tree().get_first_node_in_group("player") as Node3D
	if player and player.visible:
		nearest = minf(nearest, _gap_to(car, forward, player.global_position, 0.4, 1.5))
	# People on a zebra crossing: like a real driver, stop for anyone on the
	# crossing ahead, not just someone already in our lane.
	var watch_width := city.road_width / 2.0 + 1.0
	for person in get_tree().get_nodes_in_group("pedestrians"):
		if person.on_road:
			nearest = minf(nearest, _gap_to(car, forward, person.global_position, 0.4, watch_width))
	return nearest


func _gap_to(car: TrafficCar, forward: Vector3, pos: Vector3, other_half_length: float, max_side: float) -> float:
	var offset := pos - car.global_position
	offset.y = 0.0
	var ahead := offset.dot(forward)
	if ahead <= 0.0 or ahead > 40.0:
		return INF
	var side := (offset - forward * ahead).length()
	if side > max_side:
		return INF
	return ahead - car.half_length - other_half_length
