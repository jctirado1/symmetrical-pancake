class_name ModelLoader
## Helper that turns any imported model file into something we can place in
## the world. Works with .glb / .gltf / .fbx (imported as scenes) and .obj
## (imported as a single mesh).


## Creates a copy of [param model]. Returns null if the file isn't a model.
## If [param texture] is set, it is painted onto every surface of the model,
## which is handy for .obj files that come with a separate .png texture.
static func instantiate(model: Resource, texture: Texture2D = null) -> Node3D:
	var node: Node3D = null
	if model is PackedScene:
		node = model.instantiate() as Node3D
	elif model is Mesh:
		var mesh_instance := MeshInstance3D.new()
		mesh_instance.mesh = model
		node = mesh_instance
	if node == null:
		push_warning("'%s' isn't a 3D model Godot understands." % model.resource_path)
		return null

	if texture:
		var material := StandardMaterial3D.new()
		material.albedo_texture = texture
		for mesh_instance in find_meshes(node):
			mesh_instance.material_override = material
	return node


## Lists every mesh inside [param node], however deeply nested.
static func find_meshes(node: Node) -> Array[MeshInstance3D]:
	var result: Array[MeshInstance3D] = []
	if node is MeshInstance3D:
		result.append(node)
	for child in node.get_children():
		result.append_array(find_meshes(child))
	return result


## Gives every mesh in [param node] a solid collision shape that matches its
## exact outline, so the player can't walk through it.
static func add_collision(node: Node) -> void:
	for mesh_instance in find_meshes(node):
		if mesh_instance.mesh == null:
			continue
		var body := StaticBody3D.new()
		var shape := CollisionShape3D.new()
		shape.shape = mesh_instance.mesh.create_trimesh_shape()
		body.add_child(shape)
		mesh_instance.add_child(body)


## Finds the first AnimationPlayer inside [param node] (models with
## animations, e.g. characters, have one).
static func find_animation_player(node: Node) -> AnimationPlayer:
	if node is AnimationPlayer:
		return node
	for child in node.get_children():
		var found := find_animation_player(child)
		if found:
			return found
	return null


## Measures the box that [param node] and everything inside it fits in,
## in [param node]'s own space. Works before the model is added to the scene.
static func get_aabb(node: Node3D, parent_transform := Transform3D.IDENTITY) -> AABB:
	var xform := parent_transform * node.transform
	var result := AABB()
	var has_result := false
	if node is MeshInstance3D and node.mesh:
		result = xform * node.mesh.get_aabb()
		has_result = true
	for child in node.get_children():
		if child is Node3D:
			var child_aabb := get_aabb(child, xform)
			if child_aabb.size == Vector3.ZERO:
				continue
			result = child_aabb if not has_result else result.merge(child_aabb)
			has_result = true
	return result


## Lists every model file (.glb, .gltf, .fbx, .obj) in [param folder] and its
## sub-folders, in alphabetical order. If [param optional] is true, a missing
## folder just gives an empty list instead of a warning.
static func find_model_files(folder: String, optional := false) -> Array[String]:
	var result: Array[String] = []
	if folder == "":
		return result
	var dir := DirAccess.open(folder)
	if dir == null:
		if not optional:
			push_warning("Can't open folder '%s'." % folder)
		return result
	for file in dir.get_files():
		# In an exported game the files are listed with ".import" on the end.
		file = file.trim_suffix(".import").trim_suffix(".remap")
		var path := folder.path_join(file)
		if file.get_extension().to_lower() in ["glb", "gltf", "fbx", "obj"] and path not in result:
			result.append(path)
	for sub in dir.get_directories():
		result.append_array(find_model_files(folder.path_join(sub), optional))
	result.sort()
	return result
