from services.feature_validator import validate_feature, validate_feature_tree


def plate_feature():
    return {
        "feature_type": "extrude",
        "name": "Base",
        "params": {
            "plane": "前视基准面",
            "shape": "rectangle",
            "length": 200,
            "width": 100,
            "depth": 20,
        },
    }


def test_valid_single_feature():
    assert validate_feature_tree(plate_feature()) == []


def test_valid_multi_feature_tree():
    tree = {
        "model_name": "Plate",
        "features": [
            plate_feature(),
            {
                "feature_type": "cut",
                "params": {
                    "plane": "前视基准面",
                    "shape": "circle",
                    "diameter": 10,
                    "through_all": True,
                    "center_x": 90,
                    "center_y": 40,
                },
            },
            {"feature_type": "fillet", "params": {"radius": 3}},
            {"feature_type": "chamfer", "params": {"distance": 2}},
        ],
    }
    assert validate_feature_tree(tree) == []


def test_unknown_feature_type():
    errors = validate_feature({"feature_type": "teleport", "params": {}})
    assert any("不支持的特征类型" in error for error in errors)


def test_missing_params():
    errors = validate_feature({"feature_type": "extrude"})
    assert errors


def test_negative_diameter():
    feature = plate_feature()
    feature["params"] = {
        "plane": "前视基准面",
        "shape": "circle",
        "diameter": -1,
        "depth": 20,
    }
    assert any("diameter" in error for error in validate_feature(feature))


def test_cut_through_all_without_depth():
    feature = {
        "feature_type": "cut",
        "params": {
            "plane": "前视基准面",
            "shape": "circle",
            "diameter": 10,
            "through_all": True,
            "center_x": 10,
            "center_y": 10,
        },
    }
    assert validate_feature(feature) == []


def test_cut_blind_requires_depth():
    feature = {
        "feature_type": "cut",
        "params": {
            "plane": "前视基准面",
            "shape": "circle",
            "diameter": 10,
            "center_x": 10,
            "center_y": 10,
        },
    }
    assert any("depth" in error for error in validate_feature(feature))


def test_fillet_requires_radius():
    assert validate_feature({"feature_type": "fillet", "params": {}})


def test_chamfer_requires_distance():
    assert validate_feature({"feature_type": "chamfer", "params": {}})


def test_revolve_profile_must_off_axis():
    feature = {
        "feature_type": "revolve",
        "params": {
            "plane": "前视基准面",
            "shape": "rectangle",
            "length": 50,
            "width": 100,
            "center_x": 0,
            "center_y": 0,
        },
    }
    assert any("旋转轴" in error for error in validate_feature(feature))


def test_valid_revolve():
    feature = {
        "feature_type": "revolve",
        "params": {
            "plane": "前视基准面",
            "shape": "rectangle",
            "length": 50,
            "width": 100,
            "center_x": 25,
            "center_y": 0,
            "axis": "y",
            "angle": 360,
        },
    }
    assert validate_feature(feature) == []


def test_invalid_polygon_sides():
    feature = {
        "feature_type": "extrude",
        "params": {
            "plane": "前视基准面",
            "shape": "polygon",
            "diameter": 50,
            "sides": 2,
            "depth": 20,
        },
    }
    assert any("sides" in error for error in validate_feature(feature))


def test_valid_linear_pattern():
    feature = {
        "feature_type": "linear_pattern",
        "params": {"direction": "x", "count": 3, "spacing": 25},
    }
    assert validate_feature(feature) == []


def test_linear_pattern_requires_count():
    feature = {
        "feature_type": "linear_pattern",
        "params": {"direction": "x", "spacing": 10},
    }
    assert any("count" in error for error in validate_feature(feature))


def test_linear_pattern_second_direction_pair():
    feature = {
        "feature_type": "linear_pattern",
        "params": {"direction": "x", "count": 2, "spacing": 10, "count2": 2},
    }
    errors = validate_feature(feature)
    assert any("count2" in error and "spacing2" in error for error in errors)


def test_valid_circular_pattern():
    feature = {
        "feature_type": "circular_pattern",
        "params": {"axis": "z", "count": 6, "angle": 360},
    }
    assert validate_feature(feature) == []


def test_circular_pattern_angle_range():
    feature = {
        "feature_type": "circular_pattern",
        "params": {"axis": "z", "count": 4, "angle": 400},
    }
    assert any("angle" in error for error in validate_feature(feature))


def test_valid_mirror():
    feature = {"feature_type": "mirror", "params": {"plane": "right"}}
    assert validate_feature(feature) == []


def test_mirror_plane_validation():
    feature = {"feature_type": "mirror", "params": {"plane": "diagonal"}}
    assert any("plane" in error for error in validate_feature(feature))


def test_valid_shell():
    feature = {
        "feature_type": "shell",
        "params": {"thickness": 2, "remove_faces": "top"},
    }
    assert validate_feature(feature) == []


def test_shell_thickness_positive():
    feature = {"feature_type": "shell", "params": {"thickness": 0}}
    assert any("thickness" in error for error in validate_feature(feature))


def test_valid_draft():
    feature = {
        "feature_type": "draft",
        "params": {"angle": 3, "faces": "sides"},
    }
    assert validate_feature(feature) == []


def test_draft_angle_range():
    feature = {"feature_type": "draft", "params": {"angle": 95}}
    assert any("angle" in error for error in validate_feature(feature))


def test_valid_sweep():
    feature = {
        "feature_type": "sweep",
        "params": {
            "diameter": 10,
            "path_plane": "right",
            "path_shape": "arc",
            "path_length": 40,
        },
    }
    assert validate_feature(feature) == []


def test_sweep_requires_path_length():
    feature = {"feature_type": "sweep", "params": {"diameter": 10}}
    assert any("path_length" in error for error in validate_feature(feature))


def test_sweep_requires_diameter():
    feature = {"feature_type": "sweep", "params": {"path_length": 40}}
    assert any("diameter" in error for error in validate_feature(feature))


def test_valid_loft():
    feature = {
        "feature_type": "loft",
        "params": {
            "plane": "前视基准面",
            "height": 60,
            "profile1": {"shape": "circle", "diameter": 40},
            "profile2": {"shape": "circle", "diameter": 20},
        },
    }
    assert validate_feature(feature) == []


def test_loft_requires_profiles():
    feature = {"feature_type": "loft", "params": {"height": 60}}
    errors = validate_feature(feature)
    assert any("profile1" in error for error in errors)
    assert any("profile2" in error for error in errors)


def test_valid_rib():
    feature = {
        "feature_type": "rib",
        "params": {"plane": "上视基准面", "thickness": 3, "length": 30},
    }
    assert validate_feature(feature) == []


def test_rib_thickness_positive():
    feature = {"feature_type": "rib", "params": {"thickness": 0, "length": 30}}
    assert any("thickness" in error for error in validate_feature(feature))


def test_rib_direction_validation():
    feature = {
        "feature_type": "rib",
        "params": {"thickness": 3, "length": 30, "direction": "z"},
    }
    assert any("direction" in error for error in validate_feature(feature))
