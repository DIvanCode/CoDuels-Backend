from __future__ import annotations

from secrets import compare_digest
from typing import Callable

from fastapi import Header, HTTPException, status


def require_internal_auth(expected_key: str) -> Callable[..., None]:
    def authenticate(
        internal_auth: list[str] | None = Header(default=None, alias="internal-auth"),
    ) -> None:
        if (
            not expected_key
            or internal_auth is None
            or len(internal_auth) != 1
            or not compare_digest(
                internal_auth[0].encode("utf-8"), expected_key.encode("utf-8")
            )
        ):
            raise HTTPException(
                status_code=status.HTTP_401_UNAUTHORIZED,
                detail="Unauthorized",
            )

    return authenticate
