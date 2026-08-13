# QA-сборки 1.0.0

Локально проверены два Android-артефакта:

- `VelvetChess-1.0.0-QA-debug-signed.apk` — устанавливаемая QA-сборка;
- `VelvetChess-1.0.0-QA-debug-signed.aab` — проверка AAB-пайплайна.

Контрольные суммы свежей сборки от 14.08.2026:

- APK, 29 783 097 байт — SHA-256 `BF9509BA5008E099661246A87C2676DAD0EEB9B88407692B6D5B3E032961F9E9`;
- AAB, 29 208 267 байт — SHA-256 `11277EF39621691A4247FDD32D1F0E967AB39B163F4E7B92931F1ED40FA35252`.

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
