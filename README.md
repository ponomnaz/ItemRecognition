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
Строка подключения хранится в:
- `ItemRecognition.Api/appsettings.json`
- `ItemRecognition.Api/appsettings.Development.json`

Секреты (ключи AI и т.п.) коммитить нельзя.
Использовать .NET User Secrets или переменные окружения.

## Примечания
- Используются EF Core + PostgreSQL.
- EF migrations не создаются (считаем, что схема БД соответствует модели EF).
