# Privacy site

`index.html` — готовая автономная страница без JavaScript, cookies, аналитики и внешних ресурсов. Перед размещением сгенерируйте её из шаблона:

```powershell
.\scripts\Set-ReleaseOwnerData.ps1 `
  -DeveloperName "Имя разработчика" `
  -SupportEmail "support@example.ru" `
  -WebsiteUrl "https://example.ru" `
  -PrivacyPolicyUrl "https://example.ru/velvet-chess/privacy/"
```

После этого загрузите `index.html` на адрес `PrivacyPolicyUrl` и убедитесь, что он открывается по HTTPS без авторизации и редиректа на вход.
