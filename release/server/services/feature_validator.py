# Validates the JSON feature tree before it reaches SolidWorks.
# Keep this permissive enough for current models but strict on values that
# would produce broken geometry or COM failures.

ALLOWED_FEATURE_TYPES = {"extrude", "cut", "hole", "revolve", "fillet", "chamfer"}
ALLOWED_SHAPES = {"circle", "rectangle", "polygon", "ellipse", "slot"}


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

    return [prefix + error for error in errors]


def validate_feature_tree(model_data):
    """Return a list of validation errors. Empty list means valid."""
    if not isinstance(model_data, dict):
        return ["响应必须是 JSON 对象"]

    features = model_data.get("features")
    if features is not None:
        if not isinstance(features, list) or not features:
            return ["features 必须是非空数组"]
        errors = []
        for index, feature in enumerate(features):
            errors.extend(validate_feature(feature, f"features[{index}]: "))
        return errors

    if model_data.get("feature_type") is not None:
        return validate_feature(model_data)

    return ["响应缺少 feature_type 或 features"]
