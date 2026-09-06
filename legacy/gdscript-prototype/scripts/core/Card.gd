extends RefCounted
class_name Card

var name: String = ""
var cost: int = 0
var owner: String = "A"
var effects: Array = []


func _init(card_name: String = "", card_cost: int = 0, card_owner: String = "A", card_effects: Array = []) -> void:
	name = card_name
	cost = card_cost
	owner = card_owner
	effects = card_effects.duplicate(true)
