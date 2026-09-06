extends Node

const BattleScene := preload("res://battle.tscn")

var _battle_node: Node = null


func _ready() -> void:
	call_deferred("_run")


func _run() -> void:
	print("[TEST] battle scene UI smoke start")
	_battle_node = BattleScene.instantiate()
	add_child(_battle_node)
	await get_tree().process_frame

	_assert_equal(_energy_text(), "3/3", "opening energy label")
	_assert_equal(_intent_text(), "10", "opening intent label")
	_assert_equal(_hp_text("Spirit1HP"), "100/100", "front spirit opening HP")
	_assert_equal(_hp_text("Spirit2HP"), "100/100", "rear spirit opening HP")
	_assert_equal(_hp_text("Enemy1HP"), "120/120", "enemy opening HP")
	_assert_equal(_hand_count(), 5, "opening dynamic hand count")
	print("[OK] opening labels energy=%s intent=%s deck=%s discard=%s" % [
		_energy_text(),
		_intent_text(),
		_deck_text(),
		_discard_text(),
	])

	var enemy_hp_before: int = _battle_node.battle.enemies[0].hp
	var hand_before: int = _hand_count()
	var energy_before: int = _battle_node.battle.energy
	var selected_card = _first_enabled_card_view()
	_assert_true(selected_card != null, "opening hand has a playable card")
	_click_card_view(selected_card)
	_press_enemy()
	await get_tree().process_frame
	_assert_true(_battle_node.battle.enemies[0].hp < enemy_hp_before, "click card then enemy deals damage")
	_assert_equal(_hand_count(), hand_before - 1, "played card leaves hand")
	_assert_true(_battle_node.battle.energy <= energy_before, "playing a card does not increase energy")
	print("[OK] click card -> enemy enemy_hp=%s energy=%s hand=%d" % [
		_hp_text("Enemy1HP"),
		_energy_text(),
		_hand_count(),
	])

	_make_insufficient_card_visible()
	var disabled_card = _first_disabled_card_view()
	_assert_true(disabled_card != null, "a card is greyed out when energy is insufficient")
	enemy_hp_before = _battle_node.battle.enemies[0].hp
	_click_card_view(disabled_card)
	_press_enemy()
	await get_tree().process_frame
	_assert_equal(_battle_node.battle.enemies[0].hp, enemy_hp_before, "greyed insufficient card cannot be played")
	print("[OK] insufficient card is grey and cannot play enemy_hp=%s energy=%s" % [
		_hp_text("Enemy1HP"),
		_energy_text(),
	])

	_press_end_turn()
	await get_tree().process_frame
	_assert_equal(_energy_text(), "3/3", "energy resets after end turn")
	_assert_true(_battle_node.battle.allies[0].hp < 100, "front spirit takes locked enemy intent damage")
	_assert_equal(_battle_node.battle.allies[1].hp, 100, "rear spirit is not hit while front is latest entered")
	_assert_true(_intent_text().is_valid_int(), "intent stays numeric after enemy turn")
	print("[OK] after one end turn energy=%s intent=%s front_hp=%s rear_hp=%s deck=%s discard=%s" % [
		_energy_text(),
		_intent_text(),
		_hp_text("Spirit1HP"),
		_hp_text("Spirit2HP"),
		_deck_text(),
		_discard_text(),
	])

	_press_end_turn()
	await get_tree().process_frame
	_assert_true(_battle_node.battle.allies[0].hp < 90, "second end turn advances enemy attack again")
	_assert_equal(_battle_node.battle.allies[1].hp, 100, "rear spirit still stays untouched while front lives")
	print("[OK] after two end turns front_hp=%s rear_hp=%s intent=%s" % [
		_hp_text("Spirit1HP"),
		_hp_text("Spirit2HP"),
		_intent_text(),
	])

	var guard := 0
	while not _battle_node.battle.battle_ended and guard < 20:
		guard += 1
		if _play_first_enabled_card():
			await get_tree().process_frame
		else:
			_press_end_turn()
			await get_tree().process_frame
	_assert_true(_battle_node.battle.battle_ended, "UI can play through to battle end")
	_assert_equal(_battle_node.get_node("BattleUI/Root/ResultLabel").text, "胜利", "battle result label")
	_assert_true(_battle_node.get_node("BattleUI/Root/BottomRight/EndTurnButton").disabled, "end turn disabled after result")
	print("[OK] battle ended through UI result=%s enemy_hp=%s" % [
		_battle_node.get_node("BattleUI/Root/ResultLabel").text,
		_hp_text("Enemy1HP"),
	])

	print("[PASS] battle scene UI smoke")
	await get_tree().create_timer(0.1).timeout
	get_tree().quit()


func _press_end_turn() -> void:
	_battle_node.get_node("BattleUI/Root/BottomRight/EndTurnButton").pressed.emit()


func _press_enemy() -> void:
	_battle_node.get_node("BattleUI/Root/UnitOverlay/Enemy1TargetButton").pressed.emit()


func _click_card_view(card_view) -> void:
	var event := InputEventMouseButton.new()
	event.button_index = MOUSE_BUTTON_LEFT
	event.pressed = true
	card_view._gui_input(event)


func _play_first_enabled_card() -> bool:
	var card_view = _first_enabled_card_view()
	if card_view == null:
		return false
	_click_card_view(card_view)
	_press_enemy()
	return true


func _first_enabled_card_view():
	for card_view in _card_views():
		if not card_view._disabled:
			return card_view
	return null


func _first_disabled_card_view():
	for card_view in _card_views():
		if card_view._disabled and card_view.card.cost > _battle_node.battle.energy:
			return card_view
	return null


func _make_insufficient_card_visible() -> void:
	_battle_node.battle.energy = 0
	_battle_node._refresh_energy(_battle_node.battle.energy, _battle_node.battle.energy_max)
	_battle_node._refresh_hand()


func _card_views() -> Array:
	return _battle_node.get_node("BattleUI/Root/HandArea").get_children()


func _hand_count() -> int:
	return _card_views().size()


func _energy_text() -> String:
	return _battle_node.get_node("BattleUI/Root/BottomLeft/EnergyOrb/Label").text


func _intent_text() -> String:
	return _battle_node.get_node("BattleUI/Root/UnitOverlay/Enemy1Intent/Current/Label").text


func _hp_text(hp_bar_name: String) -> String:
	return _battle_node.get_node("BattleUI/Root/UnitOverlay/%s/HPText" % hp_bar_name).text


func _deck_text() -> String:
	return _battle_node.get_node("BattleUI/Root/BottomLeft/DeckPile/Label").text


func _discard_text() -> String:
	return _battle_node.get_node("BattleUI/Root/BottomRight/DiscardPile/Label").text


func _assert_equal(actual, expected, message: String) -> void:
	_assert_true(actual == expected, "%s | actual=%s expected=%s" % [message, str(actual), str(expected)])


func _assert_true(condition: bool, message: String) -> void:
	if condition:
		return
	push_error("[ASSERT FAIL] %s" % message)
	print("[FAIL] battle scene UI smoke: %s" % message)
	get_tree().quit(1)
