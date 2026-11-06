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
    errors = validate_feature({"feature_type": "sweep", "params": {}})
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
