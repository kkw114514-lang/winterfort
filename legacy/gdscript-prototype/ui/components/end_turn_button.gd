extends Button
class_name EndTurnButtonView

signal end_turn_pressed

const THEME_PATH := "res://ui/theme.tres"


func _ready() -> void:
	if theme == null:
		theme = load(THEME_PATH)
	theme_type_variation = "ButtonGold"
	custom_minimum_size = BattleLayout.END_TURN.size
	text = "结束回合"
	pressed.connect(func() -> void: end_turn_pressed.emit())


func set_enabled(is_enabled: bool) -> void:
	disabled = not is_enabled
