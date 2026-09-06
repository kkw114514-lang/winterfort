extends Panel
class_name CardView

signal card_clicked(card_view)

const THEME_PATH := "res://ui/theme.tres"

const RARITY_VARIATIONS := {
	"common": "CardFace",
	"white": "CardFace",
	"blue": "CardFaceRareBlue",
	"rare": "CardFaceRareBlue",
	"gold": "CardFaceRareGold",
	"legend": "CardFaceRareGold",
	"red": "CardFaceRareRed",
}
const TYPE_LABELS := {
	"attack": "攻击",
	"skill": "技能",
	"power": "能力",
}
const TYPE_THEME_COLORS := {
	"attack": "type_attack",
	"skill": "type_skill",
	"power": "type_power",
}

var _data := {}
var _rarity := "common"
var _affordable := true
var _selected := false

var _cost_icon: TextureRect
var _cost_label: Label
var _name_label: Label
var _art_window: ColorRect
var _art_label: Label
var _type_bar: ColorRect
var _type_label: Label
var _description_label: Label
var _keyword_row: HBoxContainer
var _selected_frame: Panel


func _ready() -> void:
	_ensure_nodes()


func _notification(what: int) -> void:
	if what == NOTIFICATION_RESIZED:
		_layout_nodes()


func set_card(data: Dictionary) -> void:
	_ensure_nodes()
	_data = data.duplicate(true)
	_rarity = str(_data.get("rarity", "common")).to_lower()
	theme_type_variation = _rarity_variation(_rarity)

	var cost := int(_data.get("cost", 0))
	var card_name := str(_data.get("name", "未命名"))
	var card_type := _normalize_type(str(_data.get("type", "")))
	var description := str(_data.get("description", ""))
	var element := str(_data.get("element", ""))
	var art_key := str(_data.get("art_key", ""))

	_cost_label.text = str(cost)
	_name_label.text = card_name
	_type_label.text = TYPE_LABELS.get(card_type, "类型")
	_description_label.text = description if not description.is_empty() else "效果描述待卡表补齐"
	_art_window.color = _element_color(element, card_type)
	_art_label.text = art_key if not art_key.is_empty() else _art_placeholder(element)
	_type_bar.color = _type_color(card_type)
	_refresh_keywords(_data.get("keywords", []))
	_apply_state()
	_layout_nodes()


func set_affordable(is_affordable: bool) -> void:
	_ensure_nodes()
	_affordable = is_affordable
	_apply_state()


func set_selected(is_selected: bool) -> void:
	_ensure_nodes()
	_selected = is_selected
	_apply_state()


func _gui_input(event: InputEvent) -> void:
	if event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_LEFT and event.pressed:
		card_clicked.emit(self)
		accept_event()


func _ensure_nodes() -> void:
	if _cost_icon != null:
		return
	if theme == null:
		theme = load(THEME_PATH)
	custom_minimum_size = BattleLayout.CARD_SIZE
	size = BattleLayout.CARD_SIZE
	theme_type_variation = "CardFace"

	_cost_icon = TextureRect.new()
	_cost_icon.texture = load("res://ui/icons/cost_gem.svg")
	_cost_icon.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	_cost_icon.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(_cost_icon)

	_cost_label = Label.new()
	_cost_label.theme_type_variation = "PreviewBody"
	_cost_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_cost_label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	_cost_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(_cost_label)

	_name_label = Label.new()
	_name_label.theme_type_variation = "PreviewSmall"
	_name_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_name_label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	_name_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	_name_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(_name_label)

	_art_window = ColorRect.new()
	_art_window.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(_art_window)

	_art_label = Label.new()
	_art_label.theme_type_variation = "PreviewTiny"
	_art_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_art_label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	_art_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	_art_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(_art_label)

	_type_bar = ColorRect.new()
	_type_bar.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(_type_bar)

	_type_label = Label.new()
	_type_label.theme_type_variation = "PreviewTiny"
	_type_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_type_label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	_type_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(_type_label)

	_description_label = Label.new()
	_description_label.theme_type_variation = "PreviewTiny"
	_description_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_description_label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	_description_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	_description_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(_description_label)

	_keyword_row = HBoxContainer.new()
	_keyword_row.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(_keyword_row)

	_selected_frame = Panel.new()
	_selected_frame.theme_type_variation = "CardSelectedFrame"
	_selected_frame.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_selected_frame.visible = false
	add_child(_selected_frame)

	set_card({"name": "未命名", "cost": 0})
	_layout_nodes()


