extends RefCounted
class_name SeedCodec

const ALPHABET := "0123456789ABCDEFGHJKMNPQRSTVWXYZ"
const ENCODED_GROUP_COUNT := 13
const GROUP_BITS := 5
const GROUP_MASK := 0x1F


static func encode(value: int) -> String:
	var chars: Array[String] = []
	for group_index in range(ENCODED_GROUP_COUNT - 1, -1, -1):
		var shift := group_index * GROUP_BITS
		var digit := (value >> shift) & GROUP_MASK
		chars.append(ALPHABET.substr(digit, 1))
	return "".join(chars)


static func decode(seed_code: String) -> int:
	var digits: Array[int] = []
	for i in range(seed_code.length()):
		var ch := seed_code.substr(i, 1).to_upper()
		if _is_separator(ch):
			continue

		var digit := ALPHABET.find(ch)
		if digit < 0:
			push_error("Invalid seed code character: %s" % ch)
			return 0
		digits.append(digit)

	if digits.size() > ENCODED_GROUP_COUNT:
		push_error("Seed code is too long: %s" % seed_code)
		return 0

	var value := 0
	var output_group := 0
	for input_group in range(digits.size() - 1, -1, -1):
		value |= digits[input_group] << (output_group * GROUP_BITS)
		output_group += 1

	return value


static func _is_separator(ch: String) -> bool:
	return ch == "-" or ch == "_" or ch == " " or ch == "\t" or ch == "\n" or ch == "\r"
