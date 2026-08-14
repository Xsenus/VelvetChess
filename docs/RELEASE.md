# Release Android / RuStore

## Окружение

Нужны .NET 9 SDK, workload `maui-android`, Android SDK 35, Microsoft OpenJDK 17 и release keystore.

```powershell
dotnet workload install maui-android --skip-manifest-update
dotnet build src/VelvetChess.App/VelvetChess.App.csproj -t:InstallAndroidDependencies -f net9.0-android -p:AcceptAndroidSdkLicenses=True
```

## Ключ

Создайте ключ один раз и храните вне репозитория и в резервной копии:

```powershell
keytool -genkeypair -v -keystore velvet-release.keystore -alias velvet -keyalg RSA -keysize 2048 -validity 10000
```

## Автоматическая подписанная сборка

1. Храните keystore и его резервную копию вне репозитория.
2. Создайте вне репозитория два текстовых файла, содержащих только пароль keystore и пароль ключа. Ограничьте доступ к ним средствами ОС и удалите рабочие копии после выпуска.
3. Сгенерируйте синхронизированные контакты и автономный privacy-site:

```powershell
.\scripts\Set-ReleaseOwnerData.ps1 `
  -DeveloperName "Имя разработчика" `
  -SupportEmail "support@example.ru" `
  -WebsiteUrl "https://example.ru" `
  -PrivacyPolicyUrl "https://example.ru/velvet-chess/privacy/"
```

4. Разместите `store/rustore/privacy-site/index.html` по указанному HTTPS-адресу и проверьте его в приватном окне браузера.
5. Запустите подписанную сборку:

```powershell
.\scripts\Publish-AndroidRelease.ps1 `
  -KeyStore C:\secure\velvet-release.keystore `
  -KeyAlias velvet `
  -StorePasswordFile C:\secure\velvet-store-pass.txt `
  -KeyPasswordFile C:\secure\velvet-key-pass.txt
```

Скрипт не принимает пароль как аргумент, запрещает секретные файлы внутри Git-репозитория, выполняет тесты и RuStore preflight, собирает APK/AAB, проверяет подписи и создаёт SHA-256 рядом с пакетами. Результат находится в `artifacts/release/android`.

Отдельная проверка материалов и уже собранного пакета:

```powershell
.\scripts\Test-RuStoreReadiness.ps1 -PackagePath C:\path\VelvetChess-1.0.0-RuStore-signed.aab
```

## Почему используются password-файлы

Префикс `env:` для `AndroidSigningKeyPass` и `AndroidSigningStorePass` официально не поддерживается при формате AAB. Префикс `file:` не раскрывает пароль в командной строке и build log. См. [официальную документацию .NET MAUI](https://learn.microsoft.com/dotnet/maui/android/deployment/publish-cli).

Секреты не записывайте в `.csproj`, shell history или Git. Перед загрузкой сохраните SHA-256 сертификата и пакета. Для AAB RuStore требует отдельно добавить подписи в Консоли.
