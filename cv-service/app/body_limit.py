from collections.abc import Awaitable, Callable
from typing import Any

from starlette.responses import JSONResponse


class RequestBodyLimitMiddleware:
    def __init__(self, app: Callable[..., Awaitable[None]], max_body_bytes: int):
        self.app = app
        self.max_body_bytes = max_body_bytes

    async def __call__(self, scope: dict[str, Any], receive: Callable, send: Callable) -> None:
        if scope["type"] != "http" or scope["path"] != "/analyze":
            await self.app(scope, receive, send)
            return

        headers = dict(scope["headers"])
        content_length = headers.get(b"content-length")
        if content_length is not None:
            try:
                if int(content_length) > self.max_body_bytes:
                    await self._reject(scope, receive, send)
                    return
            except ValueError:
                pass

        received_bytes = 0
        buffered_messages: list[dict[str, Any]] = []
        while True:
            message = await receive()
            if message["type"] == "http.disconnect":
                return
            buffered_messages.append(message)
            received_bytes += len(message.get("body", b""))
            if received_bytes > self.max_body_bytes:
                await self._reject(scope, receive, send)
                return
            if not message.get("more_body", False):
                break

        next_message = 0

        async def replay_receive() -> dict[str, Any]:
            nonlocal next_message
            if next_message < len(buffered_messages):
                message = buffered_messages[next_message]
                next_message += 1
                return message
            return await receive()

        await self.app(scope, replay_receive, send)

    async def _reject(self, scope: dict[str, Any], receive: Callable, send: Callable) -> None:
        response = JSONResponse(
            {"detail": "The upload exceeds the configured request size limit."},
            status_code=413,
        )
        await response(scope, receive, send)
