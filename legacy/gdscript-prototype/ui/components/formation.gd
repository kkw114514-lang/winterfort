extends RefCounted
class_name Formation


static func layout(count: int, near: Vector2, near_scale: float, far: Vector2, far_scale: float) -> Array:
	var slots := []
	if count <= 0:
		return slots
	if count == 1:
		slots.append({
			"position": near,
			"scale": clampf(near_scale, BattleLayout.SCALE_MIN, BattleLayout.SCALE_MAX),
		})
		return slots

	for i in range(count):
		var t := float(i) / float(count - 1)
		slots.append({
			"position": near.lerp(far, t),
			"scale": clampf(lerpf(near_scale, far_scale, t), BattleLayout.SCALE_MIN, BattleLayout.SCALE_MAX),
		})
	return slots
