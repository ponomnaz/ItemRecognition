namespace ItemRecognition.Application.Prompts;

public static class MainObjectsPrompt
{
    public static string Build()
    {
        return string.Join(
            '\n',
            "Ты — система компьютерного зрения. Определи основные предметы на изображении.",
            "Верни строго JSON без пояснений.",
            "Схема ответа:",
            "{",
            "  \"objects\": [",
            "    { \"name\": \"string\", \"isPrimary\": true, \"confidence\": 0.0, \"rank\": 1 }",
            "  ]",
            "}",
            "Правила:",
            "- name: нормализованное русское название предмета.",
            "- isPrimary: true только для основных предметов (обычно 1-3).",
            "- confidence: число от 0 до 1 (можно null).",
            "- rank: порядок важности начиная с 1.",
            "- Обязательно верни хотя бы один объект.",
            "- Никаких дополнительных ключей.",
            "Верни максимум 5 объектов.");
    }
}
