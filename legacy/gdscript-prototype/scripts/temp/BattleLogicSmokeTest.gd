extends Node

const BattleScript := preload("res://scripts/core/Battle.gd")
const CardScript := preload("res://scripts/core/Card.gd")
const CombatantScript := preload("res://scripts/core/Combatant.gd")
const DeckScript := preload("res://scripts/core/Deck.gd")
const RngStreamsScript := preload("res://scripts/core/RngStreams.gd")
const MAX_AUTO_TURNS := 8
const SMOKE_SEED := 246813579
const DIFFERENT_SEED := 246813580


func _ready() -> void:
	call_deferred("_run")


func _run() -> void:
	print("[TEST] battle logic smoke test start")
	_assert_enemy_targeting_rule()
	_assert_economy_formula()
	_assert_hand_limit_rule()
	var trace := _run_battle(SMOKE_SEED, true)
	_assert_true(trace["battle_ended"], "8-turn smoke should finish the battle")
	_assert_true(trace["guard"] <= MAX_AUTO_TURNS, "8-turn smoke guard should not be exceeded")
	_assert_battle_determinism()
	print("[PASS] battle logic smoke determinism assertions")
	print("[TEST] battle logic smoke test end")
	await get_tree().create_timer(0.1).timeout
	get_tree().quit()


func _run_battle(master_seed: int, emit_logs: bool) -> Dictionary:
	var battle = _create_battle(master_seed)
	var trace := {
		"hands": [],
		"intent_powers": [int(battle.enemies[0].intent.get("power", 0))],
		"battle_ended": false,
		"guard": 0,
	}

	battle.turn_started.connect(func(_turn_number: int, _energy: int, _energy_max: int, _draw_count: int) -> void:
		trace["hands"].append(_card_names(battle.deck.hand))
	)
	battle.intent_locked.connect(func(_enemy, intent: Dictionary) -> void:
		trace["intent_powers"].append(int(intent.get("power", 0)))
	)

	if emit_logs:
		_connect_logs(battle)

	battle.start_player_turn()

	var guard := 0
	while not battle.battle_ended and guard < MAX_AUTO_TURNS:
		guard += 1
		_auto_play_turn(battle)
		if not battle.battle_ended:
			if emit_logs:
				print("[TURN END] hand_before_discard=%s" % _card_names(battle.deck.hand))
			battle.end_player_turn()

	if emit_logs and not battle.battle_ended:
		print("[TEST] stopped after %d auto turns without final result" % MAX_AUTO_TURNS)

	trace["battle_ended"] = battle.battle_ended
	trace["guard"] = guard
	return trace


func _assert_battle_determinism() -> void:
	var first := _run_battle(SMOKE_SEED, false)
	var second := _run_battle(SMOKE_SEED, false)
	var different := _run_battle(DIFFERENT_SEED, false)

	_assert_equal(first["hands"], second["hands"], "same seed should reproduce hand sequence")
	_assert_equal(first["intent_powers"], second["intent_powers"], "same seed should reproduce enemy intent powers")
	_assert_true(
		first["hands"] != different["hands"] or first["intent_powers"] != different["intent_powers"],
		"different seed should change hand or enemy intent sequence"
	)
	print("[OK] whole battle trace is reproducible by seed")


func _assert_enemy_targeting_rule() -> void:
	var battle = _create_battle(SMOKE_SEED)
	_assert_equal(battle.allies[0].entered_order, 1, "front enters after rear at battle start")
	_assert_equal(battle.allies[1].entered_order, 0, "rear enters before front at battle start")
	_assert_equal(battle._get_enemy_attack_target(), battle.allies[0], "enemy targets latest entered living front ally")

	battle.allies[0].alive = false
	_assert_equal(battle._get_enemy_attack_target(), battle.allies[1], "enemy falls back to rear when front is down")

	battle.allies[1].alive = false
	_assert_equal(battle._get_enemy_attack_target(), null, "enemy has no target when all allies are down")

	battle.allies[0].alive = true
	battle.allies[1].alive = true
	battle.allies[1].entered_order = battle.allies[0].entered_order + 1
	_assert_equal(battle._get_enemy_attack_target(), battle.allies[1], "manual latest-entered order transfers aggro")
	print("[OK] enemy targeting follows latest entered living ally")


