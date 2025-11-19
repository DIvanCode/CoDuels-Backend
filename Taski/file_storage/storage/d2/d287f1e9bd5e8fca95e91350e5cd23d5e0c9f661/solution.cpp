#include <iostream>
#include <cmath>
#include <vector>
#include <random>
#include <algorithm>
#include <iomanip>

using namespace std;
#define int long long

signed main() {
    int n, q;
    cin >> n >> q;
    vector<int> a(n);
    for (int i = 0; i < n; i++) {
        cin >> a[i];
    }
    sort(a.begin(), a.end());
    for (int i = 0; i < q; i++) {
        int x;
        cin >> x;
        
        if (*lower_bound(a.begin(), a.end(), x) == x) {
            cout << "YES" << endl;
        } else {
            cout << "NO" << endl;
        }
    }
}
