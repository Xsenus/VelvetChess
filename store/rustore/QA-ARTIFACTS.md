# QA-сборки 1.0.0

Локально проверены два Android-артефакта:

- `VelvetChess-1.0.0-QA-debug-signed.apk` — устанавливаемая QA-сборка;
- `VelvetChess-1.0.0-QA-debug-signed.aab` — проверка AAB-пайплайна.

Контрольные суммы свежей сборки от 14.08.2026:

- APK, 29 795 385 байт — SHA-256 `1863D801C2E25DCE4D4DCAD1FF1DEFD9BEAC2A3F287D7B8A907A68455797BD60`;
- AAB, 29 221 864 байт — SHA-256 `1FFEF3FA2FF7AF21DF6C286E98BF7560D3D02404777E999D453BB9A7B2A41FE5`.

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
- чувствительные разрешения отсутствуют; `VIBRATE` используется только для отключаемого тактильного отклика.

APK проверен `apksigner`: подписи v1, v2 и v3 валидны. AAB проверен `jarsigner`.
