import math
import threading
import time
from collections import deque
from typing import Callable

from fastapi import Request


class RateLimited(Exception):
    def __init__(self, retry_after: int):
        self.retry_after = retry_after


class RateLimiter:
    """In-memory sliding window per client key."""

    def __init__(
        self,
        limit: int,
        window_s: float = 60.0,
        clock: Callable[[], float] | None = None,
    ):
        self.limit = limit
        self.window_s = window_s
        self._clock = clock or time.monotonic
        self._lock = threading.Lock()
        self._hits: dict[str, deque[float]] = {}

    def check(self, key: str) -> None:
        now = self._clock()
        with self._lock:
            # drop stale keys so memory stays bounded
            for k in [k for k, q in self._hits.items() if not q or q[-1] <= now - self.window_s]:
                del self._hits[k]
            q = self._hits.setdefault(key, deque())
            while q and q[0] <= now - self.window_s:
                q.popleft()
            if len(q) >= self.limit:
                retry = max(1, math.ceil(q[0] + self.window_s - now))
                raise RateLimited(retry)
            q.append(now)


def client_key(request: Request) -> str:
    return request.client.host if request.client else "unknown"
