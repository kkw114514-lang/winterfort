extends Control
class_name CombatantView

signal view_clicked(view)

class UnitShadow:
	extends Control

	var _shadow_color: Color

	func _notification(what: int) -> void:
		if what == NOTIFICATION_RESIZED:
			queue_redraw()

	func set_shadow_color(value: Color) -> void:
		_shadow_color = value
		queue_redraw()

	func _draw() -> void:
		if size.x <= 0.0 or size.y <= 0.0:
			return
		var radius := size.y * 0.5
		draw_set_transform(size * 0.5, 0.0, Vector2(size.x / size.y, 1.0))
		for i in range(BattleLayout.UNIT_SHADOW_SOFT_STEPS):
			var t := float(i + 1) / float(BattleLayout.UNIT_SHADOW_SOFT_STEPS)
			var color := _shadow_color
			color.a *= t * t
			draw_circle(Vector2.ZERO, radius * (1.0 - (1.0 - t) * BattleLayout.UNIT_SHADOW_EDGE_PULL), color)

const THEME_PATH := "res://ui/theme.tres"
const HealthBarScene := preload("res://ui/components/health_bar.tscn")
const StatusRowScene := preload("res://ui/components/status_row.tscn")
const IntentViewScene := preload("res://ui/components/intent_view.tscn")

var _is_player := true
var _alive := true
var _show_name := false
var _view_scale := 1.0
var _portrait_base_size := Vector2(BattleLayout.PORTRAIT_BASE_ALLY, BattleLayout.PORTRAIT_BASE_ALLY)
var _portrait_size := Vector2(BattleLayout.PORTRAIT_BASE_ALLY, BattleLayout.PORTRAIT_BASE_ALLY)

var _shadow: UnitShadow
var _portrait: TextureRect
var _portrait_placeholder: ColorRect
var _name_label: Label
var _health_bar
var _status_row
var _intent_view
var _target_ring: TextureRect


func _ready() -> void:
	_ensure_nodes()


func _notification(what: int) -> void:
	if what == NOTIFICATION_RESIZED:
		_layout_nodes()


func set_combatant(data: Dictionary) -> void:
	_ensure_nodes()
	_is_player = bool(data.get("is_player", true))
	_alive = bool(data.get("alive", true))
	var hp := int(data.get("hp", 0))
	var max_hp := int(data.get("max_hp", 1))
	var shield := int(data.get("shield", 0))
	var statuses = data.get("statuses", [])
	var portrait_path := str(data.get("portrait_path", ""))
	var flip_h := bool(data.get("flip_h", false))

	_name_label.text = str(data.get("name", "Unknown"))
	_name_label.visible = _show_name
	_health_bar.set_values(hp, max_hp, _is_player, shield)
	_status_row.set_statuses(statuses if statuses is Array else [])
	_refresh_portrait(portrait_path, flip_h)
	_refresh_intent(data)
	_apply_alive_state()
	_layout_nodes()


func set_show_name(should_show: bool) -> void:
	_ensure_nodes()
	_show_name = should_show
	_name_label.visible = _show_name


func set_view_scale(value: float) -> void:
	_ensure_nodes()
	_view_scale = clampf(value, BattleLayout.SCALE_MIN, BattleLayout.SCALE_MAX)
	_update_scaled_metrics()
	_layout_nodes()


func set_targeted(should_target: bool) -> void:
	_ensure_nodes()
	_target_ring.visible = should_target


func is_targeted() -> bool:
	_ensure_nodes()
	return _target_ring.visible


func portrait_width() -> float:
	_ensure_nodes()
	return _portrait_size.x


func view_width() -> float:
	_ensure_nodes()
	return size.x if size.x > 0.0 else custom_minimum_size.x


func foot_line() -> float:
	return BattleLayout.FOOT_LINE


func portrait_global_rect() -> Rect2:
	_ensure_nodes()
	return _portrait.get_global_rect()


