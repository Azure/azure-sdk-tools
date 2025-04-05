import argparse
import json
import pathlib
import os


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Create a test case for the given function.")
    parser.add_argument(
        "--apiview-path",
        type=str,
        required=True,
        help="The path to the txt file containing the APIview text",
    )
    parser.add_argument(
        "--language",
        type=str,
        required=True,
        help="The language for the test case.",
    )
    parser.add_argument(
        "--expected-path",
        type=str,
        required=True,
        help="The expected JSON output from the AI reviewer.",
    )
    parser.add_argument(
        "--file-path",
        type=str,
        required=True,
        help="The file path of the test case. Can be an existing test case file, or will create a new one.",
    )
    parser.add_argument(
        "--name",
        type=str,
        required=True,
        help="The name of the test case.",
    )

    args = parser.parse_args()

    with open(args.apiview_path, "r") as f:
        apiview_contents = f.read()

    with open(args.expected_path, "r") as f:
        expected_contents = json.loads(f.read())

    # add context to the testcase based on the rule_ids in expected_contents and corresponding text from guidelines.json
    guidelines_path = pathlib.Path(__file__).parent.parent / "guidelines" / args.language / "guidelines.json"
    with open(str(guidelines_path), "r") as f:
        guidelines = json.loads(f.read())

    context = ""
    for violation in expected_contents["violations"]:
        for rule_id in violation["rule_ids"]:
            for rule in guidelines:
                if rule["id"] == rule_id:
                    if rule["text"] not in context:
                        context += f"\n{rule['text']}"

    test_case = {"testcase": args.name, "query": apiview_contents.replace("\t", ""), "language": args.language, "context": context, "response": json.dumps(expected_contents)}

    if os.path.exists(args.file_path):
        with open(args.file_path, "a") as f:
            f.write("\n")
            json.dump(test_case, f)
    else:
        with open(args.file_path, "w") as f:
            json.dump(test_case, f)
