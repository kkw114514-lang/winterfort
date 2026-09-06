extends Node

const BattleV2Scene := preload("res://battle_v2.tscn")

var _battle_node: Control = null


func _ready() -> void:
	call_deferred("_run")


func _run() -> void:
	print("[TEST] battle_v2 UI smoke start")
	_battle_node = BattleV2Scene.instantiate()
	add_child(_battle_node)
	await get_tree().process_frame

	_assert_opening_state()

	var enemy_hp_before: int = _battle_node.battle.enemies[0].hp
	var hand_before: int = _battle_node.hand_card_count()
	var energy_before: int = _battle_node.battle.energy
	var selected_card = _first_enabled_card_view()
	_assert_true(selected_card != null, "opening hand has a playable card")
	_click_card_view(selected_card)
	_assert_true(_battle_node.enemy_view(0).is_targeted(), "selecting a playable card targets Enemy1")
	_press_enemy()
	await get_tree().process_frame
	_assert_true(_battle_node.battle.enemies[0].hp < enemy_hp_before, "click card then enemy deals damage")
	_assert_equal(_battle_node.hand_card_count(), hand_before - 1, "played card leaves hand")
	_assert_true(_battle_node.battle.energy <= energy_before, "playing a card does not increase energy")
	print("[OK] click card -> enemy enemy_hp=%d/%d energy=%s hand=%d" % [
		_battle_node.battle.enemies[0].hp,
		_battle_node.battle.enemies[0].max_hp,
		_battle_node.energy_text(),
		_battle_node.hand_card_count(),
	])

	var disabled_card = await _make_insufficient_card_visible()
	_assert_true(disabled_card != null, "a card is unaffordable when energy is insufficient")
	enemy_hp_before = _battle_node.battle.enemies[0].hp
	_click_card_view(disabled_card)
	_press_enemy()
	await get_tree().process_frame
	_assert_equal(_battle_node.battle.enemies[0].hp, enemy_hp_before, "unaffordable card cannot be played")
	print("[OK] insufficient card cannot play enemy_hp=%d/%d energy=%s" % [
		_battle_node.battle.enemies[0].hp,
		_battle_node.battle.enemies[0].max_hp,
		_battle_node.energy_text(),
	])

	_press_end_turn()
	await get_tree().process_frame
	_assert_equal(_battle_node.energy_text(), "3/3", "energy resets after end turn")
	_assert_true(_battle_node.battle.allies[0].hp < 100, "front spirit takes locked enemy intent damage")
	_assert_equal(_battle_node.battle.allies[1].hp, 100, "rear spirit is not hit while front lives")
	var front_hp_after_first: int = _battle_node.battle.allies[0].hp
	print("[OK] after one end turn energy=%s front_hp=%d/100 rear_hp=%d/100" % [
		_battle_node.energy_text(),
		_battle_node.battle.allies[0].hp,
		_battle_node.battle.allies[1].hp,
	])

	_press_end_turn()
	await get_tree().process_frame
	_assert_true(_battle_node.battle.allies[0].hp < front_hp_after_first, "second end turn advances enemy attack again")
	_assert_equal(_battle_node.battle.allies[1].hp, 100, "rear spirit still stays untouched while front lives")
	print("[OK] after two end turns front_hp=%d/100 rear_hp=%d/100" % [
		_battle_node.battle.allies[0].hp,
		_battle_node.battle.allies[1].hp,
	])

	var guard := 0
	while not _battle_node.battle.battle_ended and guard < 20:
		guard += 1
		while _play_first_enabled_card():
			await get_tree().process_frame
		if not _battle_node.battle.battle_ended:
			_press_end_turn()
			await get_tree().process_frame
	_assert_true(_battle_node.battle.battle_ended, "UI can play through to battle end within guard")
	_assert_equal(_battle_node.result_text(), "胜利", "battle_v2 result text")
	_assert_true(not _battle_node.end_turn_enabled(), "end turn disabled after result")
	print("[OK] battle_v2 ended result=%s enemy_hp=%d guard=%d" % [
		_battle_node.result_text(),
		_battle_node.battle.enemies[0].hp,
		guard,
	])

	print("[PASS] battle_v2 UI smoke")
	await get_tree().create_timer(0.1).timeout
	get_tree().quit()


