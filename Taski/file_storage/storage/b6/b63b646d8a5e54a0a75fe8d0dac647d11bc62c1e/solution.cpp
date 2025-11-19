#include <iostream>
#include <vector>
#include <algorithm>
#include <cmath>
#include <iomanip>

using namespace std;

int main() {
    int n, x, y;
    cin >> n >> x >> y;
    if (x > y) swap(x, y);
    --n;
    long long l = 0, r = n * x;
    while (r - l > 1) {
        long long mid = (l + r) / 2;
        if (mid / x + mid / y >= n)
            r = mid;
        else
            l = mid;
    }
    cout << r + x;
    return 0;
}
