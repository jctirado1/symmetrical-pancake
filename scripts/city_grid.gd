@tool
class_name CityGrid
extends Node3D
## Builds a whole grid of London streets: roads, junctions with zebra
## crossings, pavements, street lamps, buildings and parks.
##
## Everything is generated from the settings in the Inspector, so change a
## number (or the Random Seed) and the city rebuilds itself. Point the
## "Models" folders at your city packs and those buildings are used instead
## of the plain boxes.
##
## The city is centred on this node: there's always a crossroads at its
## origin (0, 0, 0).

enum Zone { CITY, SUBURBAN, INDUSTRIAL, PARK }

@export_group("Layout")
## How many blocks across (east-west) and deep (north-south).
@export_range(1, 20) var blocks_x := 6:
	set(value):
		blocks_x = value
		_queue_rebuild()
@export_range(1, 20) var blocks_z := 6:
	set(value):
		blocks_z = value
		_queue_rebuild()
## Size of each block of buildings, in metres.
@export_range(30.0, 200.0, 1.0) var block_size := 60.0:
	set(value):
		block_size = value
		_queue_rebuild()
## Width of the roads (two lanes), in metres.
@export_range(6.0, 30.0, 0.5) var road_width := 12.0:
	set(value):
		road_width = value
		_queue_rebuild()
## Width of the pavement around each block.
@export_range(2.0, 10.0, 0.5) var pavement_width := 4.0:
	set(value):
		pavement_width = value
		_queue_rebuild()
## Change this to get a different random city.
@export var random_seed := 1:
	set(value):
		random_seed = value
		_queue_rebuild()
## Chance (0 to 1) that a block in the middle of town becomes a park.
@export_range(0.0, 1.0, 0.01) var park_chance := 0.12:
	set(value):
		park_chance = value
		_queue_rebuild()
## Put industrial buildings along the east edge of the city.
@export var industrial_edge := true:
	set(value):
		industrial_edge = value
		_queue_rebuild()
@export var street_lamps := true:
	set(value):
		street_lamps = value
		_queue_rebuild()

@export_group("Building Models")
## Folder of building models for the middle of town (offices, shops, London
## buildings). Leave empty to use plain boxes.
@export_dir var city_models_folder := "":
	set(value):
		city_models_folder = value
		_queue_rebuild()
## Folder of building models for the outer ring of blocks (houses).
@export_dir var suburban_models_folder := "":
	set(value):
		suburban_models_folder = value
		_queue_rebuild()
## Folder of building models for the industrial edge (warehouses, factories).
@export_dir var industrial_models_folder := "":
	set(value):
		industrial_models_folder = value
		_queue_rebuild()
## Resize each model so it fits its plot. Untick to keep the models' own size
## (then use Model Scale to match them to the world).
@export var fit_models_to_plots := true:
	set(value):
		fit_models_to_plots = value
		_queue_rebuild()
@export_range(0.01, 100.0, 0.01, "or_greater") var model_scale := 1.0:
	set(value):
		model_scale = value
		_queue_rebuild()
## Tick this if your building models face away from the road.
@export var flip_models := false:
	set(value):
		flip_models = value
		_queue_rebuild()

const PAVEMENT_HEIGHT := 0.08

var _rng := RandomNumberGenerator.new()
var _rebuild_queued := false
var _generated: Node3D
var _collision: StaticBody3D
var _box_mesh := BoxMesh.new()
var _batches := {}  # Material -> Array of Transform3D, drawn as one MultiMesh each.
var _model_lists := {}  # Zone -> Array of model file paths.
var _block_zones := {}  # Vector2i block -> Zone.

# Materials, made once per rebuild.
var _mat_road: StandardMaterial3D
var _mat_pavement: StandardMaterial3D
var _mat_grass: StandardMaterial3D
var _mat_white: StandardMaterial3D
var _mat_roof: StandardMaterial3D
var _mat_lamp: StandardMaterial3D
var _mat_trunk: StandardMaterial3D
var _mat_leaves: StandardMaterial3D
var _facades := {}  # Zone -> Array of facade materials.


