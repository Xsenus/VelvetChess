# QA-сборки 1.0.0

Локально проверены два Android-артефакта:

- `VelvetChess-1.0.0-QA-debug-signed.apk` — устанавливаемая QA-сборка;
- `VelvetChess-1.0.0-QA-debug-signed.aab` — проверка AAB-пайплайна.

Контрольные суммы свежей сборки от 14.08.2026:

- APK, 29 795 385 байт — SHA-256 `5528AB57812A0AFC2225C2DA1C0CB7510D2059B5352BB93D37848605A9E2D449`;
- AAB, 29 221 524 байт — SHA-256 `84D9FD65722F23A170084BAE07B1DB793FB59040F6A54B37684E3E0249645651`.

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
