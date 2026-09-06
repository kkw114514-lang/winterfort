extends Panel

signal card_selected(card)

var card = null

var _disabled := false
var _selected := false

@onready var _cost_label: Label = $Cost/Label
@onready var _name_label: Label = $Name
@onready var _owner_label: Label = $Owner
@onready var _selected_frame: Panel = $SelectedFrame


func setup(card_data, disabled: bool) -> void:
	card = card_data
	_cost_label.text = str(card.cost)
	_name_label.text = card.name
	_owner_label.text = card.owner
	set_disabled(disabled)
	set_selected(false)


func set_disabled(value: bool) -> void:
	_disabled = value
	mouse_default_cursor_shape = Control.CURSOR_FORBIDDEN if _disabled else Control.CURSOR_POINTING_HAND
	modulate = Color(0.48, 0.48, 0.48, 0.72) if _disabled else Color.WHITE


func set_selected(value: bool) -> void:
	_selected = value
	_selected_frame.visible = _selected
	if not _disabled:
		modulate = Color(1.08, 1.08, 1.0, 1.0) if _selected else Color.WHITE


func _gui_input(event: InputEvent) -> void:
	if _disabled:
		return
	if event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_LEFT and event.pressed:
		card_selected.emit(card)
		accept_event()
