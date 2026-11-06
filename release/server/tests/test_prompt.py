import re

from prompts.system_prompt import PROMPT_VERSION, SYSTEM_PROMPT


def test_prompt_version_is_current():
    assert PROMPT_VERSION == "2.2.0"


def test_prompt_example_has_four_unique_holes():
    pairs = re.findall(
        r'"center_x":\s*(-?\d+(?:\.\d+)?),\s*"center_y":\s*(-?\d+(?:\.\d+)?)',
        SYSTEM_PROMPT,
    )
    corner_pairs = [pair for pair in pairs if pair[1] in {"40", "-40"}]
    assert len(corner_pairs) == 4
    assert len(set(corner_pairs)) == 4
    assert set(corner_pairs) == {
        ("90", "40"),
        ("90", "-40"),
        ("-90", "40"),
        ("-90", "-40"),
    }


def test_prompt_documents_new_feature_types():
    for feature_type in ("extrude", "cut", "revolve", "fillet", "chamfer"):
        assert feature_type in SYSTEM_PROMPT


def test_prompt_documents_draft_angle():
    assert "draft_angle" in SYSTEM_PROMPT
