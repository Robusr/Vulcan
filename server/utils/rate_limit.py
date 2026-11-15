# Per-IP sliding-window rate limiter.
# This is intentionally in-memory and per-process. Use a shared store such as
# Redis when running multiple gunicorn workers or replicas.

import threading
import time
from collections import deque


class RateLimiter:
    def __init__(
        self,
        limit,
        window_seconds=60,
        max_keys=10000,
        time_func=time.monotonic,
    ):
        self.limit = limit
        self.window_seconds = window_seconds
        self.max_keys = max_keys
        self._time_func = time_func
        self._hits = {}
        self._lock = threading.Lock()

    def is_limited(self, key):
        if self.limit <= 0:
            return False

        now = self._time_func()
        with self._lock:
            if key not in self._hits:
                self._prune(now)
            hits = self._hits.setdefault(key, deque())
            cutoff = now - self.window_seconds
            while hits and hits[0] <= cutoff:
                hits.popleft()

            if len(hits) >= self.limit:
                return True

            hits.append(now)
            return False

    def _prune(self, now):
        """Drop stale keys, then the oldest ones, before adding a new key."""
        if self.max_keys <= 0 or len(self._hits) < self.max_keys:
            return

        cutoff = now - self.window_seconds
        stale = [key for key, hits in self._hits.items() if not hits or hits[-1] <= cutoff]
        for key in stale:
            del self._hits[key]

        while len(self._hits) >= self.max_keys:
            oldest = min(
                self._hits,
                key=lambda key: self._hits[key][-1] if self._hits[key] else float("-inf"),
            )
            del self._hits[oldest]

    def reset(self):
        with self._lock:
            self._hits.clear()
