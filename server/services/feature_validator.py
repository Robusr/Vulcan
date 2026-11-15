# Validates the JSON feature tree before it reaches SolidWorks.
# Keep this permissive enough for current models but strict on values that
# would produce broken geometry or COM failures.

ALLOWED_FEATURE_TYPES = {
    "extrude",
    "cut",
    "hole",
    "revolve",
    "fillet",
    "chamfer",
    "linear_pattern",
    "circular_pattern",
    "mirror",
    "shell",
    "draft",
    "sweep",
    "loft",
    "rib",
    "counterbore",
    "countersink",
    "hole_array",
    "note",
    "insert_component",
}
ALLOWED_SHAPES = {"circle", "rectangle", "polygon", "ellipse", "slot"}
ALLOWED_DIRECTIONS = {"x", "y", "z"}
ALLOWED_MIRROR_PLANES = {
    "front",
    "top",
    "right",
    "face",
    "dessus",
    "droite",
    "前视基准面",
    "上视基准面",
    "右视基准面",
}
ALLOWED_REMOVE_FACES = {"top", "bottom", "front", "back", "left", "right", "none"}
ALLOWED_DRAFT_FACES = {"sides", "top", "bottom"}
ALLOWED_NEUTRAL_PLANES = {"front", "top", "right", "face", "dessus", "droite"}


def _is_number(value):
    return isinstance(value, (int, float)) and not isinstance(value, bool)


def _number(params, key, errors, minimum=None, maximum=None, required=True):
    if key not in params:
        if required:
            errors.append(f"缺少 {key}")
        return None
    value = params[key]
    if not _is_number(value):
        errors.append(f"{key} 必须是数字")
        return None
    if minimum is not None and value < minimum:
        errors.append(f"{key} 必须 >= {minimum}")
    if maximum is not None and value > maximum:
        errors.append(f"{key} 必须 <= {maximum}")
    return value


def _integer(params, key, errors, minimum=None, maximum=None, required=True):
    value = _number(params, key, errors, minimum=minimum, maximum=maximum, required=required)
    if value is not None and int(value) != value:
        errors.append(f"{key} 必须是整数")
    return value


def _string(params, key, errors, allowed=None, required=True):
    if key not in params:
        if required:
            errors.append(f"缺少 {key}")
        return None
    value = params[key]
    if not isinstance(value, str) or not value.strip():
        errors.append(f"{key} 必须是非空字符串")
        return None
    value = value.strip()
    if allowed is not None and value.lower() not in allowed:
        errors.append(f"{key} 不支持: {value}")
    return value


def _bool(params, key, errors, required=False, default=False):
    if key not in params:
        if required:
            errors.append(f"缺少 {key}")
        return default
    value = params[key]
    if not isinstance(value, bool):
        errors.append(f"{key} 必须是布尔值")
        return default
    return value


def _validate_shape(params, errors):
    shape = _string(params, "shape", errors, allowed=ALLOWED_SHAPES)
    if shape is None:
        return
    shape = shape.lower()

    if shape == "circle":
        _number(params, "diameter", errors, minimum=0.01)
    elif shape in {"rectangle", "slot"}:
        _number(params, "length", errors, minimum=0.01)
        _number(params, "width", errors, minimum=0.01)
    elif shape == "polygon":
        _number(params, "diameter", errors, minimum=0.01)
        sides = _number(params, "sides", errors, minimum=3, maximum=100)
        if sides is not None and int(sides) != sides:
            errors.append("sides 必须是整数")
    elif shape == "ellipse":
        _number(params, "major_axis", errors, minimum=0.01)
        _number(params, "minor_axis", errors, minimum=0.01)


def _validate_sketch_feature(feature_type, params, errors):
    _string(params, "plane", errors)
    _validate_shape(params, errors)

    if feature_type == "extrude":
        _number(params, "depth", errors, minimum=0.01)

    if feature_type in {"cut", "hole"}:
        through_all = params.get("through_all", False)
        if not isinstance(through_all, bool):
            errors.append("through_all 必须是布尔值")
        if not through_all:
            _number(params, "depth", errors, minimum=0.01)

    if feature_type in {"extrude", "cut", "hole"}:
        _number(params, "draft_angle", errors, minimum=-89, maximum=89, required=False)
        _number(params, "center_x", errors, required=False)
        _number(params, "center_y", errors, required=False)

    if feature_type == "revolve":
        _number(params, "center_x", errors, required=False)
        _number(params, "center_y", errors, required=False)
        axis = _string(params, "axis", errors, allowed={"x", "y"}, required=False)
        if axis is not None:
            axis = axis.lower()
        else:
            axis = "y"
        angle = _number(params, "angle", errors, minimum=0.01, maximum=360, required=False)
        if angle is None:
            angle = 360.0

        # The profile must not sit exactly on the revolution axis.
        cx = params.get("center_x", 0)
        cy = params.get("center_y", 0)
        if _is_number(cx) and _is_number(cy):
            if axis == "y" and abs(cx) < 0.01:
                errors.append("revolve 的 center_x 不能为 0，轮廓必须偏离旋转轴")
            if axis == "x" and abs(cy) < 0.01:
                errors.append("revolve 的 center_y 不能为 0，轮廓必须偏离旋转轴")


