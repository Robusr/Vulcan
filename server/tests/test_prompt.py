import re

from prompts import PROMPT_VERSION, SYSTEM_PROMPTS


def test_prompt_version_is_current():
    assert PROMPT_VERSION == "3.0.0"


def test_prompt_example_has_four_unique_holes():
    for language, prompt in SYSTEM_PROMPTS.items():
        pairs = re.findall(
            r'"center_x":\s*(-?\d+(?:\.\d+)?),\s*"center_y":\s*(-?\d+(?:\.\d+)?)',
            prompt,
        )
        corner_pairs = [pair for pair in pairs if pair[1] in {"40", "-40"}]
        assert len(corner_pairs) == 4, language
        assert len(set(corner_pairs)) == 4, language
        assert set(corner_pairs) == {
            ("90", "40"),
            ("90", "-40"),
            ("-90", "40"),
            ("-90", "-40"),
        }, language


def test_prompt_documents_new_feature_types():
    for language, prompt in SYSTEM_PROMPTS.items():
        for feature_type in ("extrude", "cut", "revolve", "fillet", "chamfer"):
            assert feature_type in prompt, (language, feature_type)


def test_prompt_documents_relational_feature_types():
    for language, prompt in SYSTEM_PROMPTS.items():
        for feature_type in (
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
        ):
            assert feature_type in prompt, (language, feature_type)


def test_prompt_documents_draft_angle():
    for language, prompt in SYSTEM_PROMPTS.items():
        assert "draft_angle" in prompt, language


def test_prompts_are_distinct_languages():
    assert "你是" in SYSTEM_PROMPTS["zh"]
    assert "You are" in SYSTEM_PROMPTS["en"]
    assert "Tu es" in SYSTEM_PROMPTS["fr"]