func _ready() -> void:
	_rebuild()


## Rebuilds once at the end of the frame, however many settings changed.
func _queue_rebuild() -> void:
	if _rebuild_queued or not is_inside_tree():
		return
	_rebuild_queued = true
	_rebuild.call_deferred()


## Distance from the city centre to the outer edge of the road grid.
func get_half_extents() -> Vector2:
	var pitch := block_size + road_width
	return Vector2(blocks_x * pitch + road_width, blocks_z * pitch + road_width) / 2.0


## How far each zebra crossing is from the middle of its junction. It lines
## up with the middle of the pavement, where pedestrians walk.
func crossing_offset() -> float:
	return road_width / 2.0 + pavement_width / 2.0


## The square a block covers, seen from above (x and z, in metres).
func get_block_rect(bx: int, bz: int) -> Rect2:
	var x0 := _road_x(bx) + road_width / 2.0
	var z0 := _road_z(bz) + road_width / 2.0
	return Rect2(x0, z0, block_size, block_size)


## What kind of block it is: Zone.CITY, SUBURBAN, INDUSTRIAL or PARK.
func get_block_zone(bx: int, bz: int) -> Zone:
	return _block_zones.get(Vector2i(bx, bz), Zone.CITY)


## Number of junctions across and deep (one more than the number of blocks).
func get_junction_count() -> Vector2i:
	return Vector2i(blocks_x + 1, blocks_z + 1)


## Centre of junction ([param i], [param j]) at road level.
func get_junction_position(i: int, j: int) -> Vector3:
	return Vector3(_road_x(i), 0.0, _road_z(j))


## Centre line of road number [param index] (0 = westmost / northmost).
func _road_x(index: int) -> float:
	return -get_half_extents().x + road_width / 2.0 + index * (block_size + road_width)


func _road_z(index: int) -> float:
	return -get_half_extents().y + road_width / 2.0 + index * (block_size + road_width)


func _rebuild() -> void:
	_rebuild_queued = false
	if _generated:
		_generated.queue_free()
	_generated = Node3D.new()
	_generated.name = "Generated"
	add_child(_generated)
	_collision = StaticBody3D.new()
	_collision.name = "Collision"
	_generated.add_child(_collision)
	_batches.clear()
	_block_zones.clear()
	_rng.seed = random_seed
	_box_mesh.size = Vector3.ONE

	_make_materials()
	_model_lists = {
		Zone.CITY: ModelLoader.find_model_files(city_models_folder),
		Zone.SUBURBAN: ModelLoader.find_model_files(suburban_models_folder),
		Zone.INDUSTRIAL: ModelLoader.find_model_files(industrial_models_folder),
	}

	_build_ground()
	for bx in blocks_x:
		for bz in blocks_z:
			_build_block(bx, bz)
	_build_road_markings()
	_build_boundary()
	_flush_batches()


# --- Ground and roads ---------------------------------------------------------

func _build_ground() -> void:
	var half := get_half_extents()
	# One big slab of asphalt under the whole grid; blocks sit on top of it.
	_add_box(Vector3(0, -0.1, 0), Vector3(half.x * 2.0, 0.2, half.y * 2.0), _mat_road, true)
	# Grass all around the city (kept well below the road so they don't flicker).
	_add_box(Vector3(0, -0.4, 0), Vector3(half.x * 2.0 + 200.0, 0.2, half.y * 2.0 + 200.0), _mat_grass, true)


