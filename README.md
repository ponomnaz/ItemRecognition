# ItemRecognition

Backend-сервис для распознавания основных предметов на изображении и определения материалов с логированием AI-вызовов.
Проекты: Api / Application / Domain / Infrastructure / Tests.

## Документация
- `docs/db-schema.mmd` — диаграмма схемы БД (Mermaid ER).
- `docs/architecture.md` — текстовое описание решения, ограничения и промпты в явном виде.

## Требования
- .NET SDK 10 (см. `global.json`).
- Docker Desktop (для локального PostgreSQL).

## Быстрый старт (локально)
1. Запустить PostgreSQL:

```bash
docker compose -f deploy/docker-compose.yml up -d
```

2. Настроить секреты (строка подключения + ключ GigaChat):

```bash
dotnet user-secrets set "ConnectionStrings:Postgres" "Host=localhost;Port=55432;Database=itemrecognition;Username=itemrecognition;Password=itemrecognition_pwd" --project ItemRecognition.Api
dotnet user-secrets set "GigaChat:AuthorizationKey" "<YOUR_GIGACHAT_AUTH_KEY>" --project ItemRecognition.Api
```

3. Сборка и тесты:

```bash
dotnet restore
dotnet build
dotnet test ItemRecognition.Tests/ItemRecognition.Tests.csproj
```

4. Запуск API:

```bash
dotnet run --project ItemRecognition.Api
```

5. Swagger:
- `http://localhost:5077/swagger`
- `https://localhost:7177/swagger`

Порты настраиваются в `ItemRecognition.Api/Properties/launchSettings.json`.

## Конфигурация
Базовые секции настроек:
- `ItemRecognition.Api/appsettings.json`
- `ItemRecognition.Api/appsettings.Development.json`

Секреты в репозиторий не добавляются — используйте User Secrets (см. выше).

## Тестирование (ручное)
Готовые запросы есть в `ItemRecognition.Api/ItemRecognition.Api.http`.

### Тестовые изображения
- https://mossklad.ru/upload/iblock/680/snc00v17ci0ziids6wot990fd42jv0gv/sls_6_locksmith_table_20_03.jpg
- https://www.foroffice.ru/upload/iblock/518/7886.40_2_1000x1000.jpg
- https://upload.wikimedia.org/wikipedia/commons/d/d5/Picture_of_computer_desk.jpg
- https://upload.wikimedia.org/wikipedia/commons/b/b0/50pc_Wrench_Set_Maxtech.jpg
- https://upload.wikimedia.org/wikipedia/commons/7/76/Chair.png
- https://upload.wikimedia.org/wikipedia/commons/3/3e/Notebook_with_pen%2C_August%2C_2019.jpg

### 1) Основные предметы
```bash
curl -X POST "http://localhost:5077/api/recognition" \
  -H "Content-Type: application/json" \
  -d "{\"imageUrl\":\"<IMAGE_URL>\"}"
```

Ожидаемо: `200 OK`, поля `requestId` и `objects`.

### 2) Материалы по подтвержденным предметам
```bash
curl -X POST "http://localhost:5077/api/recognition/<REQUEST_ID>/materials" \
  -H "Content-Type: application/json" \
  -d "{\"items\":[\"<NAME_1>\",\"<NAME_2>\"]}"
```

Ожидаемо: `200 OK`, поля `items` и список материалов.

### 3) Экспорт (анонимизированный)
```bash
curl -X GET "http://localhost:5077/api/exports/anonymized"
```

Проверить, что `imageUrl` и `imageStorageKey` не возвращаются, только `imageHash`.

### 4) Аналитика
```bash
curl -X GET "http://localhost:5077/api/analytics/summary"
```

### Негативные проверки
1. Невалидный URL изображения.
2. Пустой `items` или более 20 элементов.
3. Дубликаты в `items` (case-insensitive).
4. Несуществующий `requestId`.

## Примечания
- EF Core + PostgreSQL.
- Миграции не используются, схема БД уже существует.