func health_global_rect() -> Rect2:
	_ensure_nodes()
	return _health_bar.get_global_rect()


func status_global_rect() -> Rect2:
	_ensure_nodes()
	return _status_row.get_global_rect()


func shadow_global_rect() -> Rect2:
	_ensure_nodes()
	return _shadow.get_global_rect()


func _ensure_nodes() -> void:
	if _portrait != null:
		return
	if theme == null:
		theme = load(THEME_PATH)
	mouse_filter = Control.MOUSE_FILTER_STOP
	custom_minimum_size = Vector2(BattleLayout.COMBATANT_VIEW_MIN_WIDTH, BattleLayout.FOOT_LINE + BattleLayout.COMBATANT_POST_FOOT_UI_HEIGHT)
	size = custom_minimum_size

	_shadow = UnitShadow.new()
	_shadow.name = "Shadow"
	_shadow.set_shadow_color(theme.get_color("unit_shadow", "ThemeTokens"))
	_shadow.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(_shadow)

	_target_ring = TextureRect.new()
	_target_ring.name = "TargetRing"
	_target_ring.texture = load("res://ui/icons/target_ring.svg")
	_target_ring.modulate = Palette.get_color("color/enemy")
	_target_ring.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	_target_ring.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_target_ring.visible = false
	add_child(_target_ring)

	_portrait_placeholder = ColorRect.new()
	_portrait_placeholder.name = "PortraitPlaceholder"
	_portrait_placeholder.color = theme.get_color("gold_d", "ThemeTokens")
	_portrait_placeholder.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(_portrait_placeholder)

	_portrait = TextureRect.new()
	_portrait.name = "Portrait"
	_portrait.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	_portrait.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	_portrait.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(_portrait)

	_name_label = Label.new()
	_name_label.name = "Name"
	_name_label.theme_type_variation = "PreviewSmall"
	_name_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_name_label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	_name_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_name_label.visible = false
	add_child(_name_label)

	_health_bar = HealthBarScene.instantiate()
	_health_bar.name = "HealthBar"
	add_child(_health_bar)

	_status_row = StatusRowScene.instantiate()
	_status_row.name = "StatusRow"
	add_child(_status_row)

	_intent_view = IntentViewScene.instantiate()
	_intent_view.name = "IntentView"
	add_child(_intent_view)

	set_combatant({"name": "Unknown", "hp": 1, "max_hp": 1, "is_player": true})
	_layout_nodes()


func _gui_input(event: InputEvent) -> void:
	if event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_LEFT and event.pressed:
		view_clicked.emit(self)
		accept_event()