func _build_road_markings() -> void:
	var half := get_half_extents()
	var crossing_length := 3.0
	var gap := crossing_offset() + crossing_length / 2.0 + 1.0  # Keep dashes clear of crossings.

	# Roads running east-west (one per row of junctions), then north-south.
	for j in blocks_z + 1:
		var z := _road_z(j)
		for i in blocks_x:
			var start := _road_x(i) + gap
			var end := _road_x(i + 1) - gap
			var x := start
			while x + 3.0 <= end:
				_add_box(Vector3(x + 1.5, 0.0, z), Vector3(3.0, 0.08, 0.15), _mat_white)
				x += 6.0
	for i in blocks_x + 1:
		var x := _road_x(i)
		for j in blocks_z:
			var start := _road_z(j) + gap
			var end := _road_z(j + 1) - gap
			var z := start
			while z + 3.0 <= end:
				_add_box(Vector3(x, 0.0, z + 1.5), Vector3(0.15, 0.08, 3.0), _mat_white)
				z += 6.0

	# Zebra crossings on every road leading out of every junction.
	for i in blocks_x + 1:
		for j in blocks_z + 1:
			var centre := Vector3(_road_x(i), 0.0, _road_z(j))
			var offset := crossing_offset()
			if i < blocks_x: _add_zebra(centre + Vector3(offset, 0, 0), true)
			if i > 0: _add_zebra(centre - Vector3(offset, 0, 0), true)
			if j < blocks_z: _add_zebra(centre + Vector3(0, 0, offset), false)
			if j > 0: _add_zebra(centre - Vector3(0, 0, offset), false)


## A zebra crossing: white stripes running the same way as the traffic.
func _add_zebra(centre: Vector3, road_runs_along_x: bool) -> void:
	var stripes := int(road_width / 1.0)
	for s in stripes:
		var across := -road_width / 2.0 + 0.5 + s * 1.0
		if road_runs_along_x:
			_add_box(centre + Vector3(0, 0, across), Vector3(3.0, 0.08, 0.5), _mat_white)
		else:
			_add_box(centre + Vector3(across, 0, 0), Vector3(0.5, 0.08, 3.0), _mat_white)


## Invisible walls just outside the grass so nobody drives off the world.
func _build_boundary() -> void:
	var half := get_half_extents() + Vector2(60, 60)
	for side in [-1.0, 1.0]:
		_add_collision_box(Vector3(half.x * side, 5, 0), Vector3(1, 20, half.y * 2.0))
		_add_collision_box(Vector3(0, 5, half.y * side), Vector3(half.x * 2.0, 20, 1))


# --- Blocks -------------------------------------------------------------------

func _zone_for(bx: int, bz: int) -> Zone:
	if industrial_edge and blocks_x >= 3 and bx == blocks_x - 1:
		return Zone.INDUSTRIAL
	var outer := bx == 0 or bz == 0 or bx == blocks_x - 1 or bz == blocks_z - 1
	if outer and blocks_x >= 3 and blocks_z >= 3:
		return Zone.SUBURBAN
	if _rng.randf() < park_chance:
		return Zone.PARK
	return Zone.CITY


func _build_block(bx: int, bz: int) -> void:
	var zone := _zone_for(bx, bz)
	_block_zones[Vector2i(bx, bz)] = zone
	var x0 := _road_x(bx) + road_width / 2.0  # West edge of the block.
	var z0 := _road_z(bz) + road_width / 2.0  # North edge of the block.
	var centre := Vector3(x0 + block_size / 2.0, 0, z0 + block_size / 2.0)

	# Raised pavement slab covering the whole block.
	_add_box(centre + Vector3(0, PAVEMENT_HEIGHT - 0.1, 0), Vector3(block_size, 0.2, block_size), _mat_pavement, true)

	if street_lamps:
		_build_lamps(x0, z0)

	if zone == Zone.PARK:
		_build_park(centre)
		return

	# Buildings stand in rows along all four sides of the block, facing the road.
	var inner := block_size - pavement_width * 2.0
	var depth := _plot_depth(zone, inner)
	var lo := pavement_width  # Distance from the block edge to the building line.
	# North and south rows run the full width of the block...
	_build_row(zone, Vector3(x0 + lo, 0, z0 + lo), Vector3.RIGHT, Vector3.BACK, inner, depth)
	_build_row(zone, Vector3(x0 + block_size - lo, 0, z0 + block_size - lo), Vector3.LEFT, Vector3.FORWARD, inner, depth)
	# ...and the east and west rows fill the gap between them.
	var side_length := inner - depth * 2.0
	if side_length > 6.0:
		_build_row(zone, Vector3(x0 + lo, 0, z0 + block_size - lo - depth), Vector3.FORWARD, Vector3.RIGHT, side_length, depth)
		_build_row(zone, Vector3(x0 + block_size - lo, 0, z0 + lo + depth), Vector3.BACK, Vector3.LEFT, side_length, depth)


