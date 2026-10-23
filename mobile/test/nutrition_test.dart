import 'package:flutter_test/flutter_test.dart';
import 'package:metabolix/models.dart';
import 'package:metabolix/nutrition.dart';

void main() {
  test('норма считается по Миффлину — Сан Жеору с учётом цели и долей БЖУ', () {
    final birth = DateTime(DateTime.now().year - 30, 1, 1);
    final p = Profile(
      male: true,
      birthDate: '${birth.year}-01-01',
      height: 180,
      weight: 80,
      activity: 1.2,
      goal: 'maintain',
    );
    // 10*80 + 6.25*180 - 5*30 + 5 = 1780
    expect(bmr(p), 1780);
    expect(tdee(p), closeTo(2136, 0.001));

    final t = autoTargets(p, Settings());
    expect(t.kcal, 2136);
    expect(t.protein, (2136 * 0.3 / 4).roundToDouble());
    expect(t.fat, (2136 * 0.3 / 9).roundToDouble());
    expect(t.carbs, (2136 * 0.4 / 4).roundToDouble());
  });

  test('ИМТ и пересчёт порции', () {
    expect(bmi(80, 180), closeTo(24.69, 0.01));
    const food = Food(id: 'x', name: 'x', kcal: 200, protein: 10, fat: 5, carbs: 20);
    expect(food.macros(150).kcal, 300);
  });
}
