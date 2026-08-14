# Аудит готовности версии 1.0

Дата: 14.08.2026.

## Реализовано и доказано

| Требование | Реализация | Доказательство |
|---|---|---|
| .NET-приложение для Android | .NET MAUI `net9.0-android` | Release APK и AAB собраны без ошибок |
| Основа для iPhone | MAUI iOS target, Info.plist и Apple Privacy Manifest | Исходники готовы; финальная сборка требует macOS/Xcode |
| Локальные шахматы | Полный игровой экран против ИИ | Windows smoke-start; Android Release compile/link/package |
| Несколько сложностей | 4 профиля с глубиной, временем и случайностью | Тест легальности и поиска мата ИИ |
| Правила шахмат | Рокировка, en passant, promotion, шах/мат/пат и ничьи | 31 тест, EP-идентичность повторений, perft 197281/97862/2812 |
| 50 задач и решения | Lichess CC0, подсказки, показ ответа и SAN-варианты | Тест легальности и форматирования каждой полной линии решения |
| Сохранение | Партия, сложность, настройки, статистика и прогресс задач | Независимый `UserStateStore`, тесты повреждённых данных; iOS Privacy Manifest обновлён |
| Производительность | `GraphicsView`, фоновый ИИ, alpha-beta, iterative deepening, bounded cache | Release linking/AOT Android пройдены |
| Графика | Бренд-арт, адаптивная доска, собственная иконка, Noto chess font | Android assets успешно скомпилированы; лицензии сохранены |
| RuStore | Карточка, policy, release notes, checklist, icon и 6 скриншотов | 1080×1920 PNG до 3 МБ; manifest/aapt проверка |
| Git и релиз | `main`, CI, gitignore, безопасная воспроизводимая подпись | Сквозной dry-run release-ключом, preflight APK/AAB и GitHub Actions workflow |
| Будущий онлайн | Транспортный контракт и серверный roadmap | `IOnlineMatchService`, `docs/ONLINE-ROADMAP.md` |

## Требует владельца или внешнего устройства перед отправкой в магазин

Это не может быть безопасно выдумано или выполнено от имени владельца:

1. постоянный release keystore, выбранный и сохранённый владельцем;
2. реальный email поддержки и публичный HTTPS URL политики конфиденциальности;
3. проверка уникальности package ID в личной RuStore Консоли;
4. установка QA APK и финальный проход `docs/QA.md` на реальном Android-устройстве;
5. финальные скриншоты с этого устройства;
6. для iOS — Mac с Xcode и личный Apple Developer signing profile.

QA APK/AAB с debug-подписью намеренно не выдаются за магазинную release-подпись.
