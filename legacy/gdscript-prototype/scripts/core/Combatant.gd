extends RefCounted
class_name Combatant

var id: String = ""
var name: String = ""
var is_player: bool = false
var max_hp: int = 0
var hp: int = 0
var alive: bool = true
var statuses: Array = []
var shield: int = 0

var speed: int = 0
var stamina: int = 0
var owner_tag: String = ""
var entered_order: int = 0

var intent: Dictionary = {}


func _init(
	combatant_id: String = "",
	combatant_name: String = "",
	player_controlled: bool = false,
	combatant_max_hp: int = 0
) -> void:
	id = combatant_id
	name = combatant_name
	is_player = player_controlled
	max_hp = combatant_max_hp
	hp = combatant_max_hp
	alive = hp > 0
