extends Panel
class_name ComboView

const THEME_PATH := "res://ui/theme.tres"

var _label: Label


func _ready() -> void:
	_ensure_nodes()


func _notification(what: int) -> void:
	if what == NOTIFICATION_RESIZED:
		_sync_child_rects()


func set_combo(n: int) -> void:
	_ensure_nodes()
	visible = n > 0
	_label.text = "连击 ×%d" % maxi(n, 0)


func _ensure_nodes() -> void:
	if _label != null:
		return
	if theme == null:
		theme = load(THEME_PATH)
	theme_type_variation = "PanelGold"
	custom_minimum_size = Vector2(BattleLayout.CARD_SIZE.x, BattleLayout.TOPHUD.size.y * 0.9)

	_label = Label.new()
	_label.theme_type_variation = "PreviewBody"
	_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	add_child(_label)
	set_combo(0)
	_sync_child_rects()


func _sync_child_rects() -> void:
	if _label != null:
		_label.size = size if size != Vector2.ZERO else custom_minimum_size
