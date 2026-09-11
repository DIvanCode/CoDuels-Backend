import unittest

from fastapi import HTTPException

from api.auth import require_internal_auth


class InternalAuthTests(unittest.TestCase):
    def test_accepts_exactly_one_matching_header(self) -> None:
        authenticate = require_internal_auth("test-key")

        self.assertIsNone(authenticate(["test-key"]))

    def test_rejects_invalid_headers_and_unconfigured_key(self) -> None:
        cases = [
            ("test-key", None),
            ("test-key", [""]),
            ("test-key", ["wrong-key"]),
            ("test-key", ["test-key", "wrong-key"]),
            ("", ["test-key"]),
        ]

        for expected_key, headers in cases:
            with self.subTest(expected_key=expected_key, headers=headers):
                authenticate = require_internal_auth(expected_key)
                with self.assertRaises(HTTPException) as raised:
                    authenticate(headers)
                self.assertEqual(raised.exception.status_code, 401)


if __name__ == "__main__":
    unittest.main()
