extends Control
class_name HealthBarView

const THEME_PATH := "res://ui/theme.tres"

var _bar: ProgressBar
var _shield_overlay: ColorRect
var _label: Label
var _last_hp := 0
var _last_max_hp := 1
var _last_shield := 0


func _ready() -> void:
	_ensure_nodes()


func _notification(what: int) -> void:
	if what == NOTIFICATION_RESIZED:
		_sync_child_rects()


func set_values(hp: int, max_hp: int, is_ally: bool, shield: int = 0) -> void:
	_ensure_nodes()
	var safe_max := maxi(max_hp, 1)
	_last_hp = clampi(hp, 0, safe_max)
	_last_max_hp = safe_max
	_last_shield = maxi(shield, 0)
	_bar.theme_type_variation = "HPBarAlly" if is_ally else "HPBarEnemy"
	_bar.max_value = safe_max
	_bar.value = _last_hp
	_label.text = "%d/%d" % [_last_hp, safe_max]
	_sync_child_rects()


func _ensure_nodes() -> void:
	if _bar != null:
		return
	if theme == null:
		theme = load(THEME_PATH)
	custom_minimum_size = BattleLayout.HEALTH_BAR_MIN_SIZE

	_bar = ProgressBar.new()
	_bar.show_percentage = false
	_bar.min_value = 0
	_bar.theme_type_variation = "HPBarAlly"
	add_child(_bar)

	_shield_overlay = ColorRect.new()
	_shield_overlay.color = Palette.get_color("color/status/shield")
	_shield_overlay.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(_shield_overlay)

	_label = Label.new()
	_label.theme_type_variation = "PreviewSmall"
	_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(_label)

	_sync_child_rects()


func _sync_child_rects() -> void:
	if _bar == null:
		return
	var current_size := size
	if current_size.x <= 0 or current_size.y <= 0:
		current_size = custom_minimum_size
	_bar.size = current_size
	_shield_overlay.size = Vector2(
		current_size.x * clampf(float(_last_shield) / float(_last_max_hp), 0.0, 1.0),
		current_size.y
	)
	_shield_overlay.visible = _last_shield > 0
	_label.size = current_size
