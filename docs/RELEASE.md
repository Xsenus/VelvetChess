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

## AAB

```powershell
dotnet publish src/VelvetChess.App/VelvetChess.App.csproj -f net9.0-android -c Release -p:AndroidPackageFormats=aab -p:AndroidKeyStore=true -p:AndroidSigningKeyStore=C:\secure\velvet-release.keystore -p:AndroidSigningKeyAlias=velvet -p:AndroidSigningKeyPass=env:VELVET_KEY_PASS -p:AndroidSigningStorePass=env:VELVET_STORE_PASS
```

Для одновременной QA-сборки APK и AAB в PowerShell используйте экранированный разделитель:

```powershell
dotnet publish src/VelvetChess.App/VelvetChess.App.csproj -f net9.0-android -c Release -p:AndroidPackageFormats=apk%3Baab
```

Секреты не записывайте в `.csproj`, shell history или Git. Перед загрузкой сохраните SHA-256 сертификата и проверьте подпись. Для AAB RuStore требует отдельно добавить подписи в Консоли.
