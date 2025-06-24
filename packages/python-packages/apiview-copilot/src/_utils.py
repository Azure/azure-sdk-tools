def get_language_pretty_name(language: str) -> str:
    """
    Returns a pretty name for the language.
    """
    language_pretty_names = {
        "android": "Android",
        "cpp": "C++",
        "dotnet": "C#",
        "golang": "Go",
        "ios": "Swift",
        "java": "Java",
        "python": "Python",
        "typescript": "TypeScript",
    }
    return language_pretty_names.get(language, language.capitalize())


def get_architects_for_language(language: str) -> list[str]:
    """
    Returns a list of architects for the given language. Pulled from:
    https://github.com/Azure/azure-sdk/blob/main/.github/CODEOWNERS
    """
    language_architects = {
        "android": ["JonathanGiles", "srnagar"],
        "clang": ["JeffreyRichter", "LarryOsterman", "RickWinter"],
        "cpp": ["JeffreyRichter", "LarryOsterman", "RickWinter"],
        "dotnet": ["KrzysztofCwalina", "annelo-msft", "tg-msft"],
        "golang": ["JeffreyRichter", "jhendrixMSFT", "RickWinter"],
        "ios": ["tjprescott"],
        "java": ["JonathanGiles", "srnagar"],
        "python": ["johanste", "annatisch"],
        "rust": ["heaths", "JeffreyRichter", "RickWinter"],
        "typescript": ["bterlson", "xirzec"],
    }
    return language_architects.get(language, [])
