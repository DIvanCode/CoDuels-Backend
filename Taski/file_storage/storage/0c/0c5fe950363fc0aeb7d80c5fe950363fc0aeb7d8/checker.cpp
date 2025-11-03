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

    while (true) {
        int correct_output;
        bool correct_eol = !(correct >> correct_output);
        int suspect_output;
        bool suspect_eol = !(suspect >> suspect_output);
        if (correct_eol != suspect_eol) {
            cout << "WA";
            return 0;
        }
        if (correct_eol) {
            break;
        }
        if (correct_output != suspect_output) {
            cout << "WA";
            return 0;
        }
    }

    cout << "OK";
    return 0;
}