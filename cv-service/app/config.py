import os


def positive_int_environment(name: str, default: int) -> int:
    raw_value = os.getenv(name, str(default))
    try:
        value = int(raw_value)
    except ValueError as exception:
        raise RuntimeError(f"{name} must be a positive integer.") from exception

    if value <= 0:
        raise RuntimeError(f"{name} must be a positive integer.")
    return value
