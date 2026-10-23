import 'package:flutter/material.dart';

import '../app_state.dart';
import '../models.dart';
import '../util.dart';
import 'common.dart';

String foodSubtitle(Food f) =>
    '${f0(f.kcal)} ккал · Б ${f1(f.protein)} · Ж ${f1(f.fat)} · У ${f1(f.carbs)} · на 100 ${f.unit}';

/// Поиск по базе с вкладками «недавние / избранное / блюда».
/// Родитель должен ограничить высоту (список занимает всё свободное место).
class FoodPicker extends StatefulWidget {
  final ValueChanged<Food> onPick;
  final ValueChanged<String>? onEditRecipe;
  const FoodPicker({super.key, required this.onPick, this.onEditRecipe});

  @override
  State<FoodPicker> createState() => _FoodPickerState();
}

class _FoodPickerState extends State<FoodPicker> {
  static const _tabs = ['Все', 'Недавние', 'Избранное', 'Мои блюда'];
  int tab = 0;
  String query = '';

  @override
  Widget build(BuildContext context) {
    final app = AppScope.of(context);
    final recipes = app.recipes.map((r) => r.toFood()).toList();
    final byId = {for (final f in app.foods) f.id: f, for (final f in recipes) f.id: f};
    List<Food> ids(List<String> xs) => [for (final id in xs) if (byId[id] != null) byId[id]!];
    final source = switch (tab) {
      0 => app.foods,
      1 => ids(app.recents),
      2 => ids(app.favorites),
      _ => recipes,
    };
    final q = query.trim().toLowerCase();
    final list = q.isEmpty ? source : source.where((f) => f.name.toLowerCase().contains(q)).toList();

    return Column(
      children: [
        Padding(
          padding: const EdgeInsets.fromLTRB(12, 8, 12, 4),
          child: TextField(
            decoration: const InputDecoration(
              hintText: 'Поиск продукта…',
              prefixIcon: Icon(Icons.search),
              border: OutlineInputBorder(),
              isDense: true,
            ),
            onChanged: (v) => setState(() => query = v),
          ),
        ),
        SingleChildScrollView(
          scrollDirection: Axis.horizontal,
          padding: const EdgeInsets.symmetric(horizontal: 12),
          child: Row(
            children: [
              for (var i = 0; i < _tabs.length; i++)
                Padding(
                  padding: const EdgeInsets.only(right: 6),
                  child: ChoiceChip(
                    label: Text(_tabs[i]),
                    selected: tab == i,
                    onSelected: (_) => setState(() => tab = i),
                  ),
                ),
            ],
          ),
        ),
        Expanded(
          child: list.isEmpty
              ? const Center(child: Text('Ничего не найдено'))
              : ListView.builder(
                  itemCount: list.length,
                  itemBuilder: (context, i) {
                    final f = list[i];
                    final fav = app.favorites.contains(f.id);
                    return ListTile(
                      title: Text(f.name),
                      subtitle: Text(foodSubtitle(f)),
                      onTap: () => widget.onPick(f),
                      trailing: Row(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          if (f.isRecipe && widget.onEditRecipe != null)
                            IconButton(
                              icon: const Icon(Icons.edit_outlined),
                              onPressed: () => widget.onEditRecipe!(f.id.substring(7)),
                            ),
                          IconButton(
                            icon: Icon(fav ? Icons.star : Icons.star_border, color: fav ? fatColor : null),
                            onPressed: () => app.toggleFavorite(f.id),
                          ),
                        ],
                      ),
                    );
                  },
                ),
        ),
      ],
    );
  }
}

Future<Food?> showFoodPicker(BuildContext context, {String title = 'Выберите продукт'}) {
  return showModalBottomSheet<Food>(
    context: context,
    isScrollControlled: true,
    useSafeArea: true,
    showDragHandle: true,
    builder: (context) => Padding(
      padding: EdgeInsets.only(bottom: MediaQuery.of(context).viewInsets.bottom),
      child: SizedBox(
        height: MediaQuery.of(context).size.height * 0.75,
        child: Column(
          children: [
            Text(title, style: Theme.of(context).textTheme.titleMedium),
            Expanded(child: FoodPicker(onPick: (f) => Navigator.pop(context, f))),
          ],
        ),
      ),
    ),
  );
}

