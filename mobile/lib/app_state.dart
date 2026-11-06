import 'dart:developer' as developer;

import 'package:flutter/services.dart';
import 'package:flutter/widgets.dart';

import 'api.dart';
import 'models.dart';
import 'nutrition.dart';
import 'util.dart';

/// Состояние экранов в памяти. Данные аккаунта загружаются и меняются через API.
class AppState extends ChangeNotifier {
  final _api = ApiClient();
  bool busy = false;
  bool starting = true;
  String? startupError;
  String? error;
  int errorCount = 0;

  String? userEmail;
  String userName = '';
  bool guest = false;
  Profile? profile;
  List<Recipe> recipes = [];
  List<Entry> entries = [];
  List<Food> favoriteFoods = [];
  Set<String> favorites = {};
  List<Food> recents = [];
  Map<String, int> water = {};
  Map<String, double> weights = {};
  Settings settings = Settings();
  int catalogRevision = 0;

  bool get authed => userEmail != null;
  Macros get targets => settings.manualTargets ?? autoTargets(profile!, settings);
  List<Entry> entriesOn(String date) => entries.where((e) => e.date == date).toList();

  Future<Json> _object(String path) async => await _api.request('GET', path) as Json;

  List<T> _list<T>(dynamic value, T Function(Json) parse) =>
      (value as List).map((item) => parse(item as Json)).toList();

  String _message(Object e, StackTrace stack) {
    if (e is ApiException) return e.message;
    if (e is PlatformException) return e.message ?? 'Не удалось открыть защищённое хранилище';
    developer.log('Ошибка обработки ответа API', stackTrace: stack);
    return 'Не удалось загрузить или сохранить данные';
  }

  Future<T?> _run<T>(Future<T> Function() action) async {
    if (busy) return null;
    busy = true;
    error = null;
    notifyListeners();
    try {
      return await action();
    } catch (e, stack) {
      if (!_api.hasSession && authed) _clearAccount();
      error = _message(e, stack);
      errorCount++;
      return null;
    } finally {
      busy = false;
      notifyListeners();
    }
  }

  Future<bool> _change(Future<void> Function() action) async =>
      await _run(() async {
        await action();
        return true;
      }) ??
      false;

  Future<void> load() async {
    if (busy) return;
    starting = true;
    startupError = null;
    var restored = false;
    final loaded = await _change(() async {
      await _api.restore();
      restored = true;
      if (_api.hasSession) await _loadAccount();
    });
    starting = false;
    if (!loaded && (_api.hasSession || !restored)) startupError = error;
    notifyListeners();
  }

  Future<void> _loadAccount() async {
    final user = await _object('auth/me');
    final result = await Future.wait([
      _object('settings'),
      _object('sync/pull'),
      _object('favorites'),
      _api.request('GET', 'recipes'),
      _api.request('GET', 'foods/recent'),
      if (user['hasProfile'] == true) _object('profile'),
    ]);
    final history = result[1] as Json;
    final savedFavorites = result[2] as Json;
    final loadedEntries = _list(history['diary'], Entry.fromJson);
    final loadedWeights = {
      for (final item in (history['weights'] as List)) item['date'] as String: (item['kg'] as num).toDouble(),
    };
    final loadedWater = {
      for (final item in (history['water'] as List)) item['date'] as String: item['ml'] as int,
    };
    final loadedSettings = Settings.fromJson(result[0] as Json);
    final loadedRecipes = _list(result[3], Recipe.fromJson);
    final loadedRecents = _list(result[4], Food.fromJson);
    final loadedFavorites = _list(savedFavorites['foods'], Food.fromJson);
    final loadedFavoriteRecipes = _list(savedFavorites['recipes'], Recipe.fromJson);
    final loadedProfile = result.length == 6 ? Profile.fromJson(result[5] as Json) : null;

    userEmail = user['email'] as String;
    userName = user['name'] as String;
    guest = false;
    profile = loadedProfile;
    settings = loadedSettings;
    entries = loadedEntries;
    weights = loadedWeights;
    water = loadedWater;
    recipes = loadedRecipes;
    recents = loadedRecents;
    favoriteFoods = loadedFavorites;
    favorites = {
      ...loadedFavorites.map((food) => food.id),
      ...loadedFavoriteRecipes.map((recipe) => 'recipe:${recipe.id}'),
    };
  }