func _assert_economy_formula() -> void:
	var battle = _create_battle(SMOKE_SEED)
	battle.start_player_turn()
	var expected_energy := _expected_energy_max(battle.allies)
	var expected_draw := _expected_draw_count(battle.allies)
	_assert_equal(expected_energy, 3, "baseline stamina fixture pins energy 3")
	_assert_equal(expected_draw, 5, "baseline speed fixture pins draw 5")
	_assert_equal(battle.energy_max, expected_energy, "opening energy_max follows stamina formula")
	_assert_equal(battle.energy, expected_energy, "opening energy fills to max")
	_assert_equal(battle.deck.hand.size(), expected_draw, "opening hand follows speed draw formula")

	var probe = _create_battle_with_stats(SMOKE_SEED + 1, 1, 1, 2, 2)
	probe.start_player_turn()
	var probe_expected_energy := _expected_energy_max(probe.allies)
	var probe_expected_draw := _expected_draw_count(probe.allies)
	_assert_equal(probe_expected_energy, 4, "probe stamina fixture pins energy 4")
	_assert_equal(probe_expected_draw, 7, "probe speed fixture pins draw 7")
	_assert_equal(probe.energy_max, probe_expected_energy, "probe energy_max follows stamina formula")
	_assert_equal(probe.deck.hand.size(), probe_expected_draw, "probe hand follows speed draw formula")
	print("[OK] economy formula draw=2*sum(speed)+1 energy=sum(stamina)+1")


func _assert_hand_limit_rule() -> void:
	var battle = _create_battle(SMOKE_SEED)
	_assert_equal(battle.hand_limit(), _expected_hand_limit(battle.allies), "two-spirit hand limit follows party-size formula")
	_assert_equal(battle.hand_limit(), 10, "two-spirit fixture pins hand limit 10")

	var third = CombatantScript.new("C", "C", true, 100)
	battle.allies.append(third)
	_assert_equal(battle.hand_limit(), _expected_hand_limit(battle.allies), "three-spirit hand limit follows party-size formula")
	_assert_equal(battle.hand_limit(), 12, "three-spirit fixture pins hand limit 12")

	var overflow_deck = DeckScript.new()
	overflow_deck.setup(_create_numbered_cards(12), RngStreamsScript.new(SMOKE_SEED + 2).get_stream("shuffle"))
	var overflow_drawn := overflow_deck.draw(10, 3)
	_assert_equal(overflow_deck.hand.size(), 3, "overflow draw fills hand only to limit")
	_assert_equal(overflow_deck.discard_pile.size(), 7, "overflow draw sends excess cards to discard")
	_assert_equal(overflow_deck.draw_pile.size(), 2, "overflow draw consumes requested cards from draw pile")
	_assert_equal(overflow_drawn.size(), 10, "overflow cards still count as drawn")

	var waste_deck = DeckScript.new()
	waste_deck.setup(_create_numbered_cards(3), RngStreamsScript.new(SMOKE_SEED + 3).get_stream("shuffle"))
	var waste_drawn := waste_deck.draw(5, 10)
	_assert_equal(waste_deck.hand.size(), 3, "draw stops when both draw and discard piles are empty")
	_assert_equal(waste_deck.draw_pile.size(), 0, "empty draw pile remains empty after wasted draw")
	_assert_equal(waste_drawn.size(), 3, "wasted draw returns only actually drawn cards")

	var reshuffle_deck = DeckScript.new()
	reshuffle_deck.setup(_create_numbered_cards(3), RngStreamsScript.new(SMOKE_SEED + 4).get_stream("shuffle"))
	reshuffle_deck.draw(2, 10)
	_assert_equal(reshuffle_deck.hand.size(), 2, "reshuffle setup draws first two cards")
	_assert_equal(reshuffle_deck.draw_pile.size(), 1, "reshuffle setup leaves one card in draw pile")
	reshuffle_deck.discard_hand()
	_assert_equal(reshuffle_deck.discard_pile.size(), 2, "reshuffle setup moves hand to discard")
	var reshuffle_drawn := reshuffle_deck.draw(3, 10)
	_assert_equal(reshuffle_drawn.size(), 3, "reshuffle draw consumes one draw-pile card plus shuffled discard")
	_assert_equal(reshuffle_deck.hand.size(), 3, "reshuffle draw ends with three cards in hand")
	_assert_equal(reshuffle_deck.draw_pile.size(), 0, "reshuffle draw empties draw pile after one reshuffle")
	_assert_equal(reshuffle_deck.discard_pile.size(), 0, "reshuffle draw clears discard pile into draw pile")
	print("[OK] hand limit and overflow draw rules")


func _create_battle(master_seed: int):
	return _create_battle_with_stats(master_seed, 1, 1, 1, 1)


