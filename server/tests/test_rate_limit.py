from utils.rate_limit import RateLimiter


def test_old_keys_are_evicted_after_window():
    now = [0.0]
    limiter = RateLimiter(limit=5, window_seconds=60, max_keys=3, time_func=lambda: now[0])

    for index in range(3):
        limiter.is_limited(f"ip-{index}")
        now[0] += 1.0

    now[0] += 61.0
    limiter.is_limited("ip-new")

    assert len(limiter._hits) <= 3
    assert "ip-new" in limiter._hits


def test_prune_removes_oldest_key_when_all_fresh():
    now = [0.0]
    limiter = RateLimiter(limit=5, window_seconds=60, max_keys=3, time_func=lambda: now[0])

    for index in range(3):
        limiter.is_limited(f"ip-{index}")
        now[0] += 1.0

    limiter.is_limited("ip-new")

    assert len(limiter._hits) == 3
    assert "ip-new" in limiter._hits
    assert "ip-0" not in limiter._hits


def test_zero_limit_disables_limiting():
    limiter = RateLimiter(limit=0, window_seconds=60)
    assert not limiter.is_limited("any")


def test_existing_key_is_not_pruned():
    now = [0.0]
    limiter = RateLimiter(limit=5, window_seconds=60, max_keys=2, time_func=lambda: now[0])

    limiter.is_limited("first")
    limiter.is_limited("second")
    assert not limiter.is_limited("first")
    assert len(limiter._hits) == 2
