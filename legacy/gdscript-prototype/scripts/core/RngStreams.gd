extends RefCounted
class_name RngStreams

const FNV_OFFSET_BASIS: int = -3750763034362895579
const FNV_PRIME: int = 1099511628211

var master_seed: int = 0

# TODO(M5): serialize master_seed plus each active stream.state for exact mid-run resume.
var _streams: Dictionary = {}


func _init(master_seed_value: int = 0) -> void:
	master_seed = master_seed_value


func get_stream(stream_name: String) -> RandomNumberGenerator:
	if _streams.has(stream_name):
		return _streams[stream_name]

	var stream := RandomNumberGenerator.new()
	stream.seed = derive_stream_seed(stream_name)
	_streams[stream_name] = stream
	return stream


func derive_stream_seed(stream_name: String) -> int:
	var h := FNV_OFFSET_BASIS
	for k in range(8):
		h = _fnv1a_step(h, (master_seed >> (8 * k)) & 0xFF)

	for b in stream_name.to_utf8_buffer():
		h = _fnv1a_step(h, int(b))

	return h


func _fnv1a_step(h: int, byte_value: int) -> int:
	return (h ^ (byte_value & 0xFF)) * FNV_PRIME
