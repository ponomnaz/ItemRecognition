# ItemRecognition

Заготовка backend-сервиса для распознавания предметов на изображении с последующей интеграцией облачного AI (будет добавлено позже).
Решение разделено на проекты: Api / Application / Domain / Infrastructure / Tests.

## Требования
- .NET SDK 10 (см. `global.json`)
- Docker Desktop (для локального PostgreSQL)

## Быстрый старт (локально)
1) Создать локальный файл окружения:
    - скопировать `.env.example` в `.env`
    - при необходимости изменить значения

2) Запустить PostgreSQL:
    - `docker compose up -d`

3) Восстановить зависимости / собрать / прогнать тесты:
    - `dotnet restore`
    - `dotnet build`
    - `dotnet test`

4) Запустить API:
    - `dotnet run --project ItemRecognition.Api`

5) Swagger:
    - открыть `http://localhost:5000/swagger` или `https://localhost:5001/swagger`
      (порты могут отличаться — см. `launchSettings.json`)

## Конфигурация
Базовые секции настроек лежат в:
- `ItemRecognition.Api/appsettings.json`
- `ItemRecognition.Api/appsettings.Development.json`

Секреты (строка подключения, ключ GigaChat) в репозиторий не добавляются.
Используйте `.NET User Secrets`:

```bash
dotnet user-secrets set "ConnectionStrings:Postgres" "Host=localhost;Port=55432;Database=itemrecognition;Username=itemrecognition;Password=itemrecognition_pwd" --project ItemRecognition.Api
dotnet user-secrets set "GigaChat:AuthorizationKey" "<YOUR_GIGACHAT_AUTH_KEY>" --project ItemRecognition.Api
```

Проверка:

```bash
dotnet user-secrets list --project ItemRecognition.Api
```

## Примечания
- Используются EF Core + PostgreSQL.
- EF migrations не создаются (считаем, что схема БД соответствует модели EF).
