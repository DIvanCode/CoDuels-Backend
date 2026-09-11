package api

import (
	"crypto/subtle"
	"net/http"
)

const internalAuthHeader = "internal-auth"

func RequireInternalAuth(expectedKey string) func(http.Handler) http.Handler {
	return func(next http.Handler) http.Handler {
		return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
			keys := r.Header.Values(internalAuthHeader)
			if expectedKey == "" || len(keys) != 1 ||
				subtle.ConstantTimeCompare([]byte(keys[0]), []byte(expectedKey)) != 1 {
				http.Error(w, "Unauthorized", http.StatusUnauthorized)
				return
			}

			next.ServeHTTP(w, r)
		})
	}
}
