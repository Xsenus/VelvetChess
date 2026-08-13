# QA-сборки 1.0.0

Локально проверены два Android-артефакта:

- `VelvetChess-1.0.0-QA-debug-signed.apk` — устанавливаемая QA-сборка;
- `VelvetChess-1.0.0-QA-debug-signed.aab` — проверка AAB-пайплайна.

Контрольные суммы свежей сборки от 14.08.2026:

- APK, 30 557 720 байт — SHA-256 `42AF25BF95A9A3059E6DE4886B8DBAA4CB54C9F7A45077CCD77E34116A3E9ADF`;
- AAB, 29 980 270 байт — SHA-256 `3E874942C6A3331D81FA1021790AF6DE979777274B69A6A782CDE068D276104B`.

Они подписаны стандартным сертификатом `Android Debug` и **не предназначены для загрузки в RuStore**. Для магазина выполните команду из `docs/RELEASE.md` со своим постоянным release keystore. Один и тот же release-ключ необходимо безопасно хранить для всех будущих обновлений.

Проверенный Android manifest:

- package: `ru.velvetchess.game`;
- versionCode: `1`;
- versionName: `1.0.0`;
- minSdk: `21`;
- targetSdk / compileSdk: `35`;
- чувствительные разрешения отсутствуют.

APK проверен `apksigner`: подписи v1, v2 и v3 валидны. AAB проверен `jarsigner`.