  Future<void> refresh() async {
    if (authed) {
      await _change(() async {
        await _loadAccount();
        catalogRevision++;
      });
    }
  }

  Future<bool> login(String email, String password, {String? name, bool acceptedTerms = false}) =>
      _change(() async {
        final registering = name != null;
        final response = await _api.request(
          'POST',
          registering ? 'auth/register' : 'auth/login',
          auth: false,
          body: {
            'email': email,
            'password': password,
            if (registering) ...{'name': name, 'acceptedTerms': acceptedTerms},
          },
        );
        await _api.saveSession(response as Json);
        try {
          await _loadAccount();
        } catch (e, stack) {
          if (_api.hasSession) startupError = _message(e, stack);
          rethrow;
        }
      });

  Future<bool> forgotPassword(String email) => _change(() async {
        await _api.request('POST', 'auth/forgot', auth: false, body: {'email': email});
      });

  Future<bool> resetPassword(String email, String code, String password) => _change(() async {
        await _api.request('POST', 'auth/reset', auth: false, body: {
          'email': email,
          'code': code,
          'newPassword': password,
        });
      });

  void loginGuest() {
    guest = true;
    notifyListeners();
  }

  Future<bool> logout() => _change(() async {
        try {
          await _api.logout();
        } finally {
          if (!_api.hasSession) _clearAccount();
        }
      });

  Future<bool> deleteAccount(String password) => _change(() async {
        await _api.request('POST', 'auth/delete-account', body: {'password': password});
        await _api.clearSession();
        _clearAccount();
      });

  void _clearAccount() {
    userEmail = null;
    userName = '';
    guest = false;
    profile = null;
    entries = [];
    recipes = [];
    favorites = {};
    favoriteFoods = [];
    recents = [];
    water = {};
    weights = {};
    settings = Settings();
    startupError = null;
    catalogRevision++;
  }

  Future<bool> saveProfile(Profile value) => _change(() async {
        final today = todayIso();
        final result = await _api.request('PUT', 'profile', body: {...value.toJson(), 'today': today});
        profile = Profile.fromJson(result as Json);
        userName = profile!.name;
        weights[today] = value.weight;
      });

  Future<bool> updateSettings(void Function(Settings) change) => _change(() async {
        final next = Settings.fromJson(settings.toJson());
        change(next);
        final result = await _api.request('PUT', 'settings', body: next.toJson());
        settings = Settings.fromJson(result as Json);
      });

  Future<bool> addEntry(String id, String date, MealType meal, Food food, double grams) => _change(() async {
        final result = await _api.request('POST', 'diary', body: {
          'id': id,
          'date': date,
          'meal': meal.name,
          'food': food.toJson(),
          'grams': grams,
        });
        final saved = Entry.fromJson(result as Json);
        if (!entries.any((entry) => entry.id == id)) entries.add(saved);
        _rebuildRecents();
      });

  Future<bool> updateEntry(String id, double grams) => _change(() async {
        final result = await _api.request('PUT', 'diary/$id', body: {'grams': grams});
        final saved = Entry.fromJson(result as Json);
        entries = [for (final entry in entries) entry.id == id ? saved : entry];
        _rebuildRecents();
      });

  Future<bool> removeEntry(String id) => _change(() async {
        await _api.request('DELETE', 'diary/$id');
        entries.removeWhere((entry) => entry.id == id);
        _rebuildRecents();
      });

  void _rebuildRecents() {
    final seen = <String>{};
    final latest = List.of(entries)..sort((a, b) => b.updatedAt.compareTo(a.updatedAt));
    recents = [
      for (final entry in latest.take(300))
        if (seen.add(entry.food.id.isEmpty ? entry.food.name : entry.food.id)) entry.food,
    ].take(30).toList();
  }

