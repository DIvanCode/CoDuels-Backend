#!/bin/bash

tmpl=''

# jq --arg content '
# #include <iostream>
# using namespace std;
# int main() {
#   cout << "Hello, world!" << endl;
# }
# ' '.steps[0].code.content = $content' <<EOF
# {
#   "steps": [
#     {
#       "name": "step2",
#       "type":"compile_cpp",
#       "code": {
#         "type":"inline"
#       }
#     }
#   ]
# }
# EOF

code=$(cat ./main.cpp)
checker=$(cat ./checker.cpp)
correct=$(cat ./correct.txt)
indata=$(cat ./in.txt)

template='
{
  "steps": [
    {
      "name": "compile_code",
      "type":"compile_cpp",
      "code": {
        "type": "inline",
        "content": $code
      }
    },
    {
      "name": "compile_checker",
      "type": "compile_cpp",
      "code": {
        "type": "inline",
        "content": $checker
      }
    },
    {
      "name": "run_code",
      "type": "run_cpp",
      "run_input": {
        "type": "inline",
        "content": $indata
      },
      "compiled_code": {
        "type": "other_step",
        "step_name": "compile_code"
      }
    },
    {
      "name": "check_code",
      "type": "check_cpp",
      "correct_output": {
        "type": "inline",
        "content": $indata
      },
      "suspect_output": {
        "type": "other_step",
        "step_name": "run_code"
      },
      "compiled_checker": {
        "type": "other_step",
        "step_name": "compile_checker"
      }
    }
  ]
}
'

jq \
  --arg code "$code" \
  --arg checker "$checker" \
  --arg correct "$correct" \
  --arg indata "$indata" \
  -n "$template"
