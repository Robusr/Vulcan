# Robusr Mar.24th
# Business exceptions returned by the LLM pipeline.


class VulcanBaseError(Exception):
    """Base exception for Vulcan server errors."""

    status_code = 500

    def __init__(self, message):
        self.message = message
        super().__init__(self.message)


class APIConnectionError(VulcanBaseError):
    """The upstream LLM provider is unreachable or timed out."""

    status_code = 503


class LLMResponseError(VulcanBaseError):
    """The upstream response could not be parsed or validated."""

    status_code = 500


class ParamValidationError(VulcanBaseError):
    """The request parameters are invalid."""

    status_code = 400
