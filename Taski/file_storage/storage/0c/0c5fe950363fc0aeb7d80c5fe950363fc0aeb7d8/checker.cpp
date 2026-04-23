#include "testlib.h"

int main(int argc, char* argv[]) {
    setName("token-by-token checker");
    registerTestlibCmd(argc, argv);

    int n = 0;
    while (!ans.seekEof() && !ouf.seekEof()) {
        n++;
        std::string expected = ans.readWord();
        std::string found = ouf.readWord();
        if (expected != found) {
            quitf(
                _wa,
                "%d%s tokens differ - expected: '%s', found: '%s'",
                n,
                englishEnding(n).c_str(),
                compress(expected).c_str(),
                compress(found).c_str()
            );
        }
    }

    if (!ans.seekEof()) {
        quitf(_wa, "Answer contains extra tokens");
    }
    if (!ouf.seekEof()) {
        quitf(_wa, "Output contains extra tokens");
    }

    quitf(_ok, "%d token(s)", n);
}