func _assert_opening_state() -> void:
	_assert_equal(_battle_node.energy_text(), "3/3", "opening energy")
	_assert_equal(_battle_node.hand_card_count(), 5, "opening hand count")
	_assert_equal(_battle_node.battle.enemies[0].hp, 120, "enemy opening HP")
	_assert_equal(_battle_node.battle.enemies[0].max_hp, 120, "enemy opening max HP")
	_assert_equal(_battle_node.battle.allies[0].hp, 100, "front ally opening HP")
	_assert_equal(_battle_node.battle.allies[0].max_hp, 100, "front ally opening max HP")
	_assert_equal(_battle_node.battle.allies[1].hp, 100, "rear ally opening HP")
	_assert_equal(_battle_node.battle.allies[1].max_hp, 100, "rear ally opening max HP")
	_assert_equal(int(_battle_node.battle.enemies[0].intent.get("power", 0)), 10, "opening intent power")
	_assert_true(not _battle_node.enemy_view(0).is_targeted(), "opening target ring is hidden")
	print("[OK] opening energy=%s hand=%d enemy_hp=%d/%d intent=%d" % [
		_battle_node.energy_text(),
		_battle_node.hand_card_count(),
		_battle_node.battle.enemies[0].hp,
		_battle_node.battle.enemies[0].max_hp,
		int(_battle_node.battle.enemies[0].intent.get("power", 0)),
	])


func _make_insufficient_card_visible():
	var disabled_card = _first_disabled_card_view()
	var guard := 0
	while disabled_card == null and guard < 5 and not _battle_node.battle.battle_ended:
		guard += 1
		var card_view = _highest_cost_enabled_card_view()
		if card_view == null:
			break
		_click_card_view(card_view)
		_press_enemy()
		await get_tree().process_frame
		disabled_card = _first_disabled_card_view()
	return disabled_card


func _play_first_enabled_card() -> bool:
	var card_view = _first_enabled_card_view()
	if card_view == null:
		return false
	_click_card_view(card_view)
	_press_enemy()
	return true


func _first_enabled_card_view():
	for card_view in _battle_node.hand_card_views():
		var card = _battle_node.card_for_view(card_view)
		if card != null and _battle_node.battle.deck.hand.has(card) and card.cost <= _battle_node.battle.energy:
			return card_view
	return null


func _highest_cost_enabled_card_view():
	var best = null
	var best_cost := -1
	for card_view in _battle_node.hand_card_views():
		var card = _battle_node.card_for_view(card_view)
		if card == null or not _battle_node.battle.deck.hand.has(card):
			continue
		if card.cost <= _battle_node.battle.energy and card.cost > best_cost:
			best = card_view
			best_cost = card.cost
	return best


func _first_disabled_card_view():
	for card_view in _battle_node.hand_card_views():
		var card = _battle_node.card_for_view(card_view)
		if card != null and _battle_node.battle.deck.hand.has(card) and card.cost > _battle_node.battle.energy:
			return card_view
	return null


func _click_card_view(card_view) -> void:
	card_view.card_clicked.emit(card_view)


func _press_enemy() -> void:
	var enemy_view = _battle_node.enemy_view(0)
	if enemy_view != null:
		enemy_view.view_clicked.emit(enemy_view)


func _press_end_turn() -> void:
	_battle_node.end_turn_button_view().pressed.emit()


func _assert_equal(actual, expected, message: String) -> void:
	_assert_true(actual == expected, "%s | actual=%s expected=%s" % [message, str(actual), str(expected)])


func _assert_true(condition: bool, message: String) -> void:
	if condition:
		return
	push_error("[BattleV2UiSmokeTest] %s" % message)
	print("[FAIL] battle_v2 UI smoke: %s" % message)
	get_tree().quit(1)
