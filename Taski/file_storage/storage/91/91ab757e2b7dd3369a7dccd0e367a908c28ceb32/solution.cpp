#include <iostream>
#include <cmath>

using namespace std;

long long maxDiplomas(long long w, long long h, long long size) {
    long long amHor = 0, amVer = 0;
    amHor = size / w;
    amVer = size / h;
    return amVer * amHor;
}

int main()
{
    long long w, h, n;
    cin >> w >> h >> n;
    long long r = min(w, h) * n, l = max(w, h) - 1, m;
    while (r - l > 1) {
        m = (r + l) / 2;
        if (maxDiplomas(w, h, m) < n) {
            l = m;
        }
        else {
            r = m;
        }
    }
    if ((h != 1) || (w != 1)) {
        cout << r;
    }
    else {
        cout << ceil(sqrt(n));
    }
    return 0;
}
