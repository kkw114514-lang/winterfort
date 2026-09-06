extends CharacterBody2D


enum State {
	IDLE,
	PATROL,
	CHASE
}

var current_state: State = State.PATROL

var speed: float = 100.0
var stop_distance: float = 80.0
var detection_distance: float = 300.0

var patrol_point_a: Vector2 = Vector2(150, 200)
var patrol_point_b: Vector2 = Vector2(450, 200)
var patrol_target: Vector2 = patrol_point_a

var wait_timer: float = 0.0
var wait_duration: float = 3.0

@onready var player = get_parent().get_node("Player")
@onready var sprite = get_node("Sprite2D")
@onready var state_label = get_node("StateLabel")


func _process(delta: float) -> void:
	var distance_to_player = position.distance_to(player.position)

	if distance_to_player <= detection_distance:
		current_state = State.CHASE
	elif current_state == State.CHASE:
		current_state = State.PATROL

	if current_state == State.IDLE:
		sprite.modulate = Color.GRAY
		state_label.text = "IDLE"
		wait_timer -= delta

		if wait_timer <= 0:
			current_state = State.PATROL

	elif current_state == State.PATROL:
		sprite.modulate = Color.WHITE
		state_label.text = "PATROL"

		var distance_to_target = position.distance_to(patrol_target)

		if distance_to_target > 10:
			var patrol_direction = (patrol_target - position).normalized()
			position += patrol_direction * speed * delta
		else:
			if patrol_target == patrol_point_a:
				patrol_target = patrol_point_b
			else:
				patrol_target = patrol_point_a

			wait_timer = wait_duration
			current_state = State.IDLE

	elif current_state == State.CHASE:
		sprite.modulate = Color.RED
		state_label.text = "CHASE"

		if distance_to_player > stop_distance:
			var chase_direction = (player.position - position).normalized()
			position += chase_direction * speed * delta