def _validate_seed(params, errors):
    _string(params, "seed", errors, required=False)


def _validate_linear_pattern(params, errors):
    _validate_seed(params, errors)
    _string(params, "direction", errors, allowed=ALLOWED_DIRECTIONS)
    _integer(params, "count", errors, minimum=2, maximum=1000)
    _number(params, "spacing", errors, minimum=0.01)
    _bool(params, "flip", errors)

    count2 = params.get("count2")
    spacing2 = params.get("spacing2")
    if (count2 is None) != (spacing2 is None):
        errors.append("count2 与 spacing2 必须同时提供")
    else:
        _integer(params, "count2", errors, minimum=2, maximum=1000, required=False)
        _number(params, "spacing2", errors, minimum=0.01, required=False)


def _validate_circular_pattern(params, errors):
    _validate_seed(params, errors)
    _string(params, "axis", errors, allowed=ALLOWED_DIRECTIONS)
    _integer(params, "count", errors, minimum=2, maximum=1000)
    _number(params, "angle", errors, minimum=0.01, maximum=360)
    _bool(params, "equal_spacing", errors)
    _bool(params, "flip", errors)


def _validate_mirror(params, errors):
    _validate_seed(params, errors)
    _string(params, "plane", errors, allowed=ALLOWED_MIRROR_PLANES)
    _bool(params, "merge", errors)


def _validate_shell(params, errors):
    _number(params, "thickness", errors, minimum=0.01)
    _string(params, "remove_faces", errors, allowed=ALLOWED_REMOVE_FACES, required=False)
    _bool(params, "outward", errors)


def _validate_draft(params, errors):
    _number(params, "angle", errors, minimum=-89, maximum=89)
    _string(params, "faces", errors, allowed=ALLOWED_DRAFT_FACES, required=False)
    _string(params, "neutral_plane", errors, allowed=ALLOWED_NEUTRAL_PLANES, required=False)
    _integer(params, "propagation", errors, minimum=0, maximum=4, required=False)


ALLOWED_PATH_SHAPES = {"line", "arc"}


def _validate_profile(profile, errors, prefix):
    if not isinstance(profile, dict):
        errors.append(f"{prefix} 必须是 JSON 对象")
        return
    sub_errors = []
    _validate_shape(profile, sub_errors)
    _number(profile, "center_x", sub_errors, required=False)
    _number(profile, "center_y", sub_errors, required=False)
    errors.extend(prefix + error for error in sub_errors)


def _validate_sweep(params, errors):
    _number(params, "diameter", errors, minimum=0.01)
    _number(params, "path_length", errors, minimum=0.01)
    _string(params, "path_plane", errors, allowed=ALLOWED_MIRROR_PLANES, required=False)
    _string(params, "path_shape", errors, allowed=ALLOWED_PATH_SHAPES, required=False)
    _number(params, "path_center_x", errors, required=False)
    _number(params, "path_center_y", errors, required=False)


def _validate_loft(params, errors):
    _string(params, "plane", errors, required=False)
    _number(params, "height", errors, minimum=0.01)
    if "profile1" not in params:
        errors.append("缺少 profile1")
    else:
        _validate_profile(params["profile1"], errors, "profile1.")
    if "profile2" not in params:
        errors.append("缺少 profile2")
    else:
        _validate_profile(params["profile2"], errors, "profile2.")


def _validate_hole_body(params, errors):
    _string(params, "plane", errors)
    through_all = params.get("through_all", False)
    if not isinstance(through_all, bool):
        errors.append("through_all 必须是布尔值")
    if not through_all:
        _number(params, "depth", errors, minimum=0.01)
    _number(params, "center_x", errors, required=False)
    _number(params, "center_y", errors, required=False)


def _validate_counterbore(params, errors):
    _validate_hole_body(params, errors)
    hole_diameter = _number(params, "hole_diameter", errors, minimum=0.01)
    counterbore_diameter = _number(params, "counterbore_diameter", errors, minimum=0.01)
    _number(params, "counterbore_depth", errors, minimum=0.01)
    if (
        hole_diameter is not None
        and counterbore_diameter is not None
        and counterbore_diameter <= hole_diameter
    ):
        errors.append("counterbore_diameter 必须大于 hole_diameter")


