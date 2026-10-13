@tool
class_name AssetFinder
## Searches the whole project for character and car models (for example inside
## a GAMEPRO folder you copied in), so the game can use them without you
## setting anything up.
##
## How it decides what a model is:
## - Cars: the file or folder name has a word like "car", "sedan", "taxi",
##   "van" or "truck" in it (but not "wheel", "door" and other car parts).
## - Characters: the name has a word like "character", "man", "woman",
##   "player" or "npc", or the model has a skeleton (it's rigged for animation).
##
## The answers are remembered between runs, so only new or changed files are
## checked again. Every time something changes, a list of what was found is
## written to assets/found_assets.txt so you can see it.

const CACHE_PATH := "user://asset_catalog.cfg"
const REPORT_PATH := "res://assets/found_assets.txt"
const CACHE_VERSION := 1

const CAR_WORDS := [
	"car", "cars", "vehicle", "vehicles", "sedan", "suv", "taxi", "cab", "van", "truck",
	"lorry", "pickup", "hatchback", "coupe", "jeep", "police", "ambulance", "convertible",
	"limo", "limousine", "wagon", "minivan", "sportscar", "supercar", "racecar",
	"roadster", "automobile", "firetruck",
]
## Places named after vehicles or people ("Police_Station", "Car_Park").
const PLACE_WORDS := [
	"station", "building", "buildings", "house", "garage", "shop", "store", "sign",
	"signs", "park", "parking", "showroom", "wash", "dealership", "hole", "manhole",
]
const CAR_PART_WORDS := [
	"wheel", "wheels", "tire", "tires", "tyre", "tyres", "rim", "rims", "door", "doors",
	"interior", "seat", "seats", "steering", "engine", "bumper", "hood", "bonnet",
	"spoiler", "mirror", "mirrors", "chassis", "glass",
]
const CHARACTER_WORDS := [
	"character", "characters", "human", "humans", "person", "people", "pedestrian",
	"pedestrians", "npc", "npcs", "player", "players", "male", "female", "man", "woman",
	"men", "women", "guy", "girl", "boy", "kid", "child", "hero", "civilian", "citizen",
	"worker", "businessman", "businesswoman", "policeman", "policewoman", "officer",
	"bot", "avatar", "mannequin",
]
## Things that are definitely not characters, so they're not worth opening.
const SCENERY_WORDS := [
	"building", "buildings", "house", "houses", "shop", "store", "wall", "walls", "road",
	"roads", "street", "tree", "trees", "plant", "plants", "fence", "bench", "lamp",
	"light", "lights", "sign", "signs", "roof", "floor", "tile", "tiles", "sidewalk",
	"pavement", "ground", "terrain", "rock", "rocks", "bush", "grass", "office", "tower",
	"apartment", "apartments", "factory", "warehouse", "texture", "textures", "material",
	"materials", "window", "windows", "pipe", "pipes", "crate", "barrel", "box", "tank",
]

static var _scanned := false
static var _characters: Array[String] = []
static var _cars: Array[String] = []
static var _word_regex := RegEx.create_from_string("[A-Z]?[a-z]+|[A-Z]+(?![a-z])|[0-9]+")


## Every character model in the project, in alphabetical order.
static func characters() -> Array[String]:
	_scan()
	return _characters


## The character to use for the player: one whose name says it's the player
## or hero if there is one, otherwise the first character found.
static func player_character() -> String:
	_scan()
	for path in _characters:
		if _has_any(_words(path), ["player", "players", "hero", "main", "protagonist"]):
			return path
	return _characters[0] if not _characters.is_empty() else ""


## Every character except the player's (or all of them if there's only one).
static func other_characters() -> Array[String]:
	var others := characters().duplicate()
	if others.size() > 1:
		others.erase(player_character())
	return others


## Every car model in the project, in alphabetical order.
static func cars() -> Array[String]:
	_scan()
	return _cars


static func _scan() -> void:
	if _scanned:
		return
	_scanned = true
	var cache := ConfigFile.new()
	cache.load(CACHE_PATH)
	if cache.get_value("info", "version", 0) != CACHE_VERSION:
		cache.clear()
		cache.set_value("info", "version", CACHE_VERSION)

	var files := ModelLoader.find_model_files("res://", true)
	var changed := false
	for path in files:
		var modified := FileAccess.get_modified_time(path)
		var cached = cache.get_value("files", path, [])
		var kind: String
		if cached is Array and cached.size() == 2 and cached[0] == modified:
			kind = cached[1]
		else:
			kind = _classify(path)
			cache.set_value("files", path, [modified, kind])
			changed = true
		if kind == "character":
			_characters.append(path)
		elif kind == "car":
			_cars.append(path)

	if changed or not FileAccess.file_exists(REPORT_PATH):
		cache.save(CACHE_PATH)
		_write_report(files.size())
	if not Engine.is_editor_hint():
		print("AssetFinder: %d characters, %d cars (out of %d models). Details in %s" \
			% [_characters.size(), _cars.size(), files.size(), REPORT_PATH])


## Works out whether a model file is a "character", a "car" or "other".
static func _classify(path: String) -> String:
	var words := _words(path)
	if _has_any(words, CAR_PART_WORDS) or _has_any(words, PLACE_WORDS):
		return "other"
	if _has_any(words, CAR_WORDS):
		return "car"
	if _has_any(words, CHARACTER_WORDS):
		return "character"
	if _has_any(words, SCENERY_WORDS):
		return "other"

	# No clues in the name: open it and look for a skeleton.
	var resource := load(path)
	if resource is PackedScene:
		var node := (resource as PackedScene).instantiate()
		var rigged := _find_skeleton(node) and not ModelLoader.find_meshes(node).is_empty()
		node.free()
		if rigged:
			return "character"
	return "other"


## Splits a path like "res://GAMEPRO/People/BusinessMan_01.fbx" into lower-case
## words: gamepro, people, business, man, 01, fbx. Whole compound names
## (businessman) are kept too.
static func _words(path: String) -> PackedStringArray:
	var words := PackedStringArray()
	var text := path.trim_prefix("res://").get_basename()
	for part in text.split("/"):
		for chunk in part.split("_", false):
			for piece in chunk.split("-", false):
				for piece2 in piece.split(" ", false):
					words.append(piece2.to_lower())
					for found in _word_regex.search_all(piece2):
						words.append(found.get_string().to_lower())
	return words


static func _has_any(words: PackedStringArray, wanted: Array) -> bool:
	for word in words:
		if word in wanted:
			return true
	return false


static func _find_skeleton(node: Node) -> bool:
	if node is Skeleton3D:
		return true
	for child in node.get_children():
		if _find_skeleton(child):
			return true
	return false


## Writes assets/found_assets.txt (only when running from the editor, where
## the project folder can be written to).
static func _write_report(total: int) -> void:
	if not OS.has_feature("editor"):
		return
	var file := FileAccess.open(REPORT_PATH, FileAccess.WRITE)
	if file == null:
		return
	file.store_line("Models found by the game's AssetFinder (this file is rewritten automatically).")
	file.store_line("Searched %d model files.\n" % total)
	file.store_line("CHARACTERS (%d):" % _characters.size())
	for path in _characters:
		file.store_line("  " + path + ("   <- player" if path == player_character() else "   <- pedestrian"))
	file.store_line("\nCARS (%d) - used for the parked cars and the traffic:" % _cars.size())
	for path in _cars:
		file.store_line("  " + path)
	file.store_line("\nIf something is missing or in the wrong list, rename the file or its folder")
	file.store_line("(e.g. put cars in a folder called 'cars' and characters in one called 'characters').")
