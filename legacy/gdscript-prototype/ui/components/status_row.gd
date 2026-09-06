extends HBoxContainer
class_name StatusRowView

const StatusChipScene := preload("res://ui/components/status_chip.tscn")
const THEME_PATH := "res://ui/theme.tres"

const DEBUFF_ORDER := {
	"burn": 0,
	"poison": 1,
	"weak": 2,
	"freeze": 3,
	"mark": 4,
}
const BUFF_ORDER := {
	"shield": 100,
	"power": 101,
	"regen": 102,
}


func _ready() -> void:
	if theme == null:
		theme = load(THEME_PATH)
	custom_minimum_size = BattleLayout.STATUS_ROW_MIN_SIZE
	if get_child_count() == 0:
		set_statuses([])


func set_statuses(list: Array) -> void:
	_clear_children()
	if list.is_empty():
		_add_placeholder()
		return

	var sorted_statuses := list.duplicate(true)
	sorted_statuses.sort_custom(func(a, b) -> bool:
		return _sort_key(a) < _sort_key(b)
	)
	for status in sorted_statuses:
		var chip = StatusChipScene.instantiate()
		add_child(chip)
		chip.set_status(
			str(status.get("key", "mark")),
			int(status.get("level", 0)),
			int(status.get("stacks", 0))
		)


func _clear_children() -> void:
	for child in get_children():
		remove_child(child)
		child.queue_free()


func _add_placeholder() -> void:
	var placeholder := Panel.new()
	placeholder.theme_type_variation = "StatusChip"
	placeholder.custom_minimum_size = Vector2(BattleLayout.CARD_SIZE.x * 0.64, BattleLayout.TOPHUD.size.y * 0.78)
	add_child(placeholder)
	var label := Label.new()
	label.theme_type_variation = "PreviewSmall"
	label.text = "状态位"
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	label.size = placeholder.custom_minimum_size
	placeholder.add_child(label)


func _sort_key(status: Dictionary) -> int:
	var key := str(status.get("key", "")).to_lower()
	if DEBUFF_ORDER.has(key):
		return int(DEBUFF_ORDER[key])
	if BUFF_ORDER.has(key):
		return int(BUFF_ORDER[key])
	return 1000
