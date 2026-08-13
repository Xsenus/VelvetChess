# Release Android / RuStore

## Окружение

Нужны .NET 9 SDK, workload `maui-android`, Android SDK/JDK и release keystore.

## Ключ

Создайте ключ один раз и храните вне репозитория и в резервной копии:

```powershell
keytool -genkeypair -v -keystore velvet-release.keystore -alias velvet -keyalg RSA -keysize 2048 -validity 10000
```

## AAB

```powershell
dotnet publish src/VelvetChess.App/VelvetChess.App.csproj -f net9.0-android -c Release -p:AndroidPackageFormat=aab -p:AndroidKeyStore=true -p:AndroidSigningKeyStore=C:\secure\velvet-release.keystore -p:AndroidSigningKeyAlias=velvet -p:AndroidSigningKeyPass=env:VELVET_KEY_PASS -p:AndroidSigningStorePass=env:VELVET_STORE_PASS
```

Секреты не записывайте в `.csproj`, shell history или Git. Перед загрузкой сохраните SHA-256 сертификата и проверьте подпись. Для AAB RuStore требует отдельно добавить подписи в Консоли.
