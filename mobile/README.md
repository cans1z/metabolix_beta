# Metabolix для Android

Flutter-клиент использует серверный REST API. Данные аккаунта сохраняются в PostgreSQL, а на устройстве сессия хранится в зашифрованном виде с ключом Android Keystore.

1. Запустите `docker compose up --build -d --wait` из корня репозитория.
2. Подключите телефон или запустите Android Emulator.
3. Выполните `flutter run` в папке `mobile`.

Адрес по умолчанию для Android Emulator — `http://10.0.2.2:8080`. Для телефона в той же сети подставьте локальный IP компьютера, где запущен Docker:

```bash
flutter run --dart-define=API_BASE_URL=http://192.168.1.10:8080
```

HTTP разрешён в отладочной сборке. Для release укажите HTTPS-адрес сервера, включая при необходимости префикс, без суффикса `/api`.

```bash
flutter build apk --dart-define=API_BASE_URL=https://metabolix.example.org
```

Для проверки зарегистрируйтесь, заполните анкету и добавьте запись в дневник. Выйдите и войдите снова с той же почтой. Запись должна загрузиться из API. Код восстановления пароля в локальном Docker-окружении виден в Mailpit по адресу http://localhost:8025.
