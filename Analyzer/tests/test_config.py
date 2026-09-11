import os
import unittest
from unittest.mock import patch

from config import DEFAULT_INTERNAL_AUTH_KEY, load_config


class ConfigTests(unittest.TestCase):
    def test_uses_default_internal_auth_key(self) -> None:
        with patch.dict(os.environ, {}, clear=True):
            self.assertEqual(load_config().internal_auth_key, DEFAULT_INTERNAL_AUTH_KEY)

    def test_reads_internal_auth_key_from_environment(self) -> None:
        with patch.dict(os.environ, {"INTERNAL_AUTH_KEY": "environment-key"}, clear=True):
            self.assertEqual(load_config().internal_auth_key, "environment-key")


if __name__ == "__main__":
    unittest.main()
