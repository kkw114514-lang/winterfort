extends Control

const THEME_PATH := "res://ui/theme.tres"
const PYRA_PATH := "res://assets/silence.png"
const ICEY_PATH := "res://assets/icey.png"
const ENEMY_PATH := "res://assets/enemy-1.png"

const DevBattleFactoryScript := preload("res://scripts/dev/DevBattleFactory.gd")
const CombatantViewScene := preload("res://ui/components/combatant_view.tscn")
const CardViewScene := preload("res://ui/components/card_view.tscn")
const BottomSkinScene := preload("res://ui/skin/Gemini_Generated_Image_r8e3swr8e3swr8e3_layer_3.tscn")
const TopSkinScene := preload("res://ui/skin/ui_batchA.tscn")
const BackgroundTexture := preload("res://ui/skin/background_4.png")

const SKIN_DESIGN_SIZE := Vector2(2736.0, 1536.0)
const SKIN_SCALE := Vector2(1920.0 / 2736.0, 1080.0 / 1536.0)
const PORTRAITS := {
	"default": preload("res://ui/skin/portrait_default.png"),
}

const ALLY_DISPLAY_NAMES := ["炽芯", "凛汐"]
const ALLY_PORTRAITS := [PYRA_PATH, ICEY_PATH]

var battle: Battle = null

var _world: Control
var _ui: Control
var _bottom_skin: Control
var _top_skin: Control
var _energy_count_label: Label
var _draw_pile_count_label: Label
var _discard_pile_count_label: Label
var _exhaust_pile_count_label: Label
var _combo_text_label: Label
var _turn_text_label: Label
var _portrait_image: TextureRect
var _end_turn_button: Button
var _result_label: Label
var _ally_views: Array[Control] = []
var _enemy_views: Array[Control] = []
var _hand_cards: Array[Control] = []
var _card_by_view := {}
var _layout_records: Array[Dictionary] = []
var _selected_card = null
var _selected_card_view: Control = null
var _turn_number := 0


func _ready() -> void:
	custom_minimum_size = BattleLayout.RESOLUTION
	theme = load(THEME_PATH)

	battle = DevBattleFactoryScript.create_dev_battle()
	battle.start_player_turn()
	_turn_number = 1
	_build_screen()
	_connect_battle_signals()


func _build_screen() -> void:
	_add_background()

	_world = Control.new()
	_world.name = "World"
	_world.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_world.size = BattleLayout.RESOLUTION
	add_child(_world)

	_ui = Control.new()
	_ui.name = "BattleUI"
	_ui.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_ui.size = BattleLayout.RESOLUTION
	add_child(_ui)

	_add_formations()
	_add_hud()
	_add_hand()
	_add_result_label()


func _add_background() -> void:
	var background := TextureRect.new()
	background.name = "Background"
	background.texture = BackgroundTexture
	background.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	background.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_COVERED
	background.size = BattleLayout.RESOLUTION
	background.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(background)


func _add_formations() -> void:
	var ally_slots := Formation.layout(
		battle.allies.size(),
		BattleLayout.ALLY_NEAR,
		BattleLayout.ALLY_NEAR_SCALE,
		BattleLayout.ALLY_FAR,
		BattleLayout.ALLY_FAR_SCALE
	)
	for i in range(battle.allies.size()):
		var unit = CombatantViewScene.instantiate()
		unit.name = "Ally%d" % (i + 1)
		unit.set_show_name(true)
		unit.set_combatant(_ally_data(battle.allies[i], i))
		_place_combatant(unit, ally_slots[i])
		_world.add_child(unit)
		_ally_views.append(unit)
		_layout_records.append({"unit": unit, "anchor": ally_slots[i]["position"], "kind": "ally", "index": i})

	var enemy_slots := Formation.layout(
		battle.enemies.size(),
		BattleLayout.ENEMY_NEAR,
		BattleLayout.ENEMY_NEAR_SCALE,
		BattleLayout.ENEMY_FAR,
		BattleLayout.ENEMY_FAR_SCALE
	)
	for i in range(battle.enemies.size()):
		var unit = CombatantViewScene.instantiate()
		unit.name = "Enemy%d" % (i + 1)
		unit.set_combatant(_enemy_data(battle.enemies[i], i))
		unit.view_clicked.connect(_on_enemy_clicked)
		_place_combatant(unit, enemy_slots[i])
		_world.add_child(unit)
		_enemy_views.append(unit)
		_layout_records.append({"unit": unit, "anchor": enemy_slots[i]["position"], "kind": "enemy", "index": i})


