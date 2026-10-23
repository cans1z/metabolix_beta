import 'dart:convert';
import 'dart:math';

import 'package:flutter/widgets.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'models.dart';
import 'nutrition.dart';
import 'seed_foods.dart';
import 'util.dart';

/// Всё состояние приложения. Пока сервера нет — хранится локально одним JSON.
class AppState extends ChangeNotifier {
  static const _key = 'metabolix:v1';
  SharedPreferences? _prefs;

  String? userEmail;
  String userName = '';
  bool guest = false;
  Profile? profile;
  List<Food> foods = List.of(seedFoods);
  List<Recipe> recipes = [];
  List<Entry> entries = [];
  List<String> favorites = [];
  List<String> recents = [];
  Map<String, int> water = {}; // дата -> мл
  Map<String, double> weights = {}; // дата -> кг
  Settings settings = Settings();

  bool get authed => userEmail != null;
  Macros get targets => settings.manualTargets ?? autoTargets(profile!, settings);
  List<Entry> entriesOn(String date) => entries.where((e) => e.date == date).toList();

  Future<void> load() async {
    _prefs = await SharedPreferences.getInstance();
    final raw = _prefs!.getString(_key);
    if (raw == null) return;
    try {
      final j = jsonDecode(raw) as Map<String, dynamic>;
      List<T> list<T>(String k, T Function(Map<String, dynamic>) f) =>
          ((j[k] as List?) ?? []).map((e) => f(e as Map<String, dynamic>)).toList();
      userEmail = j['userEmail'] as String?;
      userName = j['userName'] as String? ?? '';
      guest = j['guest'] == true;
      profile = j['profile'] == null ? null : Profile.fromJson(j['profile'] as Map<String, dynamic>);
      foods = list('foods', Food.fromJson);
      if (foods.isEmpty) foods = List.of(seedFoods);
      recipes = list('recipes', Recipe.fromJson);
      entries = list('entries', Entry.fromJson);
      favorites = List<String>.from(j['favorites'] as List? ?? []);
      recents = List<String>.from(j['recents'] as List? ?? []);
      water = (j['water'] as Map<String, dynamic>? ?? {}).map((k, v) => MapEntry(k, (v as num).toInt()));
      weights = (j['weights'] as Map<String, dynamic>? ?? {}).map((k, v) => MapEntry(k, (v as num).toDouble()));
      settings = Settings.fromJson(j['settings'] as Map<String, dynamic>? ?? {});
    } catch (_) {
      // повреждённые данные — начинаем с чистого состояния
    }
  }

  Map<String, dynamic> _toJson() => {
        'userEmail': userEmail,
        'userName': userName,
        'guest': guest,
        'profile': profile?.toJson(),
        'foods': foods.map((e) => e.toJson()).toList(),
        'recipes': recipes.map((e) => e.toJson()).toList(),
        'entries': entries.map((e) => e.toJson()).toList(),
        'favorites': favorites,
        'recents': recents,
        'water': water,
        'weights': weights,
        'settings': settings.toJson(),
      };

  void _commit() {
    notifyListeners();
    _prefs?.setString(_key, jsonEncode(_toJson()));
  }

  // --- вход (имитация, сервера пока нет) ---

  void login(String email, [String name = '']) {
    userEmail = email;
    userName = name.isNotEmpty ? name : (profile?.name.isNotEmpty == true ? profile!.name : email.split('@').first);
    guest = false;
    _commit();
  }

  void loginGuest() {
    guest = true;
    _commit();
  }

  void logout() {
    userEmail = null;
    guest = false;
    _commit();
  }

  // --- профиль и настройки ---

  void saveProfile(Profile p) {
    profile = p;
    weights[todayIso()] = p.weight;
    _commit();
  }

  void updateSettings(void Function(Settings s) change) {
    change(settings);
    _commit();
  }

  // --- дневник ---

  void addEntry(String date, MealType meal, Food food, double grams) {
    entries.add(Entry(id: uid(), date: date, meal: meal, food: food, grams: grams));
    recents = [food.id, ...recents.where((id) => id != food.id)].take(30).toList();
    _commit();
  }

  void updateEntry(String id, double grams) {
    entries = [for (final e in entries) e.id == id ? e.copyWith(grams: grams) : e];
    _commit();
  }

  void removeEntry(String id) {
    entries.removeWhere((e) => e.id == id);
    _commit();
  }

  void copyMeal(String from, MealType meal, String to) {
    final copies = entries
        .where((e) => e.date == from && e.meal == meal)
        .map((e) => e.copyWith(id: uid(), date: to))
        .toList();
    entries.addAll(copies);
    _commit();
  }

  // --- продукты и блюда ---

  void saveFood(Food food) {
    final i = foods.indexWhere((f) => f.id == food.id);
    if (i >= 0) {
      foods[i] = food;
    } else {
      foods.insert(0, food);
    }
    _commit();
  }

  void removeFood(String id) {
    foods.removeWhere((f) => f.id == id);
    favorites.remove(id);
    recents.remove(id);
    _commit();
  }

  void toggleFavorite(String id) {
    if (!favorites.remove(id)) favorites.insert(0, id);
    _commit();
  }

  Food? byBarcode(String code) {
    for (final f in foods) {
      if (f.barcode == code) return f;
    }
    return null;
  }

  void saveRecipe(Recipe r) {
    final i = recipes.indexWhere((x) => x.id == r.id);
    if (i >= 0) {
      recipes[i] = r;
    } else {
      recipes.insert(0, r);
    }
    _commit();
  }

  void removeRecipe(String id) {
    recipes.removeWhere((r) => r.id == id);
    _commit();
  }

  // --- вода и вес ---

  void addWater(String date, int ml) {
    water[date] = max(0, (water[date] ?? 0) + ml);
    _commit();
  }

  void setWeight(String date, double kg) {
    weights[date] = kg;
    final latest = (weights.keys.toList()..sort()).last;
    if (latest == date) profile?.weight = kg;
    _commit();
  }

  void removeWeight(String date) {
    weights.remove(date);
    _commit();
  }

  /// Заполняет две недели дневника и веса случайными данными для демонстрации.
  void seedDemo() {
    final rnd = Random();
    final base = profile?.weight ?? 75;
    for (var i = 13; i >= 0; i--) {
      final date = addDays(todayIso(), -i);
      for (final meal in MealType.values) {
        for (var k = 0; k < (meal == MealType.snack ? 1 : 3); k++) {
          final food = foods[rnd.nextInt(foods.length)];
          entries.add(Entry(id: uid(), date: date, meal: meal, food: food, grams: food.portion ?? 150));
        }
      }
      weights[date] = ((base + i * 0.12 + (rnd.nextDouble() - 0.5) * 0.6) * 10).round() / 10;
      water[date] = 1000 + rnd.nextInt(6) * 250;
    }
    profile?.weight = weights[todayIso()]!;
    _commit();
  }

  void resetAll() {
    userEmail = null;
    userName = '';
    guest = false;
    profile = null;
    foods = List.of(seedFoods);
    recipes = [];
    entries = [];
    favorites = [];
    recents = [];
    water = {};
    weights = {};
    settings = Settings();
    _commit();
  }
}

class AppScope extends InheritedNotifier<AppState> {
  const AppScope({super.key, required AppState state, required super.child}) : super(notifier: state);

  static AppState of(BuildContext context) =>
      context.dependOnInheritedWidgetOfExactType<AppScope>()!.notifier!;
}
