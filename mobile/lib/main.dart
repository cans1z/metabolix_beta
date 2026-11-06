import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';

import 'app_state.dart';
import 'screens/auth_screen.dart';
import 'screens/diary_screen.dart';
import 'screens/foods_screen.dart';
import 'screens/profile_screen.dart';
import 'screens/progress_screen.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  final state = AppState();
  runApp(AppScope(state: state, child: const MetabolixApp()));
  await state.load();
}

class MetabolixApp extends StatefulWidget {
  const MetabolixApp({super.key});

  @override
  State<MetabolixApp> createState() => _MetabolixAppState();
}

class _MetabolixAppState extends State<MetabolixApp> {
  static const _seed = Color(0xFF1F9D55);
  final _messenger = GlobalKey<ScaffoldMessengerState>();
  int _lastErrorCount = 0;

  @override
  Widget build(BuildContext context) {
    final app = AppScope.of(context);
    final error = app.error;
    if (error != null && app.errorCount != _lastErrorCount) {
      WidgetsBinding.instance.addPostFrameCallback((_) {
        if (!mounted || app.startupError != null) return;
        _messenger.currentState
          ?..hideCurrentSnackBar()
          ..showSnackBar(SnackBar(content: Text(error)));
      });
    }
    _lastErrorCount = app.errorCount;
    return MaterialApp(
      scaffoldMessengerKey: _messenger,
      title: 'Metabolix',
      debugShowCheckedModeBanner: false,
      theme: ThemeData(colorSchemeSeed: _seed, brightness: Brightness.light, useMaterial3: true),
      darkTheme: ThemeData(colorSchemeSeed: _seed, brightness: Brightness.dark, useMaterial3: true),
      themeMode: switch (app.settings.theme) {
        'light' => ThemeMode.light,
        'dark' => ThemeMode.dark,
        _ => ThemeMode.system,
      },
      locale: const Locale('ru'),
      supportedLocales: const [Locale('ru')],
      localizationsDelegates: GlobalMaterialLocalizations.delegates,
      home: const _Root(),
    );
  }
}

class _Root extends StatelessWidget {
  const _Root();

  @override
  Widget build(BuildContext context) {
    final app = AppScope.of(context);
    if (app.starting || app.startupError != null) {
      return Scaffold(
        body: Center(
          child: app.starting
              ? const CircularProgressIndicator()
              : Padding(
                  padding: const EdgeInsets.all(24),
                  child: Column(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Text(app.startupError!, textAlign: TextAlign.center),
                      FilledButton(onPressed: app.busy ? null : app.load, child: const Text('Повторить')),
                      TextButton(onPressed: app.busy ? null : app.logout, child: const Text('Выйти')),
                    ],
                  ),
                ),
        ),
      );
    }
    if (!app.authed) {
      // Гостю доступен только справочник продуктов.
      return app.guest ? const FoodsScreen() : const AuthScreen();
    }
    if (app.profile == null) return const OnboardingScreen();
    return const _Shell();
  }
}

class _Shell extends StatefulWidget {
  const _Shell();

  @override
  State<_Shell> createState() => _ShellState();
}

class _ShellState extends State<_Shell> with WidgetsBindingObserver {
  int index = 0;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addObserver(this);
  }

  @override
  void dispose() {
    WidgetsBinding.instance.removeObserver(this);
    super.dispose();
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (state == AppLifecycleState.resumed) AppScope.of(context).refresh();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: IndexedStack(
        index: index,
        children: const [DiaryScreen(), FoodsScreen(), ProgressScreen(), ProfileScreen()],
      ),
      bottomNavigationBar: NavigationBar(
        selectedIndex: index,
        onDestinationSelected: (i) => setState(() => index = i),
        destinations: const [
          NavigationDestination(icon: Icon(Icons.menu_book_outlined), label: 'Дневник'),
          NavigationDestination(icon: Icon(Icons.restaurant_outlined), label: 'Продукты'),
          NavigationDestination(icon: Icon(Icons.show_chart), label: 'Прогресс'),
          NavigationDestination(icon: Icon(Icons.person_outline), label: 'Профиль'),
        ],
      ),
    );
  }
}