func _place_combatant(unit: Control, slot: Dictionary) -> void:
	var slot_scale := float(slot["scale"])
	var slot_position: Vector2 = slot["position"]
	unit.set_view_scale(slot_scale)
	unit.scale = Vector2.ONE
	unit.position = slot_position - Vector2(unit.view_width() * 0.5, unit.foot_line())


func _add_hud() -> void:
	_bottom_skin = BottomSkinScene.instantiate()
	_bottom_skin.name = "BottomSkin"
	_prepare_skin_root(_bottom_skin)
	_hide_bottom_skin_background()
	_set_mouse_filter_recursive(_bottom_skin, Control.MOUSE_FILTER_IGNORE)
	_ui.add_child(_bottom_skin)

	_top_skin = TopSkinScene.instantiate()
	_top_skin.name = "TopSkin"
	_prepare_skin_root(_top_skin)
	_set_mouse_filter_recursive(_top_skin, Control.MOUSE_FILTER_IGNORE)
	_ui.add_child(_top_skin)

	_cache_skin_nodes()
	_add_end_turn_hit_area()
	set_contract_master("default")
	_refresh_hud()


func _prepare_skin_root(skin: Control) -> void:
	skin.set_anchors_preset(Control.PRESET_TOP_LEFT)
	skin.position = Vector2.ZERO
	skin.size = SKIN_DESIGN_SIZE
	skin.scale = SKIN_SCALE
	skin.mouse_filter = Control.MOUSE_FILTER_IGNORE


func _hide_bottom_skin_background() -> void:
	var background := _bottom_skin.get_node_or_null("background") as CanvasItem
	if background != null:
		background.visible = false


func _set_mouse_filter_recursive(node: Node, mouse_filter: int) -> void:
	if node is Control:
		(node as Control).mouse_filter = mouse_filter
	for child in node.get_children():
		_set_mouse_filter_recursive(child, mouse_filter)


func _cache_skin_nodes() -> void:
	_energy_count_label = _bottom_skin.get_node_or_null("energy_count") as Label
	_draw_pile_count_label = _bottom_skin.get_node_or_null("draw_pile_count") as Label
	_discard_pile_count_label = _bottom_skin.get_node_or_null("discard_pile_count") as Label
	_exhaust_pile_count_label = _bottom_skin.get_node_or_null("exhaust_pile_count") as Label
	_combo_text_label = _top_skin.get_node_or_null("combo_text") as Label
	_turn_text_label = _top_skin.get_node_or_null("turn_text") as Label
	_portrait_image = _top_skin.get_node_or_null("portrait_slot/portrait_image") as TextureRect

	var required_nodes := {
		"energy_count": _energy_count_label,
		"draw_pile_count": _draw_pile_count_label,
		"discard_pile_count": _discard_pile_count_label,
		"exhaust_pile_count": _exhaust_pile_count_label,
		"combo_text": _combo_text_label,
		"turn_text": _turn_text_label,
		"portrait_image": _portrait_image,
	}
	for node_name in required_nodes:
		if required_nodes[node_name] == null:
			push_error("battle_v2 skin node missing: %s" % node_name)