func _layout_nodes() -> void:
	if _portrait == null:
		return
	var view_size := size
	if view_size.x <= 0 or view_size.y <= 0:
		view_size = custom_minimum_size
	var portrait_pos := Vector2((view_size.x - _portrait_size.x) * 0.5, BattleLayout.FOOT_LINE - _portrait_size.y)
	var hp_width := clampf(_portrait_size.x, BattleLayout.COMBATANT_HP_MIN_WIDTH, BattleLayout.COMBATANT_HP_MAX_WIDTH)
	var hp_size := Vector2(hp_width, BattleLayout.COMBATANT_HP_HEIGHT)
	var status_size := Vector2(BattleLayout.COMBATANT_STATUS_MAX_WIDTH, BattleLayout.COMBATANT_STATUS_HEIGHT)
	var intent_size := BattleLayout.COMBATANT_INTENT_SIZE
	var shadow_size := Vector2(_portrait_size.x * BattleLayout.COMBATANT_SHADOW_WIDTH_RATIO, BattleLayout.COMBATANT_SHADOW_HEIGHT)

	_shadow.position = Vector2((view_size.x - shadow_size.x) * 0.5, BattleLayout.FOOT_LINE - shadow_size.y * 0.5)
	_shadow.size = shadow_size
	_shadow.queue_redraw()

	_target_ring.size = Vector2(_portrait_size.x * BattleLayout.COMBATANT_TARGET_RING_WIDTH_RATIO, _portrait_size.x * BattleLayout.COMBATANT_TARGET_RING_HEIGHT_RATIO)
	_target_ring.position = Vector2((view_size.x - _target_ring.size.x) * 0.5, BattleLayout.FOOT_LINE - _target_ring.size.y * BattleLayout.COMBATANT_TARGET_RING_FOOT_OVERLAP)

	_portrait_placeholder.position = portrait_pos
	_portrait_placeholder.size = _portrait_size
	_portrait.position = portrait_pos
	_portrait.size = _portrait_size

	_name_label.position = Vector2(0, portrait_pos.y - BattleLayout.COMBATANT_NAME_ABOVE_PORTRAIT)
	_name_label.size = Vector2(view_size.x, BattleLayout.COMBATANT_NAME_HEIGHT)

	_health_bar.position = Vector2((view_size.x - hp_size.x) * 0.5, BattleLayout.FOOT_LINE + BattleLayout.COMBATANT_HP_GAP)
	_health_bar.size = hp_size

	_status_row.position = Vector2((view_size.x - status_size.x) * 0.5, _health_bar.position.y + _health_bar.size.y + BattleLayout.COMBATANT_STATUS_GAP)
	_status_row.size = status_size

	_intent_view.position = Vector2((view_size.x - intent_size.x) * 0.5, maxf(0.0, portrait_pos.y - intent_size.y * BattleLayout.COMBATANT_INTENT_HEAD_OVERLAP))
	_intent_view.size = intent_size


func _refresh_portrait(portrait_path: String, flip_h: bool) -> void:
	var texture = load(portrait_path) if not portrait_path.is_empty() and ResourceLoader.exists(portrait_path) else null
	_portrait.texture = texture
	_portrait.visible = texture != null
	_portrait_placeholder.visible = texture == null
	_portrait.flip_h = flip_h

	var portrait_height := BattleLayout.PORTRAIT_BASE_ALLY if _is_player else BattleLayout.PORTRAIT_BASE_ENEMY
	var calculated_width := portrait_height
	if texture != null and texture.get_size().y > 0:
		calculated_width = portrait_height * texture.get_size().x / texture.get_size().y
	_portrait_base_size = Vector2(calculated_width, portrait_height)
	_update_scaled_metrics()


func _refresh_intent(data: Dictionary) -> void:
	_intent_view.visible = not _is_player
	if _is_player:
		return
	var intent := {}
	if data.has("intent") and data["intent"] is Dictionary:
		intent = data["intent"]
	var current: Dictionary = intent
	if intent.has("current") and intent["current"] is Dictionary:
		current = intent["current"]
	var next: Dictionary = {"icon_key": "special", "value": "?"}
	if intent.has("next") and intent["next"] is Dictionary:
		next = intent["next"]
	_intent_view.set_intents(current, next)


func _apply_alive_state() -> void:
	var dead_tint := theme.get_color("rarity_common", "ThemeTokens").darkened(BattleLayout.COMBATANT_DEAD_TINT_DARKEN)
	var live_tint := theme.get_color("unit_tint", "ThemeTokens")
	_portrait.modulate = live_tint if _alive else dead_tint
	_portrait_placeholder.modulate = live_tint if _alive else dead_tint
	_shadow.modulate = live_tint if _alive else dead_tint.darkened(BattleLayout.COMBATANT_DEAD_SHADOW_DARKEN)


func _update_scaled_metrics() -> void:
	_portrait_size = _portrait_base_size * _view_scale
	var next_width := maxf(BattleLayout.COMBATANT_VIEW_MIN_WIDTH, _portrait_size.x)
	custom_minimum_size = Vector2(next_width, BattleLayout.FOOT_LINE + BattleLayout.COMBATANT_POST_FOOT_UI_HEIGHT)
	size = custom_minimum_size
