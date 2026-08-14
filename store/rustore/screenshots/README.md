# Скриншоты Android

Восемь PNG сняты непосредственно с подписанной Release-сборки приложения на чистом Android API 35 emulator с физическим framebuffer 1080×1920. Автоматическая обработка очищает только фиксированные области системных status/navigation bars цветом фона приложения; интерфейс не обрезается, не масштабируется и не дорисовывается. Изображения не являются UI-макетами.

Кадр `05_settings_appearance.png` показывает живой предпросмотр выбранных фигур и доски, `08_settings_board_behavior.png` — настройки подсказок и анимации, `07_profile.png` — локальные рейтинг и статистику.

Воспроизводимый smoke-test:

```powershell
.\scripts\Capture-AndroidStoreScreenshots.ps1 -PackagePath C:\path\VelvetChess.apk
```

Сценарий очищает тестовые данные приложения, проходит ключевые экраны и завершает работу ошибкой при наличии `FATAL EXCEPTION` в `AndroidRuntime`.
