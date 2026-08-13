# Проверенный статус сборки

Дата проверки: 13.08.2026.

## Успешно

- `dotnet test tests/VelvetChess.Core.Tests -c Release`: 9/9 тестов.
- Все 50 задач загружаются, и каждый UCI-ход решения легален.
- Эталонный perft начальной позиции на глубине 3: 8902 узла.
- `net9.0-windows10.0.19041.0` Release: успешно, 0 предупреждений, 0 ошибок.
- Windows Release-приложение запускается и остаётся стабильно активным после старта.
- Скриншоты RuStore: 4 × PNG, 1080×1920, каждый меньше 3 МБ.

## Android toolchain на текущей машине

Установка `maui-android` дошла до Android SDK/runtime пакетов, но Windows Installer завершил пакет `Microsoft.NETCore.App.Runtime.AOT.win-x64.Cross.android-arm64` с кодом `0x643` и выполнил откат. На диске C: на момент проверки оставалось около 4,1 ГиБ — этого недостаточно для надёжной установки полного Android workload и SDK.

После освобождения не менее 10–15 ГиБ:

```powershell
dotnet workload install maui-android --skip-manifest-update
dotnet build src/VelvetChess.App/VelvetChess.App.csproj -f net9.0-android -c Release
```

Для подписанного AAB дополнительно нужны личный release keystore и пароли владельца; они намеренно не создаются и не хранятся в репозитории. Команда публикации приведена в `docs/RELEASE.md`.
