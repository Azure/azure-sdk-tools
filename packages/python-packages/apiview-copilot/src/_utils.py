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
