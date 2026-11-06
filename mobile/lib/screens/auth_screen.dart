import 'package:flutter/material.dart';

import '../app_state.dart';
import '../models.dart';
import '../widgets/profile_form.dart';

enum _Mode { login, register, forgot, reset }

class AuthScreen extends StatefulWidget {
  const AuthScreen({super.key});

  @override
  State<AuthScreen> createState() => _AuthScreenState();
}

class _AuthScreenState extends State<AuthScreen> {
  _Mode mode = _Mode.login;
  String email = '', code = '', name = '';
  final password = TextEditingController();
  bool acceptedTerms = false;

  @override
  void dispose() {
    password.dispose();
    super.dispose();
  }

  void _switchMode(_Mode next) {
    password.clear();
    setState(() {
      mode = next;
      code = '';
      acceptedTerms = false;
    });
  }

  Future<void> _submit(AppState app) async {
    final secret = password.text;
    password.clear();
    setState(() {});
    switch (mode) {
      case _Mode.login:
        await app.login(email, secret);
      case _Mode.register:
        await app.login(email, secret, name: name.trim(), acceptedTerms: acceptedTerms);
      case _Mode.forgot:
        if (await app.forgotPassword(email) && mounted) _switchMode(_Mode.reset);
      case _Mode.reset:
        if (await app.resetPassword(email, code, secret) && mounted) {
          _switchMode(_Mode.login);
          ScaffoldMessenger.of(context).showSnackBar(
            const SnackBar(content: Text('Пароль обновлён')),
          );
        }
    }
  }

  @override
  Widget build(BuildContext context) {
    final app = AppScope.of(context);
    final theme = Theme.of(context);
    final valid = RegExp(r'^\S+@\S+\.\S+$').hasMatch(email) &&
        (mode == _Mode.forgot || password.text.length >= 6) &&
        (mode != _Mode.register || acceptedTerms) &&
        (mode != _Mode.reset || RegExp(r'^\d{6}$').hasMatch(code));
    final title = switch (mode) {
      _Mode.login => 'Вход',
      _Mode.register => 'Регистрация',
      _Mode.forgot => 'Восстановление доступа',
      _Mode.reset => 'Новый пароль',
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
                  if (mode == _Mode.reset) ...[
                    const SizedBox(height: 12),
                    TextField(
                      keyboardType: TextInputType.number,
                      decoration: const InputDecoration(
                        labelText: 'Код из письма',
                        helperText: 'Если адрес зарегистрирован, на него отправлен код',
                        border: OutlineInputBorder(),
                      ),
                      onChanged: (v) => setState(() => code = v.trim()),
                    ),
                  ],
                  if (mode != _Mode.forgot) ...[
                    const SizedBox(height: 12),
                    TextField(
                      controller: password,
                      obscureText: true,
                      decoration: const InputDecoration(
                        labelText: 'Пароль',
                        helperText: 'Не короче 6 символов',
                        border: OutlineInputBorder(),
                      ),
                      onChanged: (_) => setState(() {}),
                    ),
                  ],
                  if (mode == _Mode.register)
                    CheckboxListTile(
                      contentPadding: EdgeInsets.zero,
                      title: const Text('Согласен на обработку персональных данных'),
                      value: acceptedTerms,
                      onChanged: (value) => setState(() => acceptedTerms = value == true),
                    ),
                  const SizedBox(height: 16),
                  FilledButton(
                    onPressed: !valid || app.busy ? null : () => _submit(app),
                    child: Text(app.busy ? 'Подождите…' : switch (mode) {
                      _Mode.login => 'Войти',
                      _Mode.register => 'Создать аккаунт',
                      _Mode.forgot => 'Отправить код',
                      _Mode.reset => 'Сменить пароль',
                    }),
                  ),
                  const SizedBox(height: 8),
                  if (mode == _Mode.login)
                    Row(
                      mainAxisAlignment: MainAxisAlignment.spaceBetween,
                      children: [
                        TextButton(
                            onPressed: app.busy ? null : () => _switchMode(_Mode.register), child: const Text('Регистрация')),
                        TextButton(
                            onPressed: app.busy ? null : () => _switchMode(_Mode.forgot), child: const Text('Забыли пароль?')),
                      ],
                    )
                  else
                    TextButton(
                      onPressed: app.busy ? null : () => _switchMode(_Mode.login),
                      child: const Text('Уже есть аккаунт — войти'),
                    ),
                  TextButton(
                    onPressed: app.busy ? null : app.loginGuest,
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
