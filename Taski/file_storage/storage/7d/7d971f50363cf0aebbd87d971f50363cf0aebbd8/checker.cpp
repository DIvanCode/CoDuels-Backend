#include<bits/stdc++.h>

using namespace std;

int main(int argv, char **argc) {
    if (argv != 3) {
        return -1;
    }

    string correct_output_file = argc[1];
    string suspect_output_file = argc[2];

    ifstream correct(correct_output_file);
    ifstream suspect(suspect_output_file);

    int correct_output;
    correct >> correct_output;
    int suspect_output;
    suspect >> suspect_output;

    if (correct_output == suspect_output) {
        cout << "OK";
    } else {
        cout << "WA";
    }

    return 0;
}