extends RefCounted
class_name DevBattleFactory

const BattleScript := preload("res://scripts/core/Battle.gd")
const CardScript := preload("res://scripts/core/Card.gd")
const CombatantScript := preload("res://scripts/core/Combatant.gd")
const DeckScript := preload("res://scripts/core/Deck.gd")
const RngStreamsScript := preload("res://scripts/core/RngStreams.gd")

const DEV_SEED := 20260701


static func create_dev_battle() -> Battle:
	var rng_streams = RngStreamsScript.new(DEV_SEED)

	var spirit_a = CombatantScript.new("A", "A", true, 100)
	spirit_a.speed = 1
	spirit_a.stamina = 1
	spirit_a.owner_tag = "A"

	var spirit_b = CombatantScript.new("B", "B", true, 100)
	spirit_b.speed = 1
	spirit_b.stamina = 1
	spirit_b.owner_tag = "B"

	var enemy = CombatantScript.new("E1", "Enemy", false, 120)
	enemy.intent = {"type": "attack", "power": 10}

	var deck = DeckScript.new()
	deck.setup(_create_dev_cards(), rng_streams.get_stream("shuffle"))

	# TODO(M3): replace this dev fixture with exported card/spirit/enemy data.
	# TODO(M5): move DEV_SEED ownership to RunState or the battle entry menu.
	return BattleScript.new(spirit_a, spirit_b, enemy, deck, rng_streams)


static func _create_dev_cards() -> Array:
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
