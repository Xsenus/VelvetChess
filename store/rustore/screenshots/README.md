# Скриншоты Android

Девять PNG сняты непосредственно с Release-конфигурации приложения на чистом Android API 35 emulator с физическим framebuffer 1080×1920. В кадрах сохранены системные status/navigation bars; изображения не являются нарисованными UI-макетами.

Кадр `05_settings_appearance.png` показывает живой предпросмотр выбранных фигур и доски, `08_settings_board_behavior.png` — настройки подсказок и анимации, `09_profile_auth.png` — гостевой режим и подготовленные способы входа.

Воспроизводимый smoke-test:

```powershell
.\scripts\Capture-AndroidStoreScreenshots.ps1 -PackagePath C:\path\VelvetChess.apk
```

Сценарий очищает тестовые данные приложения, проходит ключевые экраны и завершает работу ошибкой при наличии `FATAL EXCEPTION` в `AndroidRuntime`.
