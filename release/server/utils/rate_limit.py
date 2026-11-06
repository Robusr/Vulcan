# Per-IP sliding-window rate limiter.
# This is intentionally in-memory and per-process. Use a shared store such as
# Redis when running multiple gunicorn workers or replicas.

import threading
import time
from collections import deque


class RateLimiter:
    def __init__(self, limit, window_seconds=60, time_func=time.monotonic):
        self.limit = limit
        self.window_seconds = window_seconds
        self._time_func = time_func
        self._hits = {}
        self._lock = threading.Lock()

    def is_limited(self, key):
        if self.limit <= 0:
            return False

        now = self._time_func()
        with self._lock:
            hits = self._hits.setdefault(key, deque())
            cutoff = now - self.window_seconds
            while hits and hits[0] <= cutoff:
                hits.popleft()

            if len(hits) >= self.limit:
                return True

            hits.append(now)
            return False

    def reset(self):
        with self._lock:
            self._hits.clear()