/// Ввод количества: граммы или стандартные порции, с пересчётом КБЖУ.
class AmountPicker extends StatefulWidget {
  final Food food;
  final double grams;
  final ValueChanged<double> onChanged;
  const AmountPicker({super.key, required this.food, required this.grams, required this.onChanged});

  @override
  State<AmountPicker> createState() => _AmountPickerState();
}

class _AmountPickerState extends State<AmountPicker> {
  bool portions = false;

  @override
  Widget build(BuildContext context) {
    final food = widget.food;
    final portion = food.portion;
    final m = food.macros(widget.grams);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        if (portion != null) ...[
          SegmentedButton<bool>(
            showSelectedIcon: false,
            segments: [
              ButtonSegment(value: false, label: Text(food.ml ? 'Миллилитры' : 'Граммы')),
              ButtonSegment(value: true, label: Text('Порции (${f0(portion)} ${food.unit})')),
            ],
            selected: {portions},
            onSelectionChanged: (s) => setState(() => portions = s.first),
          ),
          const SizedBox(height: 10),
        ],
        if (portions && portion != null)
          NumberStepper(
            value: widget.grams / portion,
            min: 0.5,
            max: 20,
            step: 0.5,
            unit: 'порц.',
            onChanged: (v) => widget.onChanged((v * portion).roundToDouble()),
          )
        else
          NumberStepper(
            value: widget.grams,
            min: 5,
            max: 1000,
            step: 5,
            unit: food.unit,
            slider: true,
            onChanged: widget.onChanged,
          ),
        const SizedBox(height: 6),
        Row(
          children: [
            StatTile(f0(m.kcal), 'ккал'),
            StatTile(f1(m.protein), 'белки'),
            StatTile(f1(m.fat), 'жиры'),
            StatTile(f1(m.carbs), 'углеводы'),
          ],
        ),
      ],
    );
  }
}

/// Карточка продукта с добавлением в дневник.
Future<void> showFoodCard(BuildContext context, Food food, {String? date, MealType? meal}) {
  return showModalBottomSheet<void>(
    context: context,
    isScrollControlled: true,
    useSafeArea: true,
    showDragHandle: true,
    builder: (context) => _FoodCard(food: food, date: date ?? todayIso(), meal: meal),
  );
}

class _FoodCard extends StatefulWidget {
  final Food food;
  final String date;
  final MealType? meal;
  const _FoodCard({required this.food, required this.date, this.meal});

  @override
  State<_FoodCard> createState() => _FoodCardState();
}

class _FoodCardState extends State<_FoodCard> {
  late double grams = widget.food.portion ?? 100;
  late MealType meal = widget.meal ?? MealType.breakfast;

  @override
  Widget build(BuildContext context) {
    final app = AppScope.of(context);
    final food = widget.food;
    final canLog = app.authed && app.profile != null;
    return SingleChildScrollView(
      padding: const EdgeInsets.fromLTRB(16, 0, 16, 20),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        mainAxisSize: MainAxisSize.min,
        children: [
          Text(food.name, style: Theme.of(context).textTheme.titleLarge),
          Text(
            foodSubtitle(food) + (food.barcode != null ? '\nштрихкод ${food.barcode}' : ''),
            style: Theme.of(context).textTheme.bodySmall,
          ),
          const SizedBox(height: 14),
          if (!canLog)
            const Padding(
              padding: EdgeInsets.all(16),
              child: Text('Войдите в аккаунт, чтобы добавлять продукты в дневник.', textAlign: TextAlign.center),
            )
          else ...[
            AmountPicker(food: food, grams: grams, onChanged: (g) => setState(() => grams = g)),
            const FieldLabel('Приём пищи'),
            Wrap(
              spacing: 6,
              children: [
                for (final m in MealType.values)
                  ChoiceChip(
                    label: Text(mealLabels[m]!),
                    selected: meal == m,
                    onSelected: (_) => setState(() => meal = m),
                  ),
              ],
            ),
            const SizedBox(height: 14),
            FilledButton(
              onPressed: () {
                app.addEntry(widget.date, meal, food, grams);
                Navigator.pop(context);
              },
              child: const Text('Добавить в дневник'),
            ),
            if (food.custom)
              TextButton(
                onPressed: () {
                  app.removeFood(food.id);
                  Navigator.pop(context);
                },
                child: Text('Удалить продукт', style: TextStyle(color: Theme.of(context).colorScheme.error)),
              ),
          ],
        ],
      ),
    );
  }
}

