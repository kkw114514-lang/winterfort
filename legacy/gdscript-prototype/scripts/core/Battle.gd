extends RefCounted
class_name Battle

signal turn_started(turn_number: int, energy: int, energy_max: int, draw_count: int)
signal energy_changed(energy: int, energy_max: int)
signal hand_changed(hand: Array)
signal card_played(card, combo: int)
signal damage_dealt(source, target, amount: int)
signal intent_locked(enemy, intent: Dictionary)
signal combatant_died(combatant)
signal battle_won
signal battle_lost

const DeckScript := preload("res://scripts/core/Deck.gd")
const RngStreamsScript := preload("res://scripts/core/RngStreams.gd")

const HAND_LIMIT_BASE := 6
const HAND_LIMIT_PER_SPIRIT := 2

var allies: Array = []
var enemies: Array = []
var deck = DeckScript.new()
# TODO(M5): move master_seed ownership up to RunState; Battle keeps streams until that exists.
var rng_streams = null

var energy: int = 0
var energy_max: int = 0
var combo: int = 0
var battle_ended: bool = false

var _turn_number: int = 0
var _entry_counter: int = 0


func _init(player_a = null, player_b = null, enemy_data = null, battle_deck = null, rng_source = null) -> void:
	if rng_source != null:
		set_rng_source(rng_source)
	if player_a != null and player_b != null:
		allies = [player_a, player_b]
		_assign_entry_order()
	if enemy_data != null:
		if enemy_data is Array:
			enemies = enemy_data
		else:
			enemies = [enemy_data]
	if battle_deck != null:
		deck = battle_deck
		_inject_deck_shuffle_rng()


func setup(player_a, player_b, enemy_data, battle_deck, rng_source = null) -> void:
	if rng_source != null:
		set_rng_source(rng_source)
	allies = [player_a, player_b]
	_assign_entry_order()
	if enemy_data is Array:
		enemies = enemy_data
	else:
		enemies = [enemy_data]
	deck = battle_deck
	_inject_deck_shuffle_rng()
	energy = 0
	energy_max = 0
	combo = 0
	battle_ended = false
	_turn_number = 0


func set_rng_source(rng_source) -> void:
	if typeof(rng_source) == TYPE_INT:
		set_rng_streams(RngStreamsScript.new(rng_source))
	else:
		set_rng_streams(rng_source)


func set_rng_streams(streams) -> void:
	rng_streams = streams
	_inject_deck_shuffle_rng()


func start_player_turn() -> void:
	if battle_ended:
		return

	_turn_number += 1
	var alive_allies := _get_alive_allies()
	var total_stamina := 0
	var total_speed := 0
	for ally in alive_allies:
		total_stamina += ally.stamina
		total_speed += ally.speed

	energy_max = total_stamina + 1
	_set_energy(energy_max)
	var draw_count := 2 * total_speed + 1
	deck.draw(draw_count, hand_limit())
	combo = 0

	hand_changed.emit(deck.hand.duplicate())
	turn_started.emit(_turn_number, energy, energy_max, draw_count)


func hand_limit() -> int:
	return HAND_LIMIT_BASE + allies.size() * HAND_LIMIT_PER_SPIRIT


func play_card(card) -> bool:
	if battle_ended or card == null:
		return false
	if not deck.hand.has(card):
		return false
	if energy < card.cost:
		return false

	_set_energy(energy - card.cost)
	for effect in card.effects:
		_execute_effect(card, effect)

	deck.hand.erase(card)
	deck.discard_pile.append(card)
	combo += 1

	card_played.emit(card, combo)
	hand_changed.emit(deck.hand.duplicate())
	_check_victory()
	return true


func end_player_turn() -> void:
	if battle_ended:
		return
	deck.discard_hand()
	hand_changed.emit(deck.hand.duplicate())
	enemy_turn()


func enemy_turn() -> void:
	if battle_ended:
		return

	for enemy in _get_alive_enemies():
		var locked_intent: Dictionary = enemy.intent
		if locked_intent.get("type", "") == "attack":
			var target = _get_enemy_attack_target()
			if target != null:
				deal_damage(enemy, target, int(locked_intent.get("power", 0)))

		var enemy_ai_rng = _get_enemy_ai_rng()
		if enemy_ai_rng == null:
			return
		enemy.intent = {
			"type": "attack",
			"power": enemy_ai_rng.randi_range(8, 12),
		}
		intent_locked.emit(enemy, enemy.intent.duplicate(true))

	_check_victory()
	if not battle_ended:
		start_player_turn()


func deal_damage(source, target, amount: int) -> int:
	if target == null or not target.alive:
		return 0

	var final_amount := maxi(amount, 0)
	final_amount = _apply_status_damage_hooks(source, target, final_amount)
	final_amount = _apply_shield_absorb_hook(target, final_amount)

	target.hp = clampi(target.hp - final_amount, 0, target.max_hp)
	if target.hp <= 0 and target.alive:
		target.alive = false
		combatant_died.emit(target)

	damage_dealt.emit(source, target, final_amount)
	return final_amount


func _execute_effect(card, effect) -> void:
	if not (effect is Dictionary):
		return
	if effect.get("type", "") != "damage":
		return

	var source = _get_ally_by_owner(card.owner)
	var target = null
	match effect.get("target", ""):
		"enemy":
			target = _get_first_alive_enemy()
		_:
			target = _get_first_alive_enemy()

	if target != null:
		deal_damage(source, target, int(effect.get("power", 0)))


func _apply_status_damage_hooks(_source, _target, amount: int) -> int:
	return amount


func _apply_shield_absorb_hook(_target, amount: int) -> int:
	return amount


func _get_enemy_ai_rng():
	if rng_streams == null:
		push_error("Battle enemy_ai RNG stream is not set. Inject RngStreams or a master seed before enemy_turn().")
		return null
	return rng_streams.get_stream("enemy_ai")


func _inject_deck_shuffle_rng() -> void:
	if rng_streams == null or deck == null:
		return
	deck.set_shuffle_rng(rng_streams.get_stream("shuffle"))


func _assign_entry_order() -> void:
	_entry_counter = 0
	for i in range(allies.size() - 1, -1, -1):
		if allies[i] != null:
			allies[i].entered_order = _entry_counter
			_entry_counter += 1


func _set_energy(value: int) -> void:
	energy = clampi(value, 0, energy_max)
	energy_changed.emit(energy, energy_max)


func _get_alive_allies() -> Array:
	return allies.filter(func(ally) -> bool: return ally != null and ally.alive)


func _get_alive_enemies() -> Array:
	return enemies.filter(func(enemy) -> bool: return enemy != null and enemy.alive)


func _get_ally_by_owner(owner: String):
	for ally in allies:
		if ally.owner_tag == owner or ally.id == owner:
			return ally
	return null


func _get_first_alive_enemy():
	for enemy in enemies:
		if enemy != null and enemy.alive:
			return enemy
	return null


func _get_enemy_attack_target():
	# Aggro = latest entered living ally. Initial order is rear first, front last,
	# so opening attacks hit position 1; if it dies, aggro falls back to the next latest.
	# TODO(Spirit swap): when a swap action exists, assign the entering ally _entry_counter++.
	var best = null
	for ally in allies:
		if ally != null and ally.alive and (best == null or ally.entered_order > best.entered_order):
			best = ally
	return best


func _check_victory() -> void:
	if battle_ended:
		return

	if _get_alive_enemies().is_empty():
		battle_ended = true
		battle_won.emit()
		return

	if _get_alive_allies().is_empty():
		battle_ended = true
		battle_lost.emit()
