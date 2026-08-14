# QA-сборки 1.0.0

Локально проверены два Android-артефакта:

- `VelvetChess-1.0.0-QA-debug-signed.apk` — устанавливаемая QA-сборка;
- `VelvetChess-1.0.0-QA-debug-signed.aab` — проверка AAB-пайплайна.

Контрольные суммы свежей сборки от 14.08.2026:

- APK, 29 787 193 байт — SHA-256 `2F94B61792583B1112B5F020D37D9F38D8CB077DF1529B103BE1755FF092F424`;
- AAB, 29 213 105 байт — SHA-256 `3208E6AFD7736D167F20E1E9C0A86044384B34B9E912BB4D7806A0FEA7DCB929`.

Они подписаны стандартным сертификатом `Android Debug` и **не предназначены для загрузки в RuStore**. Для магазина выполните команду из `docs/RELEASE.md` со своим постоянным release keystore. Один и тот же release-ключ необходимо безопасно хранить для всех будущих обновлений.

Для проверки этих конкретных QA-пакетов передайте preflight явный флаг `-AllowDebugCertificate`; без него debug-подпись считается ошибкой релиза.

```powershell
.\scripts\Test-RuStoreReadiness.ps1 -AllowOwnerPlaceholders -AllowDebugCertificate -PackagePath C:\path\VelvetChess-1.0.0-QA-debug-signed.apk
```

Проверенный Android manifest:

- package: `ru.velvetchess.game`;
- versionCode: `1`;
- versionName: `1.0.0`;
- minSdk: `21`;
- targetSdk / compileSdk: `35`;
- чувствительные разрешения отсутствуют.

APK проверен `apksigner`: подписи v1, v2 и v3 валидны. AAB проверен `jarsigner`.
