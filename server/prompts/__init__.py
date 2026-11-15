from .prompt_en import SYSTEM_PROMPT_EN
from .prompt_fr import SYSTEM_PROMPT_FR
from .system_prompt import PROMPT_VERSION
from .system_prompt import SYSTEM_PROMPT as SYSTEM_PROMPT_ZH

SYSTEM_PROMPTS = {
    "zh": SYSTEM_PROMPT_ZH,
    "en": SYSTEM_PROMPT_EN,
    "fr": SYSTEM_PROMPT_FR,
}

DEFAULT_LANGUAGE = "en"


def get_system_prompt(language):
    """Return the system prompt for a normalized language code."""
    key = (language or "").strip().lower()
    return SYSTEM_PROMPTS.get(key, SYSTEM_PROMPTS[DEFAULT_LANGUAGE])


__all__ = [
    "DEFAULT_LANGUAGE",
    "PROMPT_VERSION",
    "SYSTEM_PROMPT_EN",
    "SYSTEM_PROMPT_FR",
    "SYSTEM_PROMPT_ZH",
    "SYSTEM_PROMPTS",
    "get_system_prompt",
]
