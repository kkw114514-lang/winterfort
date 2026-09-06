extends Panel
class_name PileView

const THEME_PATH := "res://ui/theme.tres"

const ICON_BY_KIND := {
	"draw": "pile_draw",
	"discard": "pile_discard",
	"exhaust": "pile_exhaust",
}
const COLOR_TOKEN_BY_KIND := {
	"draw": "color/parch",
	"discard": "color/parch",
	"exhaust": "color/status/mark",
}

var _row: HBoxContainer
var _icon: TextureRect
var _label: Label


func _ready() -> void:
	_ensure_nodes()


func _notification(what: int) -> void:
	if what == NOTIFICATION_RESIZED:
		_sync_child_rects()


func set_pile(kind: String, count: int) -> void:
	_ensure_nodes()
	var slot := str(ICON_BY_KIND.get(kind, ICON_BY_KIND["draw"]))
	_icon.texture = load("res://ui/icons/%s.svg" % slot)
	_icon.modulate = Palette.get_color(str(COLOR_TOKEN_BY_KIND.get(kind, COLOR_TOKEN_BY_KIND["draw"])))
	_label.text = str(maxi(count, 0))


func _ensure_nodes() -> void:
	if _row != null:
		return
	if theme == null:
		theme = load(THEME_PATH)
	theme_type_variation = "PanelGold"
	custom_minimum_size = Vector2(BattleLayout.ENERGY_ORB_DIAMETER * 0.58, BattleLayout.TOPHUD.size.y * 1.1)

	_row = HBoxContainer.new()
	_row.alignment = BoxContainer.ALIGNMENT_CENTER
	add_child(_row)

	_icon = TextureRect.new()
	_icon.custom_minimum_size = Vector2.ONE * (BattleLayout.TOPHUD.size.y * 0.58)
	_icon.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	_row.add_child(_icon)

	_label = Label.new()
	_label.theme_type_variation = "PreviewSmall"
	_row.add_child(_label)

	set_pile("draw", 0)
	_sync_child_rects()


func _sync_child_rects() -> void:
	if _row != null:
		_row.size = size if size != Vector2.ZERO else custom_minimum_size
