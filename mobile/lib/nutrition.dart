import 'models.dart';
import 'util.dart';

class ActivityLevel {
  final double value;
  final String label, hint;
  const ActivityLevel(this.value, this.label, this.hint);
}

const activityLevels = [
  ActivityLevel(1.2, 'Минимальная', 'сидячая работа, нет тренировок'),
  ActivityLevel(1.375, 'Лёгкая', '1–3 тренировки в неделю'),
  ActivityLevel(1.55, 'Средняя', '3–5 тренировок в неделю'),
  ActivityLevel(1.725, 'Высокая', '6–7 тренировок в неделю'),
  ActivityLevel(1.9, 'Очень высокая', 'физический труд и спорт'),
];

const goalLabels = {'lose': 'Похудение', 'maintain': 'Поддержание', 'gain': 'Набор массы'};

/// Поправка к TDEE по цели: −15% похудение, +10% набор.
const _goalAdj = {'lose': -0.15, 'maintain': 0.0, 'gain': 0.10};

int ageFrom(String birthDate) {
  final b = parseIso(birthDate);
  final now = DateTime.now();
  var age = now.year - b.year;
  if (now.month < b.month || (now.month == b.month && now.day < b.day)) age--;
  return age;
}

/// Основной обмен по формуле Миффлина — Сан Жеора.
double bmr(Profile p) =>
    10 * p.weight + 6.25 * p.height - 5 * ageFrom(p.birthDate) + (p.male ? 5 : -161);

double tdee(Profile p) => bmr(p) * p.activity;

Macros autoTargets(Profile p, Settings s) {
  final kcal = tdee(p) * (1 + (_goalAdj[p.goal] ?? 0));
  return Macros(
    kcal.roundToDouble(),
    (kcal * s.ratioProtein / 100 / 4).roundToDouble(),
    (kcal * s.ratioFat / 100 / 9).roundToDouble(),
    (kcal * s.ratioCarbs / 100 / 4).roundToDouble(),
  );
}

Macros sumEntries(Iterable<Entry> entries) => entries.fold(Macros.zero, (s, e) => s + e.macros);

double bmi(double weight, double height) => weight / ((height / 100) * (height / 100));

String bmiLabel(double v) {
  if (v < 18.5) return 'Дефицит массы';
  if (v < 25) return 'Норма';
  if (v < 30) return 'Избыточная масса';
  return 'Ожирение';
}