def _validate_countersink(params, errors):
    _validate_hole_body(params, errors)
    hole_diameter = _number(params, "hole_diameter", errors, minimum=0.01)
    countersink_diameter = _number(params, "countersink_diameter", errors, minimum=0.01)
    _number(params, "countersink_angle", errors, minimum=30, maximum=180, required=False)
    if (
        hole_diameter is not None
        and countersink_diameter is not None
        and countersink_diameter <= hole_diameter
    ):
        errors.append("countersink_diameter 必须大于 hole_diameter")


def _validate_hole_array(params, errors):
    _validate_hole_body(params, errors)
    _number(params, "diameter", errors, minimum=0.01)
    _integer(params, "count", errors, minimum=2, maximum=100)
    _number(params, "spacing", errors, minimum=0.01)
    _string(params, "direction", errors, allowed={"x", "y"}, required=False)
    _number(params, "angle", errors, minimum=-360, maximum=360, required=False)


def _validate_note(params, errors):
    text = _string(params, "text", errors)
    if text is not None and len(text) > 500:
        errors.append("text 不能超过 500 个字符")
    _number(params, "x", errors, minimum=-10000, maximum=10000, required=False)
    _number(params, "y", errors, minimum=-10000, maximum=10000, required=False)
    _number(params, "height", errors, minimum=0.1, maximum=100, required=False)


def _validate_insert_component(params, errors):
    path = _string(params, "path", errors)
    if path is not None:
        if len(path) > 260:
            errors.append("path 过长")
        if not path.lower().endswith((".sldprt", ".sldasm")):
            errors.append("path 必须指向 .sldprt 或 .sldasm 文件")
    _number(params, "x", errors, minimum=-100000, maximum=100000, required=False)
    _number(params, "y", errors, minimum=-100000, maximum=100000, required=False)
    _number(params, "z", errors, minimum=-100000, maximum=100000, required=False)
    _bool(params, "fixed", errors)


def _validate_rib(params, errors):
    _number(params, "thickness", errors, minimum=0.01)
    _number(params, "length", errors, minimum=0.01)
    _string(params, "direction", errors, allowed={"x", "y"}, required=False)
    _string(params, "plane", errors, required=False)
    _number(params, "center_x", errors, required=False)
    _number(params, "center_y", errors, required=False)
    _number(params, "draft_angle", errors, minimum=-89, maximum=89, required=False)
    _bool(params, "two_sided", errors)


def validate_feature(feature, prefix=""):
    if not isinstance(feature, dict):
        return [f"{prefix}特征必须是 JSON 对象"]

    errors = []
    feature_type = feature.get("feature_type")
    if not isinstance(feature_type, str) or not feature_type.strip():
        return [f"{prefix}缺少 feature_type"]
    feature_type = feature_type.strip().lower()
    if feature_type not in ALLOWED_FEATURE_TYPES:
        return [f"{prefix}不支持的特征类型: {feature_type}"]

    params = feature.get("params")
    if not isinstance(params, dict):
        return [f"{prefix}缺失或无效的 params"]

    if feature_type in {"extrude", "cut", "hole", "revolve"}:
        _validate_sketch_feature(feature_type, params, errors)
    elif feature_type == "fillet":
        _number(params, "radius", errors, minimum=0.01)
    elif feature_type == "chamfer":
        _number(params, "distance", errors, minimum=0.01)
    elif feature_type == "linear_pattern":
        _validate_linear_pattern(params, errors)
    elif feature_type == "circular_pattern":
        _validate_circular_pattern(params, errors)
    elif feature_type == "mirror":
        _validate_mirror(params, errors)
    elif feature_type == "shell":
        _validate_shell(params, errors)
    elif feature_type == "draft":
        _validate_draft(params, errors)
    elif feature_type == "sweep":
        _validate_sweep(params, errors)
    elif feature_type == "loft":
        _validate_loft(params, errors)
    elif feature_type == "rib":
        _validate_rib(params, errors)
    elif feature_type == "counterbore":
        _validate_counterbore(params, errors)
    elif feature_type == "countersink":
        _validate_countersink(params, errors)
    elif feature_type == "hole_array":
        _validate_hole_array(params, errors)
    elif feature_type == "note":
        _validate_note(params, errors)
    elif feature_type == "insert_component":
        _validate_insert_component(params, errors)

    return [prefix + error for error in errors]


