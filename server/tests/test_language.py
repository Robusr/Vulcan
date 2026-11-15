from services.language import detect_language, normalize_language


def test_detect_chinese():
    assert detect_language("在前视基准面拉伸一个圆柱") == "zh"


def test_detect_french():
    assert detect_language("Créez une plaque avec quatre trous") == "fr"
    assert detect_language("Plaque de 200 mm avec des trous") == "fr"


def test_detect_english():
    assert detect_language("Extrude a cylinder on the front plane") == "en"


def test_detect_empty_defaults_to_english():
    assert detect_language("") == "en"
    assert detect_language(None) == "en"


def test_normalize_language_aliases():
    assert normalize_language("zh-CN") == "zh"
    assert normalize_language("English") == "en"
    assert normalize_language("fr-FR") == "fr"
    assert normalize_language("中文") == "zh"


def test_normalize_language_rejects_unknown():
    assert normalize_language("de") is None
    assert normalize_language("") is None
    assert normalize_language(None) is None
