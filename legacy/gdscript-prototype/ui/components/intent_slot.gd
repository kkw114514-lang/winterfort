extends Panel
class_name IntentSlotView

const THEME_PATH := "res://ui/theme.tres"

const TOKEN_BY_ICON := {
	"attack": "color/enemy",
	"defend": "color/status/shield",
	"buff": "color/status/regen",
	"debuff": "color/status/burn",
	"special": "color/gold/hi",
}

var _row: HBoxContainer
var _icon: TextureRect
var _label: Label


func _ready() -> void:
	_ensure_nodes()


func _notification(what: int) -> void:
	if what == NOTIFICATION_RESIZED:
		_sync_child_rects()


func set_intent(icon_key: String, value: String, is_now: bool) -> void:
	_ensure_nodes()
	var key := _normalize_icon_key(icon_key)
	theme_type_variation = "IntentSlotNow" if is_now else "IntentSlotNext"
	custom_minimum_size = _slot_size(is_now)
	_icon.texture = load("res://ui/icons/intent_%s.svg" % key)
	_icon.modulate = Palette.get_color(str(TOKEN_BY_ICON.get(key, "color/gold/hi")))
	_label.text = value
	_sync_child_rects()


func _ensure_nodes() -> void:
	if _row != null:
		return
	if theme == null:
		theme = load(THEME_PATH)
	theme_type_variation = "IntentSlotNow"
	custom_minimum_size = _slot_size(true)

	_row = HBoxContainer.new()
	_row.alignment = BoxContainer.ALIGNMENT_CENTER
	add_child(_row)

	_icon = TextureRect.new()
	_icon.custom_minimum_size = Vector2.ONE * (BattleLayout.TOPHUD.size.y * 0.52)
	_icon.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	_row.add_child(_icon)

	_label = Label.new()
	_label.theme_type_variation = "PreviewBody"
	_row.add_child(_label)

	set_intent("attack", "0", true)
	_sync_child_rects()


func _sync_child_rects() -> void:
	if _row != null:
		_row.size = size if size != Vector2.ZERO else custom_minimum_size


func _normalize_icon_key(icon_key: String) -> String:
	var normalized := icon_key.to_lower()
	if normalized.begins_with("intent_"):
		return normalized.trim_prefix("intent_")
	return normalized


func _slot_size(is_now: bool) -> Vector2:
	var base := Vector2(BattleLayout.CARD_SIZE.x * 0.64, BattleLayout.TOPHUD.size.y * 1.16)
	return base if is_now else base * 0.86