func _plot_depth(zone: Zone, inner: float) -> float:
	var wanted: float
	match zone:
		Zone.SUBURBAN: wanted = 12.0
		Zone.INDUSTRIAL: wanted = 20.0
		_: wanted = 16.0
	return minf(wanted, inner / 2.0)


## Fills one side of a block with buildings.
## [param start] is the corner where the row begins (on the building line),
## [param along] the direction the row runs and [param inward] points from the
## road into the block.
func _build_row(zone: Zone, start: Vector3, along: Vector3, inward: Vector3, length: float, depth: float) -> void:
	var used := 0.0
	while length - used > 4.0:
		var width: float
		match zone:
			Zone.SUBURBAN: width = _rng.randf_range(8.0, 12.0)
			Zone.INDUSTRIAL: width = _rng.randf_range(16.0, 28.0)
			_: width = _rng.randf_range(9.0, 18.0)
		if length - used - width < 6.0:
			width = length - used  # Stretch the last plot to the end of the row.

		var gap := 0.0
		if zone == Zone.SUBURBAN:
			gap = minf(2.0, width * 0.2)  # Space between houses.
		var plot_centre := start + along * (used + width / 2.0) + inward * (depth / 2.0)
		_build_building(zone, plot_centre, along, inward, width - gap, depth)
		used += width


func _build_building(zone: Zone, plot_centre: Vector3, along: Vector3, inward: Vector3, width: float, depth: float) -> void:
	# Turn the plot so its local +Z points at the road (models usually face +Z).
	var to_road := -inward
	var plot_basis := Basis(Vector3.UP.cross(to_road), Vector3.UP, to_road)
	plot_centre.y = PAVEMENT_HEIGHT

	var models: Array = _model_lists.get(zone, [])
	if not models.is_empty():
		_place_model(models[_rng.randi() % models.size()], plot_centre, plot_basis, width, depth)
		return

	var height: float
	match zone:
		Zone.SUBURBAN: height = _rng.randf_range(6.0, 9.0)
		Zone.INDUSTRIAL: height = _rng.randf_range(7.0, 12.0)
		_: height = [9.0, 12.0, 12.0, 15.0, 18.0, 21.0, 27.0][_rng.randi() % 7]
	var size := Vector3(width, height, depth)
	var walls: Array = _facades[zone]
	var middle := plot_centre + Vector3.UP * height / 2.0
	_add_batch(walls[_rng.randi() % walls.size()], Transform3D(plot_basis * Basis.from_scale(size), middle))
	# A flat roof so the window pattern doesn't show on top.
	var roof_size := Vector3(width + 0.3, 0.4, depth + 0.3)
	_add_batch(_mat_roof, Transform3D(plot_basis * Basis.from_scale(roof_size), plot_centre + Vector3.UP * (height + 0.2)))
	_add_collision_box(middle, size, plot_basis)