func _create_battle_with_stats(
	master_seed: int,
	spirit_a_speed: int,
	spirit_a_stamina: int,
	spirit_b_speed: int,
	spirit_b_stamina: int
):
	var rng_streams = RngStreamsScript.new(master_seed)

	var spirit_a = CombatantScript.new("A", "A", true, 100)
	spirit_a.speed = spirit_a_speed
	spirit_a.stamina = spirit_a_stamina
	spirit_a.owner_tag = "A"

	var spirit_b = CombatantScript.new("B", "B", true, 100)
	spirit_b.speed = spirit_b_speed
	spirit_b.stamina = spirit_b_stamina
	spirit_b.owner_tag = "B"

	var enemy = CombatantScript.new("E1", "Enemy", false, 120)
	enemy.intent = {"type": "attack", "power": 10}

	var deck = DeckScript.new()
	deck.setup(_create_cards(), rng_streams.get_stream("shuffle"))

	return BattleScript.new(spirit_a, spirit_b, enemy, deck, rng_streams)


func _expected_energy_max(battle_allies: Array) -> int:
	var total_stamina := 0
	for ally in battle_allies:
		if ally != null and ally.alive:
			total_stamina += ally.stamina
	return total_stamina + 1


func _expected_draw_count(battle_allies: Array) -> int:
	var total_speed := 0
	for ally in battle_allies:
		if ally != null and ally.alive:
			total_speed += ally.speed
	return 2 * total_speed + 1


func _expected_hand_limit(battle_allies: Array) -> int:
	return BattleScript.HAND_LIMIT_BASE + battle_allies.size() * BattleScript.HAND_LIMIT_PER_SPIRIT


func _create_cards() -> Array:
	var cards: Array = []
	for i in range(6):
		var card_owner := "A" if i < 3 else "B"
		cards.append(CardScript.new("打击", 1, card_owner, [{"type": "damage", "power": 10, "target": "enemy"}]))
	for i in range(2):
		var card_owner := "A" if i == 0 else "B"
		cards.append(CardScript.new("重击", 2, card_owner, [{"type": "damage", "power": 22, "target": "enemy"}]))
	for i in range(2):
		var card_owner := "A" if i == 0 else "B"
		cards.append(CardScript.new("小击", 0, card_owner, [{"type": "damage", "power": 4, "target": "enemy"}]))
	return cards


func _create_numbered_cards(amount: int) -> Array:
	var cards: Array = []
	for i in range(amount):
		cards.append(CardScript.new("测试牌%d" % i, 0, "A", []))
	return cards


func _connect_logs(battle) -> void:
	battle.turn_started.connect(func(turn_number: int, energy: int, energy_max: int, draw_count: int) -> void:
		print("[TURN %d START] energy=%d/%d draw=%d hand_count=%d hand=%s" % [
			turn_number,
			energy,
			energy_max,
			draw_count,
			battle.deck.hand.size(),
			_card_names(battle.deck.hand),
		])
	)
	battle.card_played.connect(func(card, combo: int) -> void:
		var enemy = battle.enemies[0]
		print("[CARD] %s(%s) cost=%d -> energy=%d enemy_hp=%d combo=%d" % [
			card.name,
			card.owner,
			card.cost,
			battle.energy,
			enemy.hp,
			combo,
		])
	)
	battle.damage_dealt.connect(func(source, target, amount: int) -> void:
		var source_name = "None" if source == null else source.name
		print("[DAMAGE] %s -> %s amount=%d target_hp=%d alive=%s" % [
			source_name,
			target.name,
			amount,
			target.hp,
			str(target.alive),
		])
	)
	battle.intent_locked.connect(func(enemy, intent: Dictionary) -> void:
		print("[INTENT] %s next=%s %d" % [enemy.name, intent.get("type", ""), int(intent.get("power", 0))])
	)
	battle.combatant_died.connect(func(combatant) -> void:
		print("[DIED] %s" % combatant.name)
	)
	battle.battle_won.connect(func() -> void:
		var enemy = battle.enemies[0]
		print("[RESULT] battle_won enemy_hp=%d A_hp=%d B_hp=%d" % [
			enemy.hp,
			battle.allies[0].hp,
			battle.allies[1].hp,
		])
	)
	battle.battle_lost.connect(func() -> void:
		print("[RESULT] battle_lost A_hp=%d B_hp=%d" % [
			battle.allies[0].hp,
			battle.allies[1].hp,
		])
	)


func _auto_play_turn(battle) -> void:
	var played := true
	while played and not battle.battle_ended:
		played = false
		for card in battle.deck.hand.duplicate():
			if battle.energy >= card.cost:
				battle.play_card(card)
				played = true
				break


func _card_names(cards: Array) -> String:
	var names: Array[String] = []
	for card in cards:
		names.append("%s/%s/%d" % [card.name, card.owner, card.cost])
	return ",".join(names)


func _assert_equal(actual, expected, message: String) -> void:
	_assert_true(actual == expected, "%s | actual=%s expected=%s" % [message, str(actual), str(expected)])


func _assert_true(condition: bool, message: String) -> void:
	if condition:
		return
	push_error("[ASSERT FAIL] %s" % message)
	print("[FAIL] battle logic smoke test: %s" % message)
	get_tree().quit(1)
