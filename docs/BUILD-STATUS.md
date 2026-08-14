# Проверенный статус сборки

Дата проверки: 14.08.2026.

## Успешно

- `dotnet test tests/VelvetChess.Core.Tests -c Release`: 31/31 тест.
- Все 50 задач загружаются, и каждый UCI-ход решения легален.
- Эталонный perft начальной позиции на глубине 4: 197281 узел.
- Эталонный Kiwipete perft на глубине 3: 97862 узла; en-passant позиция на глубине 3: 2812 узлов.
- `net9.0-windows10.0.19041.0` Release: успешно, 0 предупреждений, 0 ошибок.
- Windows Release-приложение запускается и остаётся стабильно активным после старта.
- Windows UI Automation открывает маршруты локальной партии, настроек, каталога и первой задачи; на экране задачи доступна кнопка полного решения.
- Скриншоты RuStore: 6 × PNG, 1080×1920, каждый меньше 3 МБ.
- Автоматический RuStore preflight проверяет идентификатор/версию, контакты, графику, тесты и подписи APK/AAB.
- Сквозной dry-run `Publish-AndroidRelease.ps1` проверен во временном чистом клоне с одноразовым release-ключом: созданы валидные APK/AAB, SHA-256 пакетов и отпечаток сертификата; временные ключ и пакеты после проверки удалены.

## Android

- Workload `maui-android` 9.0.111, Microsoft OpenJDK 17 и Android SDK 35 установлены.
- `net9.0-android` Release APK: успешно, 0 предупреждений, 0 ошибок.
- `net9.0-android` Release AAB: успешно.
- Package ID: `ru.velvetchess.game`; versionCode: 1; versionName: 1.0.0; minSdk: 21; targetSdk: 35.
- В манифесте нет доступа к интернету, контактам, файлам, камере, микрофону или геолокации.
- Тестовый APK подписан стандартным debug-сертификатом и предназначен только для установки/QA, не для RuStore.

Для повторной локальной сборки:

```powershell
dotnet workload install maui-android --skip-manifest-update
dotnet build src/VelvetChess.App/VelvetChess.App.csproj -f net9.0-android -c Release
```

Для подписанного AAB дополнительно нужны личный release keystore и пароли владельца; они намеренно не создаются и не хранятся в репозитории. Команда публикации приведена в `docs/RELEASE.md`.
