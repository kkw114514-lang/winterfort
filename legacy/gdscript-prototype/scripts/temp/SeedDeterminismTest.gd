extends Node

const RngStreamsScript := preload("res://scripts/core/RngStreams.gd")
const SeedCodecScript := preload("res://scripts/core/SeedCodec.gd")

const MASTER_SEED := 1234567890123456789
const INT64_MAX := 9223372036854775807
const INT64_MIN := -9223372036854775807 - 1


func _ready() -> void:
	call_deferred("_run")


func _run() -> void:
	print("[TEST] seed determinism test start")
	_assert_reproducible_stream()
	_assert_stream_independence()
	_assert_different_master_seed_changes_stream()
	_assert_seed_codec_roundtrip()
	print("[PASS] seed determinism test")
	await get_tree().create_timer(0.1).timeout
	get_tree().quit()


func _assert_reproducible_stream() -> void:
	var left = RngStreamsScript.new(MASTER_SEED)
	var right = RngStreamsScript.new(MASTER_SEED)
	var left_values := _take_randi(left.get_stream("shuffle"), 20)
	var right_values := _take_randi(right.get_stream("shuffle"), 20)
	_assert_equal(left_values, right_values, "same master seed reproduces shuffle stream")
	print("[OK] reproducible shuffle stream")


func _assert_stream_independence() -> void:
	var left = RngStreamsScript.new(MASTER_SEED)
	_take_randi(left.get_stream("shuffle"), 100)
	var left_values := _take_randi(left.get_stream("enemy_ai"), 10)

	var right = RngStreamsScript.new(MASTER_SEED)
	var right_values := _take_randi(right.get_stream("enemy_ai"), 10)

	_assert_equal(left_values, right_values, "shuffle consumption must not affect enemy_ai")
	print("[OK] named streams are independent from other stream consumption")


func _assert_different_master_seed_changes_stream() -> void:
	var left = RngStreamsScript.new(MASTER_SEED)
	var right = RngStreamsScript.new(MASTER_SEED + 1)
	var left_values := _take_randi(left.get_stream("shuffle"), 20)
	var right_values := _take_randi(right.get_stream("shuffle"), 20)
	_assert_true(left_values != right_values, "different master seeds should not produce the same shuffle prefix")
	print("[OK] different master seeds change shuffle stream")


func _assert_seed_codec_roundtrip() -> void:
	var values: Array[int] = [
		0,
		1,
		-1,
		MASTER_SEED,
		-MASTER_SEED,
		INT64_MAX,
		INT64_MIN,
	]

	for value in values:
		var encoded := SeedCodecScript.encode(value)
		var decoded := SeedCodecScript.decode(encoded)
		_assert_equal(decoded, value, "seed code roundtrip for %s encoded as %s" % [str(value), encoded])

	var lowercase_with_separators := SeedCodecScript.encode(MASTER_SEED).to_lower().insert(4, "-").insert(9, " ")
	_assert_equal(
		SeedCodecScript.decode(lowercase_with_separators),
		MASTER_SEED,
		"seed decode ignores separators and case"
	)
	print("[OK] seed codec roundtrip")


func _take_randi(rng: RandomNumberGenerator, amount: int) -> Array[int]:
	var values: Array[int] = []
	for i in range(amount):
		values.append(rng.randi())
	return values


func _assert_equal(actual, expected, message: String) -> void:
	_assert_true(actual == expected, "%s | actual=%s expected=%s" % [message, str(actual), str(expected)])


func _assert_true(condition: bool, message: String) -> void:
	if condition:
		return
	push_error("[ASSERT FAIL] %s" % message)
	print("[FAIL] seed determinism test: %s" % message)
	get_tree().quit(1)
