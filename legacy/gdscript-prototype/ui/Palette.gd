extends RefCounted
class_name Palette

const COLORS := {
	"color/status/burn": Color(1.0, 0.478431, 0.227451, 1.0), # #ff7a3a
	"color/status/poison": Color(0.498039, 0.878431, 0.478431, 1.0), # #7fe07a
	"color/status/shield": Color(0.498039, 0.831373, 1.0, 1.0), # #7fd4ff
	"color/status/power": Color(1.0, 0.811765, 0.352941, 1.0), # #ffcf5a
	"color/status/regen": Color(0.560784, 0.878431, 0.690196, 1.0), # #8fe0b0
	"color/status/weak": Color(0.560784, 0.627451, 0.709804, 1.0), # #8fa0b5
	"color/status/freeze": Color(0.74902, 0.909804, 1.0, 1.0), # #bfe8ff
	"color/status/mark": Color(0.72549, 0.545098, 0.878431, 1.0), # #b98be0
	"color/enemy": Color(0.784314, 0.192157, 0.227451, 1.0), # #c8313a
	"color/energy": Color(0.435294, 0.388235, 0.847059, 1.0), # #6f63d8
	"color/gold/hi": Color(0.941176, 0.839216, 0.564706, 1.0), # #f0d690
	"color/parch": Color(0.788235, 0.658824, 0.372549, 1.0), # #c9a85f
}


static func get_color(token: String, fallback: Color = Color.WHITE) -> Color:
	return COLORS.get(token, fallback)
