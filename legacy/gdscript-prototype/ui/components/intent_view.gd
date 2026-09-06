extends HBoxContainer
class_name IntentView

const IntentSlotScene := preload("res://ui/components/intent_slot.tscn")
const THEME_PATH := "res://ui/theme.tres"

var _current_slot
var _next_slot


func _ready() -> void:
	if theme == null:
		theme = load(THEME_PATH)
	custom_minimum_size = Vector2(BattleLayout.CARD_SIZE.x * 1.28, BattleLayout.TOPHUD.size.y * 1.16)
	_ensure_slots()


func set_intents(current: Dictionary, next: Dictionary) -> void:
	_ensure_slots()
	_current_slot.set_intent(
		str(current.get("icon_key", "attack")),
		str(current.get("value", "")),
		true
	)
	_next_slot.set_intent(
		str(next.get("icon_key", "special")),
		str(next.get("value", "")),
		false
	)


func _ensure_slots() -> void:
	if _current_slot != null:
		return
	_current_slot = IntentSlotScene.instantiate()
	_next_slot = IntentSlotScene.instantiate()
	add_child(_current_slot)
	add_child(_next_slot)
	set_intents({"icon_key": "attack", "value": "0"}, {"icon_key": "special", "value": "?"})
