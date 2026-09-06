extends Node2D

const DevBattleFactoryScript := preload("res://scripts/dev/DevBattleFactory.gd")
const CardViewScene := preload("res://card_view.tscn")

const DEV_SEED := DevBattleFactoryScript.DEV_SEED
const CARD_SIZE := Vector2(112, 160)
const HAND_BASE_Y := 520.0
const HAND_SPACING := 122.0
const HAND_FAN_DEGREES := 5.0

var battle = null
var _selected_card = null
var _selected_card_view = null

@onready var _spirit_1_hp: ProgressBar = $BattleUI/Root/UnitOverlay/Spirit1HP
@onready var _spirit_2_hp: ProgressBar = $BattleUI/Root/UnitOverlay/Spirit2HP
@onready var _enemy_1_hp: ProgressBar = $BattleUI/Root/UnitOverlay/Enemy1HP
@onready var _enemy_1_intent_label: Label = $BattleUI/Root/UnitOverlay/Enemy1Intent/Current/Label
@onready var _enemy_1_next_label: Label = $BattleUI/Root/UnitOverlay/Enemy1Intent/Next/Label
@onready var _deck_pile_label: Label = $BattleUI/Root/BottomLeft/DeckPile/Label
@onready var _discard_pile_label: Label = $BattleUI/Root/BottomRight/DiscardPile/Label
@onready var _energy_label: Label = $BattleUI/Root/BottomLeft/EnergyOrb/Label
@onready var _end_turn_button: Button = $BattleUI/Root/BottomRight/EndTurnButton
@onready var _enemy_1_target_button: Button = $BattleUI/Root/UnitOverlay/Enemy1TargetButton
@onready var _hand_area: Control = $BattleUI/Root/HandArea
@onready var _result_label: Label = $BattleUI/Root/ResultLabel
@onready var _top_hud_label: Label = $BattleUI/Root/TopHUD/Label
@onready var _enemy_2: Node2D = $World/EnemyGroup/Enemy2
@onready var _enemy_2_intent: Control = $BattleUI/Root/UnitOverlay/Enemy2Intent
@onready var _enemy_2_hp: ProgressBar = $BattleUI/Root/UnitOverlay/Enemy2HP


func _ready() -> void:
	_enemy_2.visible = false
	_enemy_2_intent.visible = false
	_enemy_2_hp.visible = false
	_result_label.visible = false
	_enemy_1_next_label.text = "-"
	_top_hud_label.text = "开发战斗  seed %d" % DEV_SEED

	battle = _create_dev_battle()
	_connect_battle_signals()
	_end_turn_button.pressed.connect(_on_end_turn_pressed)
	_enemy_1_target_button.pressed.connect(_on_enemy_1_pressed)

	_refresh_all_units()
	_refresh_intent(battle.enemies[0])
	_refresh_piles()
	print("[UI] battle scene ready seed=%d" % DEV_SEED)
	battle.start_player_turn()


func _create_dev_battle():
	return DevBattleFactoryScript.create_dev_battle()


func _connect_battle_signals() -> void:
	battle.turn_started.connect(func(turn_number: int, energy: int, energy_max: int, draw_count: int) -> void:
		_refresh_energy(energy, energy_max)
		_refresh_piles()
		print("[UI] turn=%d energy=%d/%d draw=%d hand=%d" % [
			turn_number,
			energy,
			energy_max,
			draw_count,
			battle.deck.hand.size(),
		])
	)
	battle.energy_changed.connect(func(energy: int, energy_max: int) -> void:
		_refresh_energy(energy, energy_max)
	)
	battle.hand_changed.connect(func(_hand: Array) -> void:
		_refresh_piles()
		_refresh_hand()
	)
	battle.damage_dealt.connect(func(_source, target, amount: int) -> void:
		_refresh_unit(target)
		print("[UI] damage amount=%d target=%s hp=%d/%d" % [amount, target.name, target.hp, target.max_hp])
	)
	battle.intent_locked.connect(func(enemy, intent: Dictionary) -> void:
		_refresh_intent(enemy)
		print("[UI] intent %s %d" % [intent.get("type", ""), int(intent.get("power", 0))])
	)
	battle.combatant_died.connect(func(combatant) -> void:
		_refresh_unit(combatant)
	)
	battle.battle_won.connect(func() -> void:
		_show_result("胜利")
		print("[UI] battle_won")
	)
	battle.battle_lost.connect(func() -> void:
		_show_result("失败")
		print("[UI] battle_lost")
	)


