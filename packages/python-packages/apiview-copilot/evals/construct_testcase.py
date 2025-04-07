import argparse
import json
import pathlib
import os


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Create a test case for evals.")
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
        "--test-file",
        type=str,
        required=True,
        help="The file path of the JSONL test case. Can be an existing test case file, or will create a new one.",
    )
    parser.add_argument(
        "--test-case",
        type=str,
        required=True,
        help="The name of the test case.",
    )
    parser.add_argument(
        "--overwrite",
        action="store_true",
        help="Overwrite a test case with the same name if it exists in the file.",
    )

    args = parser.parse_args()

    with open(args.apiview_path, "r") as f:
        apiview_contents = f.read()

    with open(args.expected_path, "r") as f:
        expected_contents = json.loads(f.read())

    # TODO this needs to get guidelines context from the cosmos db
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

    test_case = {
        "testcase": args.test_case,
        "query": apiview_contents.replace("\t", ""),
        "language": args.language,
        "context": context,
        "response": json.dumps(expected_contents),
    }

    if os.path.exists(args.test_file):
        if args.overwrite:
            with open(args.test_file, "r") as f:
                existing_test_cases = [json.loads(line) for line in f if line.strip()]
            for existing_test_case in existing_test_cases:
                if existing_test_case["testcase"] == args.test_case:
                    existing_test_cases.remove(existing_test_case)
                    break
            existing_test_cases.append(test_case)
            with open(args.test_file, "w") as f:
                for existing_test_case in existing_test_cases:
                    f.write(json.dumps(existing_test_case) + "\n")
        else:
            with open(args.test_file, "a") as f:
                f.write("\n")
                json.dump(test_case, f)
    else:
        with open(args.test_file, "w") as f:
            json.dump(test_case, f)