  Future<bool> copyMeal(String from, MealType meal, String to) => _change(() async {
        final result = await _api.request('POST', 'diary/copy', body: {
          'from': from,
          'meal': meal.name,
          'to': to,
        });
        final copied = _list(result, Entry.fromJson);
        entries.addAll(copied);
        _rebuildRecents();
      });

  Future<({List<Food> items, bool more})> searchFoods(String query, int page) async {
    final result = await _api.request('GET', 'foods', query: {
      'q': query,
      'page': '$page',
      'pageSize': '30',
    }) as Json;
    return (
      items: _list(result['items'], Food.fromJson),
      more: page < 1000 && page * 30 < (result['total'] as int)
    );
  }

  Future<Food?> byBarcode(String code) => _run<Food?>(() async {
        try {
          final result = await _api.request('GET', 'foods/barcode/$code');
          return Food.fromJson(result as Json);
        } on ApiException catch (e) {
          if (e.status == 404) return null;
          rethrow;
        }
      });

  Future<Food?> saveFood(Food food) => _run(() async {
        final result = await _api.request('POST', 'foods', body: food.toJson());
        catalogRevision++;
        return Food.fromJson(result as Json);
      });

  Future<bool> removeFood(String id) => _change(() async {
        await _api.request('DELETE', 'foods/$id');
        favoriteFoods.removeWhere((food) => food.id == id);
        favorites.remove(id);
        catalogRevision++;
      });

  Future<bool> toggleFavorite(Food food) => _change(() async {
        final id = food.id;
        final path = food.isRecipe ? 'recipes/${id.substring(7)}' : 'foods/$id';
        final removing = favorites.contains(id);
        await _api.request(removing ? 'DELETE' : 'PUT', 'favorites/$path');
        if (removing) {
          favorites.remove(id);
          favoriteFoods.removeWhere((item) => item.id == id);
        } else {
          favorites.add(id);
          if (!food.isRecipe) favoriteFoods.insert(0, food);
        }
      });

  Future<bool> saveRecipe(Recipe recipe) => _change(() async {
        final creating = recipe.id.isEmpty;
        final result = await _api.request(
          creating ? 'POST' : 'PUT',
          creating ? 'recipes' : 'recipes/${recipe.id}',
          body: {'name': recipe.name, 'items': recipe.items.map((item) => item.toJson()).toList()},
        );
        final saved = Recipe.fromJson(result as Json);
        recipes = [saved, ...recipes.where((item) => item.id != saved.id)];
      });

  Future<bool> removeRecipe(String id) => _change(() async {
        await _api.request('DELETE', 'recipes/$id');
        recipes.removeWhere((recipe) => recipe.id == id);
        favorites.remove('recipe:$id');
      });

  Future<bool> addWater(String date, int delta) => _change(() async {
        final amount = ((water[date] ?? 0) + delta).clamp(0, 30000);
        final result = await _api.request('PUT', 'water/$date', body: {'ml': amount}) as Json;
        water[date] = result['ml'] as int;
      });

  Future<bool> setWeight(String date, double kg) => _change(() async {
        await _api.request('PUT', 'weight/$date', body: {'kg': kg});
        weights[date] = kg;
        _updateCurrentWeight();
      });

  Future<bool> removeWeight(String date) => _change(() async {
        await _api.request('DELETE', 'weight/$date');
        weights.remove(date);
        _updateCurrentWeight();
      });

  void _updateCurrentWeight() {
    final latest = (weights.keys.toList()..sort()).lastOrNull;
    if (latest != null) profile?.weight = weights[latest]!;
  }

  Future<String?> exportCsv(String from, String to) =>
      _run(() async => await _api.request('GET', 'export', query: {'from': from, 'to': to}, text: true) as String);
}

class AppScope extends InheritedNotifier<AppState> {
  const AppScope({super.key, required AppState state, required super.child}) : super(notifier: state);

  static AppState of(BuildContext context) => context.dependOnInheritedWidgetOfExactType<AppScope>()!.notifier!;
}