/// Создание карточки продукта вручную (в т.ч. когда штрихкод не найден).
class FoodFormScreen extends StatefulWidget {
  final String? barcode;
  const FoodFormScreen({super.key, this.barcode});

  @override
  State<FoodFormScreen> createState() => _FoodFormScreenState();
}

class _FoodFormScreenState extends State<FoodFormScreen> {
  String name = '', kcal = '', protein = '', fat = '', carbs = '', portion = '';
  late String barcode = widget.barcode ?? '';
  bool ml = false;

  Widget _num(String label, ValueChanged<String> onChanged) => Expanded(
        child: Padding(
          padding: const EdgeInsets.all(3),
          child: TextField(
            keyboardType: const TextInputType.numberWithOptions(decimal: true),
            decoration: InputDecoration(labelText: label, border: const OutlineInputBorder(), isDense: true),
            onChanged: (v) => setState(() => onChanged(v)),
          ),
        ),
      );

  @override
  Widget build(BuildContext context) {
    final valid = name.trim().isNotEmpty && kcal.trim().isNotEmpty;
    return Scaffold(
      appBar: AppBar(title: const Text('Новый продукт')),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          TextField(
            autofocus: true,
            decoration: const InputDecoration(labelText: 'Название', border: OutlineInputBorder()),
            onChanged: (v) => setState(() => name = v),
          ),
          const FieldLabel('Пищевая ценность'),
          SegmentedButton<bool>(
            showSelectedIcon: false,
            segments: const [
              ButtonSegment(value: false, label: Text('на 100 г')),
              ButtonSegment(value: true, label: Text('на 100 мл')),
            ],
            selected: {ml},
            onSelectionChanged: (s) => setState(() => ml = s.first),
          ),
          const SizedBox(height: 10),
          Row(children: [_num('Ккал', (v) => kcal = v), _num('Белки', (v) => protein = v)]),
          Row(children: [_num('Жиры', (v) => fat = v), _num('Углеводы', (v) => carbs = v)]),
          const FieldLabel('Необязательно'),
          Row(children: [_num('Порция, ${ml ? 'мл' : 'г'}', (v) => portion = v)]),
          Padding(
            padding: const EdgeInsets.all(3),
            child: TextFormField(
              initialValue: barcode,
              keyboardType: TextInputType.number,
              decoration: const InputDecoration(labelText: 'Штрихкод', border: OutlineInputBorder(), isDense: true),
              onChanged: (v) => barcode = v,
            ),
          ),
          const SizedBox(height: 16),
          FilledButton(
            onPressed: !valid
                ? null
                : () {
                    final food = Food(
                      id: uid(),
                      name: name.trim(),
                      kcal: parseNum(kcal),
                      protein: parseNum(protein),
                      fat: parseNum(fat),
                      carbs: parseNum(carbs),
                      ml: ml,
                      portion: parseNum(portion) > 0 ? parseNum(portion) : null,
                      barcode: barcode.trim().isEmpty ? null : barcode.trim(),
                      custom: true,
                    );
                    AppScope.of(context).saveFood(food);
                    Navigator.pop(context, food);
                  },
            child: const Text('Сохранить'),
          ),
        ],
      ),
    );
  }
}
