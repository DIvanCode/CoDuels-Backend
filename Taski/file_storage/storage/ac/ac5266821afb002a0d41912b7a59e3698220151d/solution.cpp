#include <iostream>
#include <stdio.h>
#include <string>
#include <sstream>
#include <algorithm>
#include <vector>
#include <math.h>
#include <cmath>
using namespace std;
int a[100000], d[400000];
int gcd(int a, int b) {
	if (b == 0) return a;
	return gcd(b, a % b);
}
void build(int i, int l, int r) {
	if (l == r) {
		d[i] = a[l];
		return;
	} 
	build(2 * i, l, (r + l) / 2);
	build(2 * i + 1, (r + l) / 2 + 1, r);
	d[i] = gcd(d[2 * i + 0], d[2 * i + 1]);
}
int nod(int i, int l, int r, int l1, int r1) {
	if (l1 > r1) return 0;
	if (l1 == l && r1 == r) return d[i];
	return gcd(nod(2 * i, l, (l + r) / 2, l1, min(r1, (l + r) / 2)), 
	       nod(2 * i + 1, (l + r) / 2 + 1, r, max(l1, (l + r) / 2 + 1), r1));
}
void update(int i, int l, int r, int p, int f) {
	if (l == r) {
		d[i] = f;
		return;
	}
	if (p <= (l + r) / 2) update (i * 2, l, (l + r) / 2, p, f);
	else update (i * 2 + 1, (l + r) / 2 + 1, r, p, f);
	d[i] = gcd(d[i * 2], d[i * 2 + 1]);
}
int main() {
	int n;
	cin >> n;
	for (int i = 0; i < n; i++) cin >> a[i];
	build(1, 0, n - 1);
	int k;
	cin >> k;
	for (int i =0 ; i < k; i++) {
		int a, b;
		char r;
		cin >> r >> a >> b;
		if (r == 's') cout << nod(1, 0, n - 1, a - 1, b - 1) << " ";
		else update(1, 0, n - 1, a - 1, b);
	}
    return 0;
}  
