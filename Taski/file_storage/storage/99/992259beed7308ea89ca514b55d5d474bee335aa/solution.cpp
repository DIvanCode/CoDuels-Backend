#include <bits/stdc++.h>
using namespace std;
int main() {
    ios_base::sync_with_stdio(false);
    cin.tie(0);
    string s;
    cin >> s;
    stack<char> st;
    map<char, char> br;
    br[')'] = '(';
    br['}'] = '{';
    br[']'] = '[';
    for (auto x : s) {
	if (br.find(x) == br.end()) {
		st.push(x);
	} else {
		if (st.empty()) {
			cout << "no";
			return 0;
		}
		if (st.top() != br[x]) {
			cout << "no";
			return 0;
		}
		st.pop();
	}
    }
    if (!st.empty()) {
	    cout << "no";
	    return 0;
    }
    cout << "yes";
}
