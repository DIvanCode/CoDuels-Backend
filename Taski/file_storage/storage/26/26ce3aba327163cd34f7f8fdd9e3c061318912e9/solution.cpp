#include <iostream>

using namespace std;

int a[50][50];

int main()
{
    int n, m;
    cin >> n >> m;
    a[0][0] = 1;
    a[2][1] = 1;
    a[1][2] = 1;
    for (int i = 2; i < n; i++){
        for (int j = 2; j < m; j++){
            a[i][j] = a[i - 1][j - 2] + a[i - 2][j - 1];
        }
    }
    cout << a[n - 1][m - 1];
    return 0;
}
