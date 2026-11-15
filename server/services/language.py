# Prompt language selection.
# The client may send an explicit language; otherwise the request text is
# classified so the model receives a native prompt instead of a Chinese one.

import re

SUPPORTED_LANGUAGES = {"zh", "en", "fr"}

_LANGUAGE_ALIASES = {
    "zh": "zh",
    "zh-cn": "zh",
    "zh-hans": "zh",
    "chinese": "zh",
    "中文": "zh",
    "en": "en",
    "en-us": "en",
    "en-gb": "en",
    "english": "en",
    "fr": "fr",
    "fr-fr": "fr",
    "french": "fr",
    "français": "fr",
    "francais": "fr",
}

_CJK_PATTERN = re.compile(r"[㐀-䶿一-鿿豈-﫿]")
_FRENCH_CHARS = set("àâçéèêëîïôûùüÿœæ")
_FRENCH_WORDS = (
    " le ",
    " la ",
    " les ",
    " un ",
    " une ",
    " des ",
    " avec ",
    " pour ",
    " dans ",
    " sur ",
    " créer ",
    " percer ",
    " pièce ",
    " plaque ",
    " trou ",
    " trous ",
    " nervure ",
    " rayon ",
    " épaisseur ",
)


def normalize_language(value):
    """Return zh/en/fr for a client value, or None when unsupported."""
    if value is None:
        return None
    key = str(value).strip().lower()
    if not key:
        return None
    if key in SUPPORTED_LANGUAGES:
        return key
    return _LANGUAGE_ALIASES.get(key)


def detect_language(text):
    """Classify a prompt as zh, fr, or en."""
    if not isinstance(text, str) or not text.strip():
        return "en"

    if _CJK_PATTERN.search(text):
        return "zh"

    lowered = " " + text.lower() + " "
    if any(char in _FRENCH_CHARS for char in text):
        return "fr"
    if any(word in lowered for word in _FRENCH_WORDS):
        return "fr"

    return "en"
