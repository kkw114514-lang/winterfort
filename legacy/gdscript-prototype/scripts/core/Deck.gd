extends RefCounted
class_name Deck

var draw_pile: Array = []
var hand: Array = []
var discard_pile: Array = []
var exhaust_pile: Array = []

var _shuffle_rng: RandomNumberGenerator = null


func set_shuffle_rng(rng: RandomNumberGenerator) -> void:
	_shuffle_rng = rng


func setup(cards: Array, rng: RandomNumberGenerator = null) -> void:
	if rng != null:
		set_shuffle_rng(rng)

	draw_pile.clear()
	hand.clear()
	discard_pile.clear()
	exhaust_pile.clear()

	for entry in cards:
		if entry is Array:
			draw_pile.append_array(entry)
		else:
			draw_pile.append(entry)

	_shuffle(draw_pile)


func draw(amount: int, hand_limit: int = -1) -> Array:
	var drawn_cards: Array = []
	for i in range(maxi(amount, 0)):
		if draw_pile.is_empty():
			if discard_pile.is_empty():
				break
			draw_pile = discard_pile.duplicate()
			discard_pile.clear()
			_shuffle(draw_pile)

		var card = draw_pile.pop_back()
		if hand_limit >= 0 and hand.size() >= hand_limit:
			discard_pile.append(card)
		else:
			hand.append(card)
		drawn_cards.append(card)

	return drawn_cards


func discard_hand() -> void:
	discard_pile.append_array(hand)
	hand.clear()


func _shuffle(cards: Array) -> void:
	if _shuffle_rng == null:
		push_error("Deck shuffle RNG is not set. Inject RngStreams.get_stream(\"shuffle\") before shuffling.")
		return

	for i in range(cards.size() - 1, 0, -1):
		var j := _shuffle_rng.randi_range(0, i)
		var temp = cards[i]
		cards[i] = cards[j]
		cards[j] = temp
