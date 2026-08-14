# Шахматы Velvet

[![CI](https://github.com/Xsenus/VelvetChess/actions/workflows/ci.yml/badge.svg)](https://github.com/Xsenus/VelvetChess/actions/workflows/ci.yml)
[![Privacy policy](https://img.shields.io/badge/privacy-online-D6AE68)](https://xsenus.github.io/VelvetChess/privacy/)

Кроссплатформенная шахматная игра на .NET MAUI для Android, iOS/macOS и Windows. Партия против компьютера и 50 тактических задач полностью доступны офлайн; аккаунт и синхронизация являются опциональными.

[Политика конфиденциальности](https://xsenus.github.io/VelvetChess/privacy/) · [Страница проекта](https://xsenus.github.io/VelvetChess/) · лицензия MIT

## Что уже реализовано

- Полные базовые правила шахмат: легальность хода, шах/мат/пат, рокировка, en passant, превращение, правило 50 ходов и недостаток материала.
- 4 уровня ИИ с разной глубиной, случайностью и лимитом времени.
- Автосохранение и продолжение партии, отмена полного хода и SAN-история.
- Пять наборов фигур и пять цветовых тем доски с живым предпросмотром и сохранением выбора.
- Плавные перемещения фигур, заметное выделение выбранной фигуры, подсветка последнего хода и раздельные маркеры обычных ходов/взятий — всё отключается в настройках.
- Настройки координат, анимации, подсказок ходов, тактильного отклика и подтверждений; безопасный сброс прогресса.
- Быстрая отзывчивая доска на `GraphicsView`, без тяжёлых игровых движков и растровых фигур.
- 50 популярных задач Lichess, отобранных по популярности: пользователь ищет свой ход, ответ соперника выполняется автоматически, а полный вариант доступен отдельно в разборе.
- Гостевой профиль с локальным рейтингом игры и тактики, статистикой побед/ничьих/поражений и попыток решения задач.
- Тёмный адаптивный интерфейс, фирменный арт, иконка и splash screen.
- ASP.NET Core Account API с профилями, рейтингами, переносом гостевой статистики, CORS для будущего веб-клиента и входом Яндекс ID/VK ID через Authorization Code + PKCE без секретов в приложении.
- Тесты ядра и всех 50 решений.
- Материалы, тексты и чек-лист публикации RuStore в `store/rustore`.

## Структура

- `src/VelvetChess.Core` — независимое шахматное ядро, ИИ, задачи и online-контракты.
- `src/VelvetChess.App` — .NET MAUI UI.
- `src/VelvetChess.Server` — общий Account API для мобильного и будущего веб-клиента.
- `tests/VelvetChess.Core.Tests` — автоматические тесты.
- `tests/VelvetChess.Server.Tests` — интеграционные тесты аккаунтов, сессий и рейтинга.
- `tools/PuzzleImporter` — воспроизводимый импорт задач.
- `store/rustore` — карточка, политика, иконка, скриншоты и release checklist.
- `scripts` — подстановка данных владельца, RuStore preflight и безопасная подписанная Android-сборка.
- `docs` — QA, релиз и онлайн-roadmap.

## Быстрый старт

```powershell
dotnet workload install maui-android
dotnet build src/VelvetChess.App/VelvetChess.App.csproj -t:InstallAndroidDependencies -f net9.0-android -p:AcceptAndroidSdkLicenses=True
dotnet restore VelvetChess.sln
dotnet test tests/VelvetChess.Core.Tests -c Release
dotnet build src/VelvetChess.App/VelvetChess.App.csproj -f net9.0-android -c Debug
```

Для iOS нужен Mac с Xcode и workload MAUI iOS. Инструкции, генерация privacy-site и автоматизированная release-подпись Android находятся в `docs/RELEASE.md`; конфигурация Account API — в `docs/SERVER-DEPLOYMENT.md`.

## Важное перед публикацией

Package ID установлен как `ru.velvetchess.game`. До первой публикации подтвердите в RuStore Консоли, что он доступен. Контакты владельца и публичная политика заполнены; постоянный release-keystore создан вне Git. Перед отправкой нужен финальный прогон на реальном Android-устройстве. Секреты и ключи никогда не коммитьте.

## Лицензии данных и арта

Шахматные задачи получены из открытой базы Lichess, опубликованной под CC0. Ссылки на исходные партии сохранены в JSON. Фирменный арт создан специально для проекта с помощью OpenAI ImageGen; промпт сохранён рядом с исходником.