func _on_end_turn_pressed() -> void:
	if battle == null or battle.battle_ended:
		return
	_clear_selection()
	print("[UI] end turn pressed")
	battle.end_player_turn()


func _on_card_selected(card, card_view) -> void:
	if battle == null or battle.battle_ended:
		return
	if not battle.deck.hand.has(card):
		return
	if battle.energy < card.cost:
		return

	_clear_selection()
	_selected_card = card
	_selected_card_view = card_view
	_selected_card_view.set_selected(true)
	print("[UI] card selected %s cost=%d owner=%s" % [card.name, card.cost, card.owner])


func _on_enemy_1_pressed() -> void:
	if _selected_card == null or battle == null or battle.battle_ended:
		return

	var card_to_play = _selected_card
	_clear_selection()
	var played: bool = battle.play_card(card_to_play)
	print("[UI] play selected card %s result=%s enemy_hp=%d energy=%d" % [
		card_to_play.name,
		str(played),
		battle.enemies[0].hp,
		battle.energy,
	])
	if not played:
		_refresh_hand()


func _unhandled_input(event: InputEvent) -> void:
	if _selected_card == null:
		return
	if event is InputEventMouseButton and event.pressed:
		if event.button_index == MOUSE_BUTTON_RIGHT or event.button_index == MOUSE_BUTTON_LEFT:
			_clear_selection()


func _refresh_all_units() -> void:
	_refresh_unit(battle.allies[0])
	_refresh_unit(battle.allies[1])
	_refresh_unit(battle.enemies[0])


func _refresh_unit(combatant) -> void:
	var hp_bar = _get_hp_bar(combatant)
	if hp_bar == null:
		return

	hp_bar.max_value = combatant.max_hp
	hp_bar.value = combatant.hp
	hp_bar.get_node("HPText").text = "%d/%d" % [combatant.hp, combatant.max_hp]
	hp_bar.modulate = Color(0.42, 0.42, 0.42, 0.86) if not combatant.alive else Color.WHITE


func _get_hp_bar(combatant):
	if combatant == battle.allies[0]:
		return _spirit_1_hp
	if combatant == battle.allies[1]:
		return _spirit_2_hp
	if combatant == battle.enemies[0]:
		return _enemy_1_hp
	return null


func _refresh_energy(energy: int, energy_max: int) -> void:
	_energy_label.text = "%d/%d" % [energy, energy_max]


func _refresh_intent(enemy) -> void:
	if enemy != battle.enemies[0]:
		return
	var intent: Dictionary = enemy.intent
	if intent.get("type", "") == "attack":
		_enemy_1_intent_label.text = str(int(intent.get("power", 0)))
	else:
		_enemy_1_intent_label.text = "-"


func _refresh_piles() -> void:
	_deck_pile_label.text = "卡组%d" % battle.deck.draw_pile.size()
	_discard_pile_label.text = "弃牌%d" % battle.deck.discard_pile.size()


func _refresh_hand() -> void:
	if _selected_card != null and not battle.deck.hand.has(_selected_card):
		_clear_selection()

	for child in _hand_area.get_children():
		_hand_area.remove_child(child)
		child.queue_free()

	var hand: Array = battle.deck.hand
	var count := hand.size()
	if count == 0:
		return

	var total_width := CARD_SIZE.x + HAND_SPACING * float(count - 1)
	var start_x := (get_viewport_rect().size.x - total_width) * 0.5
	var center := float(count - 1) * 0.5
	for i in range(count):
		var card = hand[i]
		var card_view = CardViewScene.instantiate()
		_hand_area.add_child(card_view)
		card_view.position = Vector2(start_x + HAND_SPACING * i, HAND_BASE_Y + abs(float(i) - center) * 8.0)
		card_view.rotation_degrees = (float(i) - center) * HAND_FAN_DEGREES
		card_view.setup(card, battle.battle_ended or battle.energy < card.cost)
		card_view.card_selected.connect(func(selected_card) -> void:
			_on_card_selected(selected_card, card_view)
		)


func _clear_selection() -> void:
	if _selected_card_view != null and is_instance_valid(_selected_card_view):
		_selected_card_view.set_selected(false)
	_selected_card = null
	_selected_card_view = null


func _show_result(text: String) -> void:
	_clear_selection()
	_result_label.text = text
	_result_label.visible = true
	_end_turn_button.disabled = true
	_refresh_hand()
