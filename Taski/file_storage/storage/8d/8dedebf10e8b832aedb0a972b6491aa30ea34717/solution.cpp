#include <bits/stdc++.h>

using namespace std;

int main() {
    int n, k;
    cin >> n >> k;
    vector<int> a(n);
    for (int i = 0; i < n; i++) {
        cin >> a[i];
    }
    set<pair<int, int>> s;
    for (int i = 0; i < k; i++) {
        s.insert({a[i], i});
    }
    for (int i = 0; i < n - k + 1; i++) {
        int ans = (*s.begin()).first;
        s.erase({a[i], i});
        if (i + k < n)
            s.insert({a[i + k], i + k});
        cout << ans << " " ;
    }
}