func _place_model(path: String, plot_centre: Vector3, plot_basis: Basis, width: float, depth: float) -> void:
	var model := ModelLoader.instantiate(load(path))
	if model == null:
		return
	var aabb := ModelLoader.get_aabb(model)
	if aabb.size.x <= 0.0 or aabb.size.z <= 0.0:
		model.free()
		return

	var fit_scale := model_scale
	if fit_models_to_plots:
		fit_scale = minf(width / aabb.size.x, depth / aabb.size.z)
	var turn := Basis.IDENTITY if not flip_models else Basis(Vector3.UP, PI)
	# Move the model so the bottom-centre of its box sits on the plot centre.
	var bottom_centre := Vector3(aabb.get_center().x, aabb.position.y, aabb.get_center().z)
	var model_basis := plot_basis * turn * Basis.from_scale(Vector3.ONE * fit_scale)
	model.transform = Transform3D(model_basis, plot_centre - model_basis * bottom_centre)
	_generated.add_child(model)

	var size := aabb.size * fit_scale
	_add_collision_box(plot_centre + Vector3.UP * size.y / 2.0, size, plot_basis * turn)


func _build_park(centre: Vector3) -> void:
	var grass_size := block_size - pavement_width * 2.0
	_add_box(centre + Vector3(0, PAVEMENT_HEIGHT + 0.1 - 0.1, 0), Vector3(grass_size, 0.2, grass_size), _mat_grass)
	for t in int(grass_size * grass_size / 120.0):
		var pos := centre + Vector3(
			_rng.randf_range(-grass_size, grass_size) * 0.42, PAVEMENT_HEIGHT,
			_rng.randf_range(-grass_size, grass_size) * 0.42)
		var tree_height := _rng.randf_range(4.0, 7.0)
		_add_box(pos + Vector3.UP * tree_height / 2.0, Vector3(0.4, tree_height, 0.4), _mat_trunk, false)
		var crown := _rng.randf_range(3.0, 5.0)
		_add_box(pos + Vector3.UP * (tree_height + crown * 0.3), Vector3(crown, crown * 0.8, crown), _mat_leaves, false)
		_add_collision_box(pos + Vector3.UP * tree_height / 2.0, Vector3(0.4, tree_height, 0.4))


func _build_lamps(x0: float, z0: float) -> void:
	var inset := 0.6  # Distance from the kerb.
	var spacing := 20.0
	var count := int(block_size / spacing)
	for k in count:
		var t := (k + 0.5) * block_size / count
		_add_lamp(Vector3(x0 + t, 0, z0 + inset))
		_add_lamp(Vector3(x0 + t, 0, z0 + block_size - inset))
		_add_lamp(Vector3(x0 + inset, 0, z0 + t))
		_add_lamp(Vector3(x0 + block_size - inset, 0, z0 + t))


func _add_lamp(pos: Vector3) -> void:
	pos.y = PAVEMENT_HEIGHT
	_add_box(pos + Vector3.UP * 2.75, Vector3(0.15, 5.5, 0.15), _mat_lamp)
	_add_box(pos + Vector3.UP * 5.6, Vector3(0.5, 0.25, 0.5), _mat_lamp)


# --- Helpers ------------------------------------------------------------------

## Queues a box to be drawn. Boxes with the same material are drawn together,
## which keeps the game fast even with thousands of them.
func _add_box(centre: Vector3, size: Vector3, material: Material, solid := false) -> void:
	_add_batch(material, Transform3D(Basis.from_scale(size), centre))
	if solid:
		_add_collision_box(centre, size)


func _add_batch(material: Material, xform: Transform3D) -> void:
	if not _batches.has(material):
		_batches[material] = []
	_batches[material].append(xform)


func _add_collision_box(centre: Vector3, size: Vector3, rotation_basis := Basis.IDENTITY) -> void:
	var shape := BoxShape3D.new()
	shape.size = size
	var collision_shape := CollisionShape3D.new()
	collision_shape.shape = shape
	collision_shape.transform = Transform3D(rotation_basis.orthonormalized(), centre)
	_collision.add_child(collision_shape)


