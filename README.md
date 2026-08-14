# Шахматы Velvet

Кроссплатформенная шахматная игра на .NET MAUI для Android, iOS/macOS и Windows. Версия 1.0 ориентирована на полностью офлайн-режим: партия против компьютера и 50 тактических задач.

## Что уже реализовано

- Полные базовые правила шахмат: легальность хода, шах/мат/пат, рокировка, en passant, превращение, правило 50 ходов и недостаток материала.
- 4 уровня ИИ с разной глубиной, случайностью и лимитом времени.
- Автосохранение и продолжение партии, отмена полного хода и SAN-история.
- Настройки координат, тактильного отклика и подтверждений; безопасный сброс прогресса.
- Отзывчивая доска на `GraphicsView`, без тяжёлых игровых движков.
- 50 популярных задач Lichess, отобранных по популярности, с проверенными решениями в SAN, подсказками, показом ответа, рейтингом и сохранением прогресса.
- Тёмный адаптивный интерфейс, фирменный арт, иконка и splash screen.
- Контракты для будущего серверного онлайн-режима.
- Тесты ядра и всех 50 решений.
- Материалы, тексты и чек-лист публикации RuStore в `store/rustore`.

## Структура

- `src/VelvetChess.Core` — независимое шахматное ядро, ИИ, задачи и online-контракты.
- `src/VelvetChess.App` — .NET MAUI UI.
- `tests/VelvetChess.Core.Tests` — автоматические тесты.
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

Для iOS нужен Mac с Xcode и workload MAUI iOS. Инструкции, генерация privacy-site и автоматизированная release-подпись Android находятся в `docs/RELEASE.md`.

## Важное перед публикацией

Package ID установлен как `ru.velvetchess.game`. До первой публикации подтвердите, что он принадлежит вам и ещё не занят. Заполните контактные `TODO`, создайте собственный release keystore и сделайте финальный прогон на реальном Android-устройстве. Секреты и ключи никогда не коммитьте.

## Лицензии данных и арта

Шахматные задачи получены из открытой базы Lichess, опубликованной под CC0. Ссылки на исходные партии сохранены в JSON. Фирменный арт создан специально для проекта с помощью OpenAI ImageGen; промпт сохранён рядом с исходником.