func _add_end_turn_hit_area() -> void:
	var end_turn_art := _bottom_skin.get_node_or_null("end_turn_button") as Control
	if end_turn_art == null:
		push_error("battle_v2 skin node missing: end_turn_button")
		return

	_end_turn_button = Button.new()
	_end_turn_button.name = "EndTurnButtonHitArea"
	_end_turn_button.position = end_turn_art.position
	_end_turn_button.size = end_turn_art.size
	_end_turn_button.text = ""
	_end_turn_button.focus_mode = Control.FOCUS_NONE
	_end_turn_button.mouse_default_cursor_shape = Control.CURSOR_POINTING_HAND
	_end_turn_button.flat = true
	_make_button_transparent(_end_turn_button)
	_end_turn_button.pressed.connect(_on_end_turn_pressed)
	_bottom_skin.add_child(_end_turn_button)


func _make_button_transparent(button: Button) -> void:
	for state in ["normal", "hover", "pressed", "disabled", "focus"]:
		button.add_theme_stylebox_override(state, StyleBoxEmpty.new())


func set_contract_master(id: String) -> void:
	if _portrait_image == null:
		return
	_portrait_image.texture = PORTRAITS.get(id, PORTRAITS["default"])


func _refresh_hud() -> void:
	_refresh_energy()
	_refresh_combo()
	_refresh_piles()
	_refresh_turn()


func _add_result_label() -> void:
	_result_label = Label.new()
	_result_label.name = "ResultLabel"
	_result_label.theme_type_variation = "PreviewTitle"
	_result_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_result_label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	_result_label.size = Vector2(BattleLayout.CARD_SIZE.x * 3.0, BattleLayout.TOPHUD.size.y * 2.2)
	_result_label.position = (BattleLayout.RESOLUTION - _result_label.size) * 0.5
	_result_label.visible = false
	_result_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_ui.add_child(_result_label)


func _add_hand() -> void:
	var cards: Array[Control] = []
	for i in range(battle.deck.hand.size()):
		var card = battle.deck.hand[i]
		var card_view = CardViewScene.instantiate()
		card_view.name = "Card%d" % (i + 1)
		card_view.set_card(_card_data(card))
		card_view.set_affordable(_is_card_affordable(card))
		card_view.set_selected(false)
		card_view.card_clicked.connect(_on_card_clicked)
		_ui.add_child(card_view)
		cards.append(card_view)
		_hand_cards.append(card_view)
		_card_by_view[card_view] = card
	_layout_hand(cards)


func _layout_hand(cards: Array[Control]) -> void:
	var count := cards.size()
	if count == 0:
		return
	var span := BattleLayout.FAN_SPAN_SMALL if count <= 5 else BattleLayout.FAN_SPAN_BIG
	var step := BattleLayout.FAN_STEP_SMALL if count <= 5 else BattleLayout.FAN_STEP_BIG
	var total_width := step * float(maxi(count - 1, 0)) + BattleLayout.CARD_SIZE.x
	var center_x := BattleLayout.RESOLUTION.x * 0.5
	var left_x := clampf(center_x - total_width * 0.5, BattleLayout.FAN_SAFE_X.x, BattleLayout.FAN_SAFE_X.y - total_width)
	var base_y := BattleLayout.HAND_BASE_Y
	var angle_start := -float(span) * 0.5
	var angle_step := float(span) / float(maxi(count - 1, 1))

	for i in range(count):
		var card: Control = cards[i]
		var angle := angle_start + angle_step * float(i)
		card.pivot_offset = BattleLayout.CARD_SIZE * 0.5
		card.rotation_degrees = angle
		card.position = Vector2(left_x + step * i, base_y + absf(angle) * 0.9)
		card.size = BattleLayout.CARD_SIZE


func _ally_data(combatant, index: int) -> Dictionary:
	return {
		"name": _ally_display_name(combatant, index),
		"hp": combatant.hp,
		"max_hp": combatant.max_hp,
		"shield": combatant.shield,
		"statuses": _statuses_for(combatant),
		"is_player": true,
		"alive": combatant.alive,
		"portrait_path": _ally_portrait_path(index),
	}


func _enemy_data(combatant, _index: int) -> Dictionary:
	return {
		"name": _enemy_display_name(combatant),
		"hp": combatant.hp,
		"max_hp": combatant.max_hp,
		"shield": combatant.shield,
		"statuses": _statuses_for(combatant),
		"is_player": false,
		"alive": combatant.alive,
		"intent": _map_intent(combatant.intent),
		"portrait_path": ENEMY_PATH,
	}


