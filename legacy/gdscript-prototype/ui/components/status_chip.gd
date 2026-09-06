extends Panel
class_name StatusChipView

const THEME_PATH := "res://ui/theme.tres"

const ROMAN_LEVELS := ["", "Ⅰ", "Ⅱ", "Ⅲ", "Ⅳ"]
const TOKEN_BY_KEY := {
	"burn": "color/status/burn",
	"poison": "color/status/poison",
	"shield": "color/status/shield",
	"power": "color/status/power",
	"regen": "color/status/regen",
	"weak": "color/status/weak",
	"freeze": "color/status/freeze",
	"mark": "color/status/mark",
}

var _row: HBoxContainer
var _icon: TextureRect
var _label: Label


func _ready() -> void:
	_ensure_nodes()


func _notification(what: int) -> void:
	if what == NOTIFICATION_RESIZED:
		_sync_child_rects()


func set_status(key: String, level: int, stacks: int) -> void:
	_ensure_nodes()
	var status_key := key.to_lower()
	_icon.texture = load("res://ui/icons/status_%s.svg" % status_key)
	_icon.modulate = Palette.get_color(str(TOKEN_BY_KEY.get(status_key, "color/parch")))
	if status_key == "burn":
		_label.text = "%s ×%d" % [_roman_level(level), maxi(stacks, 0)]
	else:
		_label.text = str(maxi(stacks, 0))


func _ensure_nodes() -> void:
	if _row != null:
		return
	if theme == null:
		theme = load(THEME_PATH)
	theme_type_variation = "StatusChip"
	custom_minimum_size = Vector2(BattleLayout.CARD_SIZE.x * 0.64, BattleLayout.TOPHUD.size.y * 0.78)

	_row = HBoxContainer.new()
	_row.alignment = BoxContainer.ALIGNMENT_CENTER
	add_child(_row)

	_icon = TextureRect.new()
	_icon.custom_minimum_size = Vector2.ONE * (BattleLayout.TOPHUD.size.y * 0.42)
	_icon.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	_row.add_child(_icon)

	_label = Label.new()
	_label.theme_type_variation = "PreviewSmall"
	_row.add_child(_label)

	set_status("mark", 0, 0)
	_sync_child_rects()


func _sync_child_rects() -> void:
	if _row != null:
		_row.size = size if size != Vector2.ZERO else custom_minimum_size


func _roman_level(level: int) -> String:
	return ROMAN_LEVELS[clampi(level, 1, ROMAN_LEVELS.size() - 1)]
