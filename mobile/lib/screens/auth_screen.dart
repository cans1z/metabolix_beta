import 'package:flutter/material.dart';

import '../app_state.dart';
import '../models.dart';
import '../widgets/profile_form.dart';

enum _Mode { login, register, forgot }

// Сервера пока нет: вход имитируется локально, пароль никуда не сохраняется.
class AuthScreen extends StatefulWidget {
  const AuthScreen({super.key});

  @override
  State<AuthScreen> createState() => _AuthScreenState();
}

class _AuthScreenState extends State<AuthScreen> {
  _Mode mode = _Mode.login;
  String email = '', password = '', name = '';
  bool sent = false;

  @override
  Widget build(BuildContext context) {
    final app = AppScope.of(context);
    final theme = Theme.of(context);
    final valid = RegExp(r'\S+@\S+\.\S+').hasMatch(email) && (mode == _Mode.forgot || password.length >= 6);
    final title = switch (mode) {
      _Mode.login => 'Вход',
      _Mode.register => 'Регистрация',
      _Mode.forgot => 'Восстановление доступа',
    };
    return Scaffold(
      body: SafeArea(
        child: Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(24),
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 420),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Text('Metabolix',
                      style: theme.textTheme.headlineMedium
                          ?.copyWith(fontWeight: FontWeight.bold, color: theme.colorScheme.primary)),
                  Text('Учёт калорий, белков, жиров и углеводов', style: theme.textTheme.bodyMedium),
                  const SizedBox(height: 28),
                  Text(title, style: theme.textTheme.titleLarge),
                  const SizedBox(height: 14),
                  if (mode == _Mode.register) ...[
                    TextField(
                      decoration: const InputDecoration(labelText: 'Имя', border: OutlineInputBorder()),
                      onChanged: (v) => name = v,
                    ),
                    const SizedBox(height: 12),
                  ],
                  TextField(
                    keyboardType: TextInputType.emailAddress,
                    autocorrect: false,
                    decoration: const InputDecoration(labelText: 'Электронная почта', border: OutlineInputBorder()),
                    onChanged: (v) => setState(() => email = v.trim()),
                  ),
                  if (mode != _Mode.forgot) ...[
                    const SizedBox(height: 12),
                    TextField(
                      obscureText: true,
                      decoration: const InputDecoration(
                        labelText: 'Пароль',
                        helperText: 'Не короче 6 символов',
                        border: OutlineInputBorder(),
                      ),
                      onChanged: (v) => setState(() => password = v),
                    ),
                  ],
                  if (sent && mode == _Mode.forgot)
                    const Padding(
                      padding: EdgeInsets.only(top: 12),
                      child: Text('Если адрес зарегистрирован, письмо уже в пути.'),
                    ),
                  const SizedBox(height: 16),
                  FilledButton(
                    onPressed: !valid
                        ? null
                        : () => mode == _Mode.forgot ? setState(() => sent = true) : app.login(email, name.trim()),
                    child: Text(switch (mode) {
                      _Mode.login => 'Войти',
                      _Mode.register => 'Создать аккаунт',
                      _Mode.forgot => 'Отправить ссылку',
                    }),
                  ),
                  if (mode != _Mode.forgot) ...[
                    const SizedBox(height: 8),
                    OutlinedButton.icon(
                      onPressed: () => app.login('demo@gmail.com', 'Demo'),
                      icon: const Icon(Icons.account_circle_outlined),
                      label: const Text('Войти через Google'),
                    ),
                  ],
                  const SizedBox(height: 8),
                  if (mode == _Mode.login)
                    Row(
                      mainAxisAlignment: MainAxisAlignment.spaceBetween,
                      children: [
                        TextButton(
                            onPressed: () => setState(() => mode = _Mode.register), child: const Text('Регистрация')),
                        TextButton(
                            onPressed: () => setState(() => mode = _Mode.forgot), child: const Text('Забыли пароль?')),
                      ],
                    )
                  else
                    TextButton(
                      onPressed: () => setState(() => mode = _Mode.login),
                      child: const Text('Уже есть аккаунт — войти'),
                    ),
                  TextButton(
                    onPressed: app.loginGuest,
                    child: Text('Посмотреть справочник продуктов без входа',
                        style: TextStyle(color: theme.hintColor), textAlign: TextAlign.center),
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}

class OnboardingScreen extends StatelessWidget {
  const OnboardingScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final app = AppScope.of(context);
    return Scaffold(
      appBar: AppBar(
        title: const Text('Анкета'),
        actions: [TextButton(onPressed: app.logout, child: const Text('Выйти'))],
      ),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          const Text('По этим данным рассчитаем вашу суточную норму КБЖУ. Их можно изменить позже.'),
          const SizedBox(height: 16),
          ProfileForm(
            initial: Profile(name: app.userName),
            submitLabel: 'Рассчитать норму',
            onSubmit: app.saveProfile,
          ),
        ],
      ),
    );
  }
}
