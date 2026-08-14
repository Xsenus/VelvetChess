# Развёртывание Account API

`VelvetChess.Server` — общий HTTPS API для Android, iOS и будущего веб-клиента. Локальные партии не зависят от его доступности.

## Обязательная production-конфигурация

Секреты задаются переменными окружения хостинга и не добавляются в Git:

```text
ConnectionStrings__Accounts=Data Source=/data/velvetchess-accounts.db
Session__SigningKey=<случайная строка не короче 32 байт>
OAuth__Yandex__ClientId=<выданный client id>
OAuth__Yandex__ClientSecret=<выданный secret>
OAuth__Vk__ClientId=<выданный app id>
OAuth__Vk__ClientSecret=<выданный secret>
Cors__AllowedOrigins__0=https://web.example.ru
```

Каталог `/data` должен быть постоянным volume. При запуске EF Core применяет версионированные миграции SQLite. Перед production рекомендуется настроить резервное копирование базы и заменить SQLite на PostgreSQL при росте нагрузки.

## Docker

Из корня репозитория:

```powershell
docker build -f src/VelvetChess.Server/Dockerfile -t velvetchess-account-api .
docker run --rm -p 8080:8080 --env-file C:\secure\velvetchess-server.env -v C:\secure\velvetchess-data:/data velvetchess-account-api
```

Проверка: `GET http://localhost:8080/health` должна вернуть `{"status":"ok"}`. На публичном сервере обязателен HTTPS reverse proxy.

## Подключение мобильной сборки

Публичный адрес API не является секретом и передаётся при сборке:

```powershell
dotnet publish src/VelvetChess.App/VelvetChess.App.csproj -f net9.0-android -c Release -p:AccountApiBaseUrl=https://api.example.ru/
```

Callback URI для обоих провайдеров: `velvetchess://auth`. Его нужно зарегистрировать в кабинетах Яндекс ID и VK ID. Client secret остаётся только на сервере.

## Границы текущего сервера

Account API уже хранит профиль, тактический рейтинг и статистику, переносит гостевой прогресс, вращает refresh tokens и удаляет аккаунт. Матчмейкинг, проверка сетевых партий, шахматные часы и Glicko-2 относятся к следующему online-сервису; локальные результаты против ИИ не повышают соревновательный online-рейтинг.
