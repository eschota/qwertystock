"""
Загрузка qwertystock_way.json из корня репозитория (единый источник настроек).
"""
from __future__ import annotations

import json
import os
from typing import Any

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CONFIG_PATH = os.path.join(ROOT, "qwertystock_way.json")

CONFIG: dict[str, Any] = {}


def load_config(path: str | None = None) -> dict[str, Any]:
    global CONFIG
    p = path or CONFIG_PATH
    with open(p, encoding="utf-8") as f:
        CONFIG = json.load(f)
    return CONFIG


def get_config() -> dict[str, Any]:
    if not CONFIG:
        load_config()
    return CONFIG
