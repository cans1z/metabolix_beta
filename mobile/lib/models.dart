enum MealType { breakfast, lunch, dinner, snack }

const mealLabels = {
  MealType.breakfast: 'Завтрак',
  MealType.lunch: 'Обед',
  MealType.dinner: 'Ужин',
  MealType.snack: 'Перекусы',
};

double _d(dynamic v) => (v as num?)?.toDouble() ?? 0;

class Macros {
  final double kcal, protein, fat, carbs;
  const Macros(this.kcal, this.protein, this.fat, this.carbs);
  static const zero = Macros(0, 0, 0, 0);

  Macros operator +(Macros o) => Macros(kcal + o.kcal, protein + o.protein, fat + o.fat, carbs + o.carbs);

  Map<String, dynamic> toJson() => {'kcal': kcal, 'protein': protein, 'fat': fat, 'carbs': carbs};
  factory Macros.fromJson(Map<String, dynamic> j) =>
      Macros(_d(j['kcal']), _d(j['protein']), _d(j['fat']), _d(j['carbs']));
}

/// Карточка продукта. Пищевая ценность — на 100 г / 100 мл.
class Food {
  final String id, name;
  final double kcal, protein, fat, carbs;
  final bool ml;
  final double? portion;
  final String? barcode;
  final bool custom;

  const Food({
    required this.id,
    required this.name,
    required this.kcal,
    required this.protein,
    required this.fat,
    required this.carbs,
    this.ml = false,
    this.portion,
    this.barcode,
    this.custom = false,
  });

  String get unit => ml ? 'мл' : 'г';
  bool get isRecipe => id.startsWith('recipe:');

  Macros macros(double grams) {
    final k = grams / 100;
    return Macros(kcal * k, protein * k, fat * k, carbs * k);
  }

  Map<String, dynamic> toJson() => {
        'id': id,
        'name': name,
        'kcal': kcal,
        'protein': protein,
        'fat': fat,
        'carbs': carbs,
        'ml': ml,
        'portion': portion,
        'barcode': barcode,
        'custom': custom,
      };

  factory Food.fromJson(Map<String, dynamic> j) => Food(
        id: j['id'] as String,
        name: j['name'] as String,
        kcal: _d(j['kcal']),
        protein: _d(j['protein']),
        fat: _d(j['fat']),
        carbs: _d(j['carbs']),
        ml: j['ml'] == true,
        portion: (j['portion'] as num?)?.toDouble(),
        barcode: j['barcode'] as String?,
        custom: j['custom'] == true,
      );
}

class RecipeItem {
  final Food food;
  double grams;
  RecipeItem(this.food, this.grams);

  Map<String, dynamic> toJson() => {'food': food.toJson(), 'grams': grams};
  factory RecipeItem.fromJson(Map<String, dynamic> j) =>
      RecipeItem(Food.fromJson(j['food'] as Map<String, dynamic>), _d(j['grams']));
}

class Recipe {
  final String id;
  String name;
  List<RecipeItem> items;
  Recipe({required this.id, this.name = '', List<RecipeItem>? items}) : items = items ?? [];

  double get weight => items.fold<double>(0, (s, i) => s + i.grams);
  Macros get total => items.fold<Macros>(Macros.zero, (s, i) => s + i.food.macros(i.grams));

  /// Блюдо как продукт: удельная ценность на 100 г, порция — всё блюдо.
  Food toFood() {
    final w = weight == 0 ? 1.0 : weight;
    final t = total;
    double per(double n) => (n / w * 1000).round() / 10;
    return Food(
      id: 'recipe:$id',
      name: name,
      kcal: per(t.kcal),
      protein: per(t.protein),
      fat: per(t.fat),
      carbs: per(t.carbs),
      portion: weight,
    );
  }

  Map<String, dynamic> toJson() => {'id': id, 'name': name, 'items': items.map((i) => i.toJson()).toList()};
  factory Recipe.fromJson(Map<String, dynamic> j) => Recipe(
        id: j['id'] as String,
        name: j['name'] as String,
        items: (j['items'] as List).map((i) => RecipeItem.fromJson(i as Map<String, dynamic>)).toList(),
      );
}