func _card_data(card) -> Dictionary:
	return {
		"name": card.name,
		"cost": card.cost,
	}


func _map_intent(intent: Dictionary) -> Dictionary:
	var current := {"icon_key": "special", "value": "?"}
	if intent.get("type", "") == "attack":
		current = {"icon_key": "attack", "value": str(int(intent.get("power", 0)))}
	return {
		"current": current,
		"next": {"icon_key": "special", "value": "?"},
	}


func _statuses_for(combatant) -> Array:
	if combatant.statuses is Array:
		return combatant.statuses.duplicate(true)
	return []


func _ally_display_name(combatant, index: int) -> String:
	if index < ALLY_DISPLAY_NAMES.size():
		return str(ALLY_DISPLAY_NAMES[index])
	return str(combatant.name)


func _ally_portrait_path(index: int) -> String:
	if index < ALLY_PORTRAITS.size():
		return str(ALLY_PORTRAITS[index])
	return ""


func _enemy_display_name(combatant) -> String:
	if str(combatant.name) == "Enemy":
		return "熔岩熊"
	return str(combatant.name)


func _connect_battle_signals() -> void:
	battle.turn_started.connect(func(turn_number: int, _energy: int, _energy_max: int, _draw_count: int) -> void:
		_turn_number = turn_number
		_refresh_energy()
		_refresh_combo()
		_refresh_piles()
		_refresh_turn()
	)
	battle.energy_changed.connect(func(_energy: int, _energy_max: int) -> void:
		_refresh_energy()
		_refresh_hand_affordability()
	)
	battle.hand_changed.connect(func(_hand: Array) -> void:
		_refresh_piles()
		_refresh_hand()
	)
	battle.card_played.connect(func(_card, _combo: int) -> void:
		_refresh_combo()
		_refresh_piles()
	)
	battle.damage_dealt.connect(func(_source, target, _amount: int) -> void:
		_refresh_combatant(target)
	)
	battle.intent_locked.connect(func(enemy, _intent: Dictionary) -> void:
		_refresh_combatant(enemy)
	)
	battle.combatant_died.connect(func(combatant) -> void:
		_refresh_combatant(combatant)
		_refresh_target_highlight()
	)
	battle.battle_won.connect(func() -> void:
		_show_result("胜利")
	)
	battle.battle_lost.connect(func() -> void:
		_show_result("失败")
	)


func _on_card_clicked(card_view: Control) -> void:
	if battle.battle_ended or not _card_by_view.has(card_view):
		return

	var card = _card_by_view[card_view]
	if not battle.deck.hand.has(card) or not _is_card_affordable(card):
		return

	if _selected_card_view == card_view:
		_clear_selection()
		return

	_clear_selection(false)
	_selected_card = card
	_selected_card_view = card_view
	_selected_card_view.set_selected(true)
	_refresh_target_highlight()


func _on_enemy_clicked(_view) -> void:
	if battle.battle_ended or _selected_card == null:
		return

	var card_to_play = _selected_card
	_clear_selection()
	var played := battle.play_card(card_to_play)
	if not played:
		_refresh_all_units()
		_refresh_energy()
		_refresh_combo()
		_refresh_piles()
		_refresh_hand()


func _on_end_turn_pressed() -> void:
	if battle.battle_ended:
		return
	_clear_selection()
	battle.end_player_turn()


func _unhandled_input(event: InputEvent) -> void:
	if _selected_card == null:
		return
	if event is InputEventMouseButton and event.pressed:
		if event.button_index == MOUSE_BUTTON_LEFT or event.button_index == MOUSE_BUTTON_RIGHT:
			_clear_selection()


func _refresh_energy() -> void:
	if _energy_count_label != null:
		_energy_count_label.text = "%d/%d" % [battle.energy, battle.energy_max]


func _refresh_combo() -> void:
	if _combo_text_label != null:
		_combo_text_label.text = "连击 ×%d" % battle.combo


