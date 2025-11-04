#include <iostream>
#include <vector>

using namespace std;

int main()
{
    int n;
    cin >> n;
    vector <int> a(n + 1);
    vector <int> d(n + 1);
    for (int i = 1; i <= n; i++){
        cin >> a[i];
    }
    d[0] = 0;
    d[1] = a[1];
    for (int i = 2; i <= n; i++){
        d[i] = min(d[i - 2], d[i - 1]) + a[i];
    }
    cout << d[n];
    return 0;
}