PLANE_ALIASES = {
    "front": "front",
    "face": "front",
    "前视": "front",
    "前视基准面": "front",
    "top": "top",
    "dessus": "top",
    "上视": "top",
    "上视基准面": "top",
    "right": "right",
    "droite": "right",
    "右视": "right",
    "右视基准面": "right",
}


def _plane_key(value):
    if not isinstance(value, str):
        return None
    return PLANE_ALIASES.get(value.strip().lower())


def _number_value(value):
    return value if _is_number(value) else None


def _find_base_block(features):
    for index, feature in enumerate(features):
        if not isinstance(feature, dict):
            continue
        if str(feature.get("feature_type", "")).strip().lower() != "extrude":
            continue
        params = feature.get("params")
        if not isinstance(params, dict):
            continue
        if str(params.get("shape", "")).strip().lower() != "rectangle":
            continue
        length = _number_value(params.get("length"))
        width = _number_value(params.get("width"))
        depth = _number_value(params.get("depth"))
        if length is None or width is None or depth is None:
            continue
        return index, _plane_key(params.get("plane")), length, width, depth
    return None


def _validate_geometry(features, errors):
    """Check features against the base block so broken geometry is rejected early."""
    base = _find_base_block(features)
    if base is None:
        return

    base_index, base_plane, length, width, depth = base
    min_dim = min(length, width, depth)
    tolerance = 1e-6

    for index, feature in enumerate(features):
        if not isinstance(feature, dict):
            continue
        feature_type = str(feature.get("feature_type", "")).strip().lower()
        params = feature.get("params")
        if not isinstance(params, dict):
            continue
        prefix = f"features[{index}]: "

        hole_types = {"cut", "hole", "counterbore", "countersink", "hole_array"}
        if feature_type in hole_types and index > base_index:
            bounds = _cut_bounds(base_plane, _plane_key(params.get("plane")), length, width, depth)
            diameter = _number_value(
                params.get("counterbore_diameter")
                or params.get("countersink_diameter")
                or params.get("diameter")
            )
            center_x = _number_value(params.get("center_x"))
            center_y = _number_value(params.get("center_y"))
            if bounds and diameter is not None and center_x is not None and center_y is not None:
                half_width, half_height = bounds
                radius = diameter / 2.0
                if (
                    abs(center_x) + radius > half_width + tolerance
                    or abs(center_y) + radius > half_height + tolerance
                ):
                    errors.append(prefix + "孔中心超出基体范围")

        if feature_type == "fillet":
            radius = _number_value(params.get("radius"))
            if radius is not None and radius > min_dim / 2.0 + tolerance:
                errors.append(prefix + "圆角半径超过基体最小尺寸的一半")

        if feature_type == "chamfer":
            distance = _number_value(params.get("distance"))
            if distance is not None and distance > min_dim / 2.0 + tolerance:
                errors.append(prefix + "倒角距离超过基体最小尺寸的一半")

        if feature_type == "shell":
            thickness = _number_value(params.get("thickness"))
            if thickness is not None and thickness * 2.0 >= min_dim - tolerance:
                errors.append(prefix + "抽壳壁厚达到或超过基体最小尺寸的一半")

        if feature_type == "linear_pattern" and base_plane == "front":
            direction = str(params.get("direction", "")).strip().lower()
            count = _number_value(params.get("count"))
            spacing = _number_value(params.get("spacing"))
            if direction in {"x", "y"} and count is not None and spacing is not None:
                extent = (count - 1) * spacing
                limit = length if direction == "x" else width
                if extent > limit + tolerance:
                    errors.append(prefix + "阵列范围超出基体尺寸")


def _cut_bounds(base_plane, cut_plane, length, width, depth):
    if base_plane != "front":
        return None
    if cut_plane == "front":
        return length / 2.0, width / 2.0
    if cut_plane == "top":
        return length / 2.0, depth / 2.0
    if cut_plane == "right":
        return width / 2.0, depth / 2.0
    return None


def validate_feature_tree(model_data):
    """Return a list of validation errors. Empty list means valid."""
    if not isinstance(model_data, dict):
        return ["响应必须是 JSON 对象"]

    features = model_data.get("features")
    if features is not None and model_data.get("feature_type") is not None:
        return ["响应不能同时包含 feature_type 和 features"]
    if features is not None:
        if not isinstance(features, list) or not features:
            return ["features 必须是非空数组"]
        errors = []
        for index, feature in enumerate(features):
            errors.extend(validate_feature(feature, f"features[{index}]: "))
        if not errors:
            _validate_geometry(features, errors)
        return errors

    if model_data.get("feature_type") is not None:
        return validate_feature(model_data)

    return ["响应缺少 feature_type 或 features"]