/// Запись дневника. Продукт хранится снимком, чтобы удаление из базы не ломало историю.
class Entry {
  final String id, date;
  final MealType meal;
  final Food food;
  final double grams;
  const Entry({required this.id, required this.date, required this.meal, required this.food, required this.grams});

  Macros get macros => food.macros(grams);
  Entry copyWith({String? id, String? date, double? grams}) =>
      Entry(id: id ?? this.id, date: date ?? this.date, meal: meal, food: food, grams: grams ?? this.grams);

  Map<String, dynamic> toJson() =>
      {'id': id, 'date': date, 'meal': meal.name, 'food': food.toJson(), 'grams': grams};
  factory Entry.fromJson(Map<String, dynamic> j) => Entry(
        id: j['id'] as String,
        date: j['date'] as String,
        meal: MealType.values.byName(j['meal'] as String),
        food: Food.fromJson(j['food'] as Map<String, dynamic>),
        grams: _d(j['grams']),
      );
}

class Profile {
  String name;
  bool male;
  String birthDate; // YYYY-MM-DD
  double height, weight, targetWeight, activity;
  String goal; // lose | maintain | gain

  Profile({
    this.name = '',
    this.male = true,
    this.birthDate = '2000-01-01',
    this.height = 175,
    this.weight = 75,
    this.targetWeight = 70,
    this.activity = 1.375,
    this.goal = 'lose',
  });

  Profile copy() => Profile.fromJson(toJson());

  Map<String, dynamic> toJson() => {
        'name': name,
        'male': male,
        'birthDate': birthDate,
        'height': height,
        'weight': weight,
        'targetWeight': targetWeight,
        'activity': activity,
        'goal': goal,
      };

  factory Profile.fromJson(Map<String, dynamic> j) => Profile(
        name: j['name'] as String? ?? '',
        male: j['male'] != false,
        birthDate: j['birthDate'] as String? ?? '2000-01-01',
        height: _d(j['height']),
        weight: _d(j['weight']),
        targetWeight: _d(j['targetWeight']),
        activity: _d(j['activity']),
        goal: j['goal'] as String? ?? 'maintain',
      );
}

class Settings {
  String theme = 'system'; // system | light | dark
  int ratioProtein = 30, ratioFat = 30;
  Macros? manualTargets;
  int waterGoal = 2000;
  bool remindMeal = true, remindWater = false, remindWeight = false;
  String remindTime = '09:00';

  int get ratioCarbs => 100 - ratioProtein - ratioFat;

  Map<String, dynamic> toJson() => {
        'theme': theme,
        'ratioProtein': ratioProtein,
        'ratioFat': ratioFat,
        'manualTargets': manualTargets?.toJson(),
        'waterGoal': waterGoal,
        'remindMeal': remindMeal,
        'remindWater': remindWater,
        'remindWeight': remindWeight,
        'remindTime': remindTime,
      };

  static Settings fromJson(Map<String, dynamic> j) {
    final s = Settings();
    s.theme = j['theme'] as String? ?? s.theme;
    s.ratioProtein = (j['ratioProtein'] as num?)?.toInt() ?? s.ratioProtein;
    s.ratioFat = (j['ratioFat'] as num?)?.toInt() ?? s.ratioFat;
    final m = j['manualTargets'];
    s.manualTargets = m == null ? null : Macros.fromJson(m as Map<String, dynamic>);
    s.waterGoal = (j['waterGoal'] as num?)?.toInt() ?? s.waterGoal;
    s.remindMeal = j['remindMeal'] as bool? ?? s.remindMeal;
    s.remindWater = j['remindWater'] as bool? ?? s.remindWater;
    s.remindWeight = j['remindWeight'] as bool? ?? s.remindWeight;
    s.remindTime = j['remindTime'] as String? ?? s.remindTime;
    return s;
  }
}
