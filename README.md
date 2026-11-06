# Metabolix

Дневник питания для подсчёта калорий и БЖУ, учёта воды и веса. Android-приложение на Flutter работает с API на ASP.NET Core и PostgreSQL. Веб-версия на React работает как демо с данными в браузере.

## Сервер и веб

Запусти Docker и выполни из корня проекта.

```bash
docker compose up --build -d --wait
```

- [Веб-версия](http://localhost:8080)
- [Документация API](http://localhost:8080/scalar/v1)
- [Письма для сброса пароля](http://localhost:8025)

Чтобы остановить контейнеры, выполни `docker compose down`.

## Android

Запусти сервер и Android-эмулятор. В папке `mobile` выполни команды.

```bash
flutter pub get
flutter run
```

Для APK на телефоне в той же Wi-Fi сети подставь локальный IP компьютера с Docker.

```bash
flutter build apk --debug --dart-define=API_BASE_URL=http://192.168.1.10:8080
```

Готовый файл будет в `mobile/build/app/outputs/flutter-apk/app-debug.apk`.
