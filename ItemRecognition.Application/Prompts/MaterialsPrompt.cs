namespace ItemRecognition.Application.Prompts;

public static class MaterialsPrompt
{
    public static string Build(IReadOnlyList<string> itemNames)
    {
        var items = itemNames.Count == 0 ? "[]" : string.Join(", ", itemNames);

        return string.Join(
            '\n',
            "Ты — система компьютерного зрения. Определи материалы указанных предметов на изображении.",
            "Верни строго JSON без пояснений.",
            "Схема ответа:",
            "{",
            "  \"items\": [",
            "    { \"name\": \"string\", \"materials\": [\"string\"] }",
            "  ]",
            "}",
            "Правила:",
            "- name должен совпадать с одним из переданных названий.",
            "- materials: массив материалов, без дубликатов.",
            "- Если материал неизвестен, используй пустой массив.",
            "- Никаких дополнительных ключей.",
            $"Переданные предметы: {items}");
    }
}
