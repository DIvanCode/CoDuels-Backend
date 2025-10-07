#include <iostream>
#include <string>
#include <fstream>

using namespace std;

int main(int argc, const char **argv) {
  const char *fn1 = argv[1];
  const char *fn2 = argv[2];

  std::ifstream file1(fn1, std::ios::binary);
  std::ifstream file2(fn2, std::ios::binary);

  // Compare file sizes first for a quick check
  file1.seekg(0, std::ios::end);
  file2.seekg(0, std::ios::end);
  if (file1.tellg() != file2.tellg()) {
      cout << "WA"; // Files have different sizes
      return 0;
  }
  file1.seekg(0, std::ios::beg);
  file2.seekg(0, std::ios::beg);

  // Compare byte by byte
  char char1, char2;
  while (file1.get(char1) && file2.get(char2)) {
    if (char1 != char2) {
      cout << "WA";
      return 0;
    }
  }

  cout << "OK"; // Files are identical
}
