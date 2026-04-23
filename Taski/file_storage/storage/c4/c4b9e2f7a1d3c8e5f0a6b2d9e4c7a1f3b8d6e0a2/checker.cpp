#include "testlib.h"

int main(int argc, char* argv[]) {
    setName("single integer checker");
    registerTestlibCmd(argc, argv);

    int expected = ans.readInt();
    int found = ouf.readInt();

    if (expected != found) {
        quitf(_wa, "expected %d, found %d", expected, found);
    }

    ouf.readEof();
    quitf(_ok, "answer is %d", expected);
}