func _refresh_piles() -> void:
	if _draw_pile_count_label != null:
		_draw_pile_count_label.text = str(battle.deck.draw_pile.size())
	if _exhaust_pile_count_label != null:
		_exhaust_pile_count_label.text = str(battle.deck.exhaust_pile.size())
	if _discard_pile_count_label != null:
		_discard_pile_count_label.text = str(battle.deck.discard_pile.size())


func _refresh_turn() -> void:
	if _turn_text_label != null:
		_turn_text_label.text = "回合 %d" % _turn_number


func _refresh_hand() -> void:
	var selected_card = _selected_card if battle.deck.hand.has(_selected_card) else null
	_clear_selection(false)

	for card_view in _hand_cards:
		if is_instance_valid(card_view):
			_ui.remove_child(card_view)
			card_view.queue_free()
	_hand_cards.clear()
	_card_by_view.clear()

	_add_hand()

	if selected_card != null:
		for card_view in _hand_cards:
			if _card_by_view.get(card_view) == selected_card:
				_selected_card = selected_card
				_selected_card_view = card_view
				_selected_card_view.set_selected(true)
				break
	_refresh_target_highlight()


func _refresh_hand_affordability() -> void:
	for card_view in _hand_cards:
		var card = _card_by_view.get(card_view)
		if card == null:
			continue
		card_view.set_affordable(_is_card_affordable(card))
	if _selected_card != null and not _is_card_affordable(_selected_card):
		_clear_selection()


func _refresh_all_units() -> void:
	for ally in battle.allies:
		_refresh_combatant(ally)
	for enemy in battle.enemies:
		_refresh_combatant(enemy)


func _refresh_combatant(combatant) -> void:
	var ally_index := battle.allies.find(combatant)
	if ally_index >= 0 and ally_index < _ally_views.size():
		_ally_views[ally_index].set_combatant(_ally_data(combatant, ally_index))
		return

	var enemy_index := battle.enemies.find(combatant)
	if enemy_index >= 0 and enemy_index < _enemy_views.size():
		_enemy_views[enemy_index].set_combatant(_enemy_data(combatant, enemy_index))


func _refresh_target_highlight() -> void:
	var target_index := _first_alive_enemy_index() if _selected_card != null and not battle.battle_ended else -1
	for i in range(_enemy_views.size()):
		_enemy_views[i].set_targeted(i == target_index)


func _first_alive_enemy_index() -> int:
	for i in range(battle.enemies.size()):
		if battle.enemies[i] != null and battle.enemies[i].alive:
			return i
	return -1


func _clear_selection(update_target: bool = true) -> void:
	if _selected_card_view != null and is_instance_valid(_selected_card_view):
		_selected_card_view.set_selected(false)
	_selected_card = null
	_selected_card_view = null
	if update_target:
		_refresh_target_highlight()


func _show_result(text: String) -> void:
	_clear_selection()
	_result_label.text = text
	_result_label.visible = true
	if _end_turn_button != null:
		_end_turn_button.disabled = true
	_refresh_hand_affordability()


func _is_card_affordable(card) -> bool:
	return not battle.battle_ended and card != null and card.cost <= battle.energy


func energy_text() -> String:
	if _energy_count_label != null:
		return _energy_count_label.text
	return ""


func hand_card_count() -> int:
	return _hand_cards.size()


func ally_view_count() -> int:
	return _ally_views.size()


func enemy_view(index: int) -> Control:
	if index < 0 or index >= _enemy_views.size():
		return null
	return _enemy_views[index]


func hand_card_views() -> Array[Control]:
	return _hand_cards.duplicate()


func card_for_view(card_view: Control):
	return _card_by_view.get(card_view)


func end_turn_button_view() -> Button:
	return _end_turn_button


func result_text() -> String:
	return _result_label.text


func end_turn_enabled() -> bool:
	return _end_turn_button != null and not _end_turn_button.disabled


func layout_records() -> Array[Dictionary]:
	return _layout_records.duplicate()