func _flush_batches() -> void:
	for material in _batches:
		var transforms: Array = _batches[material]
		var multimesh := MultiMesh.new()
		multimesh.transform_format = MultiMesh.TRANSFORM_3D
		multimesh.mesh = _box_mesh
		multimesh.instance_count = transforms.size()
		for k in transforms.size():
			multimesh.set_instance_transform(k, transforms[k])
		var instance := MultiMeshInstance3D.new()
		instance.multimesh = multimesh
		instance.material_override = material
		_generated.add_child(instance)


func _make_materials() -> void:
	_mat_road = _plain(Color(0.18, 0.18, 0.2))
	_mat_pavement = _plain(Color(0.42, 0.42, 0.4))
	_mat_grass = _plain(Color(0.25, 0.38, 0.2))
	_mat_white = _plain(Color(0.92, 0.92, 0.88))
	_mat_roof = _plain(Color(0.22, 0.22, 0.24))
	_mat_lamp = _plain(Color(0.12, 0.14, 0.12), 0.5)
	_mat_trunk = _plain(Color(0.3, 0.22, 0.15))
	_mat_leaves = _plain(Color(0.2, 0.42, 0.18))

	var glass := Color(0.22, 0.27, 0.33)
	_facades = {
		Zone.CITY: [
			_facade(Color(0.55, 0.27, 0.2), glass, 3.5),   # red brick
			_facade(Color(0.62, 0.36, 0.26), glass, 3.5),  # orange brick
			_facade(Color(0.82, 0.78, 0.68), glass, 3.8),  # Portland stone
			_facade(Color(0.7, 0.66, 0.6), glass, 3.8),    # grey stone
			_facade(Color(0.45, 0.5, 0.55), Color(0.5, 0.65, 0.75), 3.5, true),  # glass office
		],
		Zone.SUBURBAN: [
			_facade(Color(0.6, 0.32, 0.24), glass, 3.0),
			_facade(Color(0.85, 0.82, 0.75), glass, 3.0),  # white render
			_facade(Color(0.5, 0.3, 0.25), glass, 3.0),
		],
		Zone.INDUSTRIAL: [
			_corrugated(Color(0.5, 0.52, 0.5)),
			_corrugated(Color(0.35, 0.42, 0.48)),
			_corrugated(Color(0.55, 0.45, 0.35)),
		],
	}


func _plain(color: Color, roughness := 0.9) -> StandardMaterial3D:
	var material := StandardMaterial3D.new()
	material.albedo_color = color
	material.roughness = roughness
	return material


## A wall texture with one window per [param floor_height] metres. "World
## triplanar" mapping keeps the windows the same size on every building.
func _facade(wall: Color, window: Color, floor_height: float, big_windows := false) -> StandardMaterial3D:
	var image := Image.create(32, 32, false, Image.FORMAT_RGB8)
	image.fill(wall)
	if big_windows:
		image.fill_rect(Rect2i(2, 4, 28, 24), window)
	else:
		image.fill_rect(Rect2i(9, 6, 14, 18), window)
	return _textured(image, floor_height)


func _corrugated(wall: Color) -> StandardMaterial3D:
	var image := Image.create(32, 32, false, Image.FORMAT_RGB8)
	image.fill(wall)
	for x in range(0, 32, 4):
		image.fill_rect(Rect2i(x, 0, 1, 32), wall.darkened(0.25))
	return _textured(image, 2.0)


func _textured(image: Image, tile_size: float) -> StandardMaterial3D:
	# Enlarge without blurring so window edges stay sharp up close.
	image.resize(256, 256, Image.INTERPOLATE_NEAREST)
	image.generate_mipmaps()
	var material := StandardMaterial3D.new()
	material.albedo_texture = ImageTexture.create_from_image(image)
	material.uv1_triplanar = true
	material.uv1_world_triplanar = true
	material.uv1_scale = Vector3.ONE / tile_size
	material.roughness = 0.85
	return material