func _layout_nodes() -> void:
	if _cost_icon == null:
		return
	var card_size := size
	if card_size.x <= 0 or card_size.y <= 0:
		card_size = BattleLayout.CARD_SIZE
	var margin := BattleLayout.CARD_CORNER_RADIUS
	var cost_size := Vector2.ONE * (BattleLayout.CARD_SIZE.x * 0.24)

	_cost_icon.position = Vector2(margin * 0.35, margin * 0.25)
	_cost_icon.size = cost_size
	_cost_label.position = _cost_icon.position
	_cost_label.size = cost_size

	_name_label.position = Vector2(margin + cost_size.x * 0.35, margin * 0.45)
	_name_label.size = Vector2(card_size.x - margin * 2.0 - cost_size.x * 0.3, BattleLayout.TOPHUD.size.y * 0.5)

	_art_window.position = Vector2(margin, BattleLayout.TOPHUD.size.y * 0.75)
	_art_window.size = Vector2(card_size.x - margin * 2.0, BattleLayout.CARD_SIZE.y * 0.33)
	_art_label.position = _art_window.position
	_art_label.size = _art_window.size

	_type_bar.position = Vector2(margin, _art_window.position.y + _art_window.size.y + margin * 0.45)
	_type_bar.size = Vector2(card_size.x - margin * 2.0, BattleLayout.TOPHUD.size.y * 0.38)
	_type_label.position = _type_bar.position
	_type_label.size = _type_bar.size

	_description_label.position = Vector2(margin, _type_bar.position.y + _type_bar.size.y + margin * 0.45)
	_description_label.size = Vector2(card_size.x - margin * 2.0, BattleLayout.CARD_SIZE.y * 0.21)

	_keyword_row.position = Vector2(margin, card_size.y - BattleLayout.TOPHUD.size.y * 0.55)
	_keyword_row.size = Vector2(card_size.x - margin * 2.0, BattleLayout.TOPHUD.size.y * 0.42)

	_selected_frame.position = Vector2.ZERO
	_selected_frame.size = card_size


func _refresh_keywords(raw_keywords) -> void:
	for child in _keyword_row.get_children():
		_keyword_row.remove_child(child)
		child.queue_free()
	if not (raw_keywords is Array):
		return
	for keyword in raw_keywords:
		var text := str(keyword).strip_edges()
		if text.is_empty():
			continue
		var chip := Panel.new()
		chip.theme_type_variation = "StatusChip"
		chip.custom_minimum_size = Vector2(BattleLayout.CARD_SIZE.x * 0.28, BattleLayout.TOPHUD.size.y * 0.4)
		var label := Label.new()
		label.theme_type_variation = "PreviewTiny"
		label.text = text
		label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
		label.size = chip.custom_minimum_size
		chip.add_child(label)
		_keyword_row.add_child(chip)


func _apply_state() -> void:
	var energy_color := Palette.get_color("color/energy")
	_cost_icon.modulate = energy_color if _affordable else energy_color.darkened(0.62)
	modulate = Color.WHITE if _affordable else theme.get_color("rarity_common", "ThemeTokens").darkened(0.5)
	theme_type_variation = _rarity_variation(_rarity)
	_selected_frame.visible = _selected


func _normalize_type(value: String) -> String:
	var normalized := value.to_lower()
	if normalized in ["attack", "atk", "攻击", "攻"]:
		return "attack"
	if normalized in ["skill", "技能", "技"]:
		return "skill"
	if normalized in ["power", "ability", "能力", "能"]:
		return "power"
	return "attack"


func _rarity_variation(value: String) -> String:
	return str(RARITY_VARIATIONS.get(value, RARITY_VARIATIONS["common"]))


func _type_color(card_type: String) -> Color:
	return theme.get_color(str(TYPE_THEME_COLORS.get(card_type, "type_attack")), "ThemeTokens")


func _element_color(element: String, card_type: String) -> Color:
	var normalized := element.to_lower()
	if normalized in ["fire", "burn", "火", "炎"]:
		return Palette.get_color("color/status/burn")
	if normalized in ["ice", "freeze", "水", "冰"]:
		return Palette.get_color("color/status/freeze")
	if normalized in ["poison", "毒"]:
		return Palette.get_color("color/status/poison")
	if normalized in ["dark", "mark", "暗"]:
		return Palette.get_color("color/status/mark")
	return _type_color(card_type).darkened(0.48)


func _art_placeholder(element: String) -> String:
	return "卡图槽" if element.is_empty() else "%s 卡图槽" % element
