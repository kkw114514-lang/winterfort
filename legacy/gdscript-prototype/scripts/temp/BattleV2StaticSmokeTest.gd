extends Node

const BattleV2Scene := preload("res://battle_v2.tscn")

var _battle_node: Control = null


func _ready() -> void:
	call_deferred("_run")


func _run() -> void:
	print("[TEST] battle_v2 static smoke start")
	_battle_node = BattleV2Scene.instantiate()
	add_child(_battle_node)
	await get_tree().process_frame

	_assert_equal(_battle_node.hand_card_count(), _battle_node.battle.deck.hand.size(), "hand card_view count")
	_assert_equal(_battle_node.energy_text(), "3/3", "opening energy text")
	_assert_equal(_battle_node.ally_view_count(), _battle_node.battle.allies.size(), "ally combatant_view count")
	_assert_layout_geometry()
	print("[OK] battle_v2 opening energy=%s hand=%d allies=%d enemy_hp=%d/%d" % [
		_battle_node.energy_text(),
		_battle_node.hand_card_count(),
		_battle_node.ally_view_count(),
		_battle_node.battle.enemies[0].hp,
		_battle_node.battle.enemies[0].max_hp,
	])
	print("[PASS] battle_v2 static smoke")
	await get_tree().create_timer(0.1).timeout
	get_tree().quit()


func _assert_layout_geometry() -> void:
	for record in _battle_node.layout_records():
		var unit = record["unit"]
		var anchor: Vector2 = record["anchor"]
		_assert_true(absf(unit.portrait_global_rect().end.y - anchor.y) <= 1.0, "%s portrait foot should match formation anchor" % unit.name)
		_assert_true(absf(unit.health_global_rect().position.y - (anchor.y + BattleLayout.COMBATANT_HP_GAP)) <= 1.0, "%s health bar should sit below foot line" % unit.name)
		_assert_true(absf(unit.shadow_global_rect().get_center().y - anchor.y) <= 1.0, "%s shadow center should sit on foot line" % unit.name)
		if str(record["kind"]) == "enemy" and int(record["index"]) == 0:
			_assert_true(not unit.is_targeted(), "Enemy1 target ring should be hidden before card selection")


func _assert_equal(actual, expected, message: String) -> void:
	_assert_true(actual == expected, "%s | actual=%s expected=%s" % [message, str(actual), str(expected)])


func _assert_true(condition: bool, message: String) -> void:
	if condition:
		return
	push_error("[BattleV2StaticSmokeTest] %s" % message)
	print("[FAIL] battle_v2 static smoke: %s" % message)
	get_tree().quit(1)
