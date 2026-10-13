extends Control
## A GTA-style minimap in the corner of the screen. It shows the roads,
## blocks and parks of the city, the traffic (white dots, red buses), cars you
## can drive (blue dots) and you (the yellow arrow). It turns with your view
## so "up" is always the way you're looking, and zooms out while you drive.

## The CityGrid to draw.
@export var city: CityGrid
## How many metres across the map shows when you're on foot / driving.
@export var metres_on_foot := 180.0
@export var metres_driving := 320.0
## Untick to keep north at the top instead of turning with the camera.
@export var rotate_with_camera := true

const COLOUR_GRASS := Color(0.27, 0.36, 0.24)
const COLOUR_ROAD := Color(0.42, 0.42, 0.45)
const COLOUR_PAVEMENT := Color(0.62, 0.62, 0.6)
const COLOUR_ZONE := {
	CityGrid.Zone.CITY: Color(0.32, 0.3, 0.33),
	CityGrid.Zone.SUBURBAN: Color(0.38, 0.32, 0.28),
	CityGrid.Zone.INDUSTRIAL: Color(0.3, 0.33, 0.36),
	CityGrid.Zone.PARK: Color(0.3, 0.5, 0.26),
}

var _zoom := 180.0


func _process(delta: float) -> void:
	var wanted := metres_driving if _driven_vehicle() else metres_on_foot
	_zoom = lerpf(_zoom, wanted, clampf(2.0 * delta, 0.0, 1.0))
	queue_redraw()


func _draw() -> void:
	var map_size := size
	draw_rect(Rect2(Vector2.ZERO, map_size), COLOUR_GRASS)
	if city == null:
		return

	# Work out how world positions (x, z) map onto the minimap.
	var focus := _focus_position()
	var angle := 0.0
	var camera := get_viewport().get_camera_3d()
	if rotate_with_camera and camera:
		var look := -camera.global_basis.z
		angle = -PI / 2.0 - atan2(look.z, look.x)
	var pixels_per_metre := minf(map_size.x, map_size.y) / _zoom
	var to_map := Transform2D(angle, map_size / 2.0) \
		* Transform2D(0.0, Vector2.ONE * pixels_per_metre, 0.0, Vector2.ZERO) \
		* Transform2D(0.0, -Vector2(focus.x, focus.z))

	# The city, drawn in metres.
	draw_set_transform_matrix(to_map)
	var half := city.get_half_extents()
	draw_rect(Rect2(-half, half * 2.0), COLOUR_ROAD)
	var inset := city.pavement_width
	for bx in city.blocks_x:
		for bz in city.blocks_z:
			var block := city.get_block_rect(bx, bz)
			draw_rect(block, COLOUR_PAVEMENT)
			draw_rect(block.grow(-inset), COLOUR_ZONE[city.get_block_zone(bx, bz)])

	# Markers, drawn at a fixed size in pixels.
	draw_set_transform_matrix(Transform2D.IDENTITY)
	var traffic := get_tree().get_first_node_in_group("traffic")
	if traffic:
		for car in traffic.cars:
			var colour := Color(0.85, 0.15, 0.15) if car.is_bus else Color(0.95, 0.95, 0.95)
			draw_circle(to_map * _flat(car.global_position), 4.0 if car.is_bus else 2.5, colour)
	var driven := _driven_vehicle()
	for vehicle in get_tree().get_nodes_in_group("vehicles"):
		if vehicle != driven:
			draw_circle(to_map * _flat(vehicle.global_position), 3.5, Color(0.3, 0.6, 1.0))

	# You: an arrow pointing the way you're facing.
	var centre := to_map * _flat(focus)
	var facing := _focus_facing()
	var direction := to_map.basis_xform(Vector2(facing.x, facing.z)).normalized()
	var side := Vector2(-direction.y, direction.x)
	var arrow := PackedVector2Array([
		centre + direction * 9.0, centre - direction * 6.0 + side * 6.0,
		centre - direction * 3.0, centre - direction * 6.0 - side * 6.0,
	])
	draw_colored_polygon(arrow, Color(1.0, 0.85, 0.1))
	draw_polyline(arrow + PackedVector2Array([arrow[0]]), Color.BLACK, 1.5)

	# "N" on the edge of the map, pointing north (-Z).
	var north := to_map.basis_xform(Vector2(0, -1)).normalized()
	var north_pos := map_size / 2.0 + north * (minf(map_size.x, map_size.y) / 2.0 - 12.0)
	draw_circle(north_pos, 9.0, Color(0, 0, 0, 0.6))
	draw_string(get_theme_default_font(), north_pos + Vector2(-5, 5), "N", HORIZONTAL_ALIGNMENT_LEFT, -1, 14)

	draw_rect(Rect2(Vector2.ZERO, map_size), Color.BLACK, false, 3.0)


func _flat(pos: Vector3) -> Vector2:
	return Vector2(pos.x, pos.z)


func _player() -> Node3D:
	return get_tree().get_first_node_in_group("player") as Node3D


func _driven_vehicle() -> Node3D:
	var player := _player()
	if player == null or player.visible:
		return null
	for vehicle in get_tree().get_nodes_in_group("vehicles"):
		if vehicle.driver == player:
			return vehicle
	return null


func _focus_position() -> Vector3:
	var vehicle := _driven_vehicle()
	if vehicle:
		return vehicle.global_position
	var player := _player()
	return player.global_position if player else Vector3.ZERO


func _focus_facing() -> Vector3:
	var vehicle := _driven_vehicle()
	if vehicle:
		return vehicle.global_basis.z
	var player := _player()
	if player:
		return -player.get_node("Body").global_basis.z
	return Vector3.FORWARD
