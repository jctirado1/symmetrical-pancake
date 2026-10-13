@tool
class_name ModelProp
extends Node3D
## A slot for one of your own models (a building, car, lamp post...).
##
## Drag a model file from the FileSystem panel onto "Model" in the Inspector.
## It appears straight away in the editor and is solid when you play.

## The model to show: a .glb, .gltf, .fbx or .obj file.
@export var model: Resource:
	set(value):
		model = value
		_rebuild()

## Optional .png to paint onto the model (useful for .obj files that show up white).
@export var texture: Texture2D:
	set(value):
		texture = value
		_rebuild()

## Make the model solid so the player bumps into it instead of walking through.
@export var solid := true

var _instance: Node3D


func _ready() -> void:
	_rebuild()
	if solid and _instance and not Engine.is_editor_hint():
		ModelLoader.add_collision(_instance)


func _rebuild() -> void:
	if not is_inside_tree():
		return
	if _instance:
		_instance.queue_free()
		_instance = null
	if model:
		_instance = ModelLoader.instantiate(model, texture)
		if _instance:
			add_child(_instance)
