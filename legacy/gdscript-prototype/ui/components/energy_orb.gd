extends Panel
class_name EnergyOrbView

const THEME_PATH := "res://ui/theme.tres"

var _label: Label


func _ready() -> void:
	_ensure_nodes()


func _notification(what: int) -> void:
	if what == NOTIFICATION_RESIZED:
		_sync_child_rects()


func set_energy(cur: int, max_energy: int) -> void:
	_ensure_nodes()
	_label.text = "%d/%d" % [cur, max_energy]


func _ensure_nodes() -> void:
	if _label != null:
		return
	if theme == null:
		theme = load(THEME_PATH)
	theme_type_variation = "EnergyOrb"
	custom_minimum_size = Vector2.ONE * BattleLayout.ENERGY_ORB_DIAMETER

	_label = Label.new()
	_label.theme_type_variation = "PreviewTitle"
	_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(_label)
	set_energy(0, 0)
	_sync_child_rects()


func _sync_child_rects() -> void:
	if _label != null:
		_label.size = size if size != Vector2.ZERO else custom_minimum_size
