import 'dart:async';

import 'package:flutter/material.dart';

import '../api.dart';
import '../app_state.dart';
import '../models.dart';
import '../util.dart';
import 'common.dart';

String foodSubtitle(Food f) =>
    '${f0(f.kcal)} ккал · Б ${f1(f.protein)} · Ж ${f1(f.fat)} · У ${f1(f.carbs)} · на 100 ${f.unit}';

/// поиск продуктов и выбор из сохранённых

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
  int page = 1;
  int? _revision;
  Timer? _debounce;
  late AppState _app;
  Future<({List<Food> items, bool more})>? _result;

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    _app = AppScope.of(context);
    if (_revision != _app.catalogRevision) {
      _revision = _app.catalogRevision;
      page = 1;
      _result = null;
      if (tab == 0) _loadPage();
    }
  }

  @override
  void dispose() {
    _debounce?.cancel();
    super.dispose();
  }

  void _loadPage() {
    _result = _app.searchFoods(query.trim(), page);
  }

  void _queryChanged(String value) {
    _debounce?.cancel();
    setState(() {
      query = value;
      page = 1;
      _result = null;
    });
    if (tab == 0) {
      _debounce = Timer(const Duration(milliseconds: 250), () => setState(_loadPage));
    }
  }

  @override
  Widget build(BuildContext context) {
    final app = AppScope.of(context);
    final recipes = app.recipes.map((r) => r.toFood()).toList();
    final source = switch (tab) {
      1 => app.recents,
      2 => [...app.favoriteFoods, ...recipes.where((food) => app.favorites.contains(food.id))],
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
            onChanged: _queryChanged,
          ),
        ),
        SingleChildScrollView(
          scrollDirection: Axis.horizontal,
          padding: const EdgeInsets.symmetric(horizontal: 12),
          child: Row(
            children: [
              for (var i = 0; i < (app.authed ? _tabs.length : 1); i++)
                Padding(
                  padding: const EdgeInsets.only(right: 6),
                  child: ChoiceChip(
                    label: Text(_tabs[i]),
                    selected: tab == i,
                    onSelected: (_) => setState(() {
                      _debounce?.cancel();
                      tab = i;
                      if (i == 0 && _result == null) {
                        _loadPage();
                      }
                    }),
                  ),
                ),
            ],
          ),
        ),
        Expanded(
          child: tab != 0
              ? _foodList(app, list)
              : FutureBuilder(
                  future: _result,
                  builder: (context, snapshot) {
                    if (snapshot.connectionState != ConnectionState.done) {
                      return const Center(child: CircularProgressIndicator());
                    }
                    if (snapshot.hasError) {
                      return Center(
                        child: Column(
                          mainAxisSize: MainAxisSize.min,
                          children: [
                            Text(snapshot.error is ApiException
                                ? snapshot.error.toString()
                                : 'Не удалось загрузить продукты'),
                            TextButton(onPressed: () => setState(_loadPage), child: const Text('Повторить')),
                          ],
                        ),
                      );
                    }
                    final result = snapshot.data!;
                    return Column(
                      children: [
                        Expanded(child: _foodList(app, result.items)),
                        if (page > 1 || result.more)
                          Row(
                            mainAxisAlignment: MainAxisAlignment.center,
                            children: [
                              IconButton(
                                tooltip: 'Предыдущая страница',
                                onPressed: page > 1 ? () => _changePage(-1) : null,
                                icon: const Icon(Icons.chevron_left),
                              ),
                              Text('$page'),
                              IconButton(
                                tooltip: 'Следующая страница',
                                onPressed: result.more ? () => _changePage(1) : null,
                                icon: const Icon(Icons.chevron_right),
                              ),
                            ],
                          ),
                      ],
                    );
                  },
                ),
        ),
      ],
    );
  }

  void _changePage(int delta) => setState(() {
        page += delta;
        _loadPage();
      });

  Widget _foodList(AppState app, List<Food> list) {
    if (list.isEmpty) return const Center(child: Text('Ничего не найдено'));
    return ListView.builder(
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
              if (f.isRecipe && widget.onEditRecipe != null &&
                  app.recipes.any((recipe) => recipe.id == f.id.substring(7)))
                IconButton(
                  icon: const Icon(Icons.edit_outlined),
                  onPressed: () => widget.onEditRecipe!(f.id.substring(7)),
                ),
              if (app.authed && f.id.isNotEmpty)
                IconButton(
                  icon: Icon(fav ? Icons.star : Icons.star_border, color: fav ? fatColor : null),
                  onPressed: app.busy ? null : () => app.toggleFavorite(f),
                ),
            ],
          ),
        );
      },
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

/// пересчёт кбжу по весу или числу порций
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
  final _entryId = entryUuid();

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
              onPressed: app.busy || grams < 0.1 || grams > 20000
                  ? null
                  : () async {
                      if (await app.addEntry(_entryId, widget.date, meal, food, grams) && context.mounted) {
                        Navigator.pop(context);
                      }
                    },
              child: const Text('Добавить в дневник'),
            ),
            if (food.custom && !food.isRecipe && food.id.isNotEmpty)
              TextButton(
                onPressed: app.busy
                    ? null
                    : () async {
                        if (await app.removeFood(food.id) && context.mounted) Navigator.pop(context);
                      },
                child: Text('Удалить продукт', style: TextStyle(color: Theme.of(context).colorScheme.error)),
              ),
          ],
        ],
      ),
    );
  }
}

/// создание своего продукта
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
    final app = AppScope.of(context);
    double? number(String value) => double.tryParse(value.trim().replaceAll(',', '.'));
    bool inRange(String value, double min, double max, {bool optional = false}) {
      if (optional && value.trim().isEmpty) return true;
      final parsed = number(value);
      return parsed != null && parsed >= min && parsed <= max;
    }

    final valid = name.trim().isNotEmpty &&
        name.trim().length <= 200 &&
        inRange(kcal, 0, 1000) &&
        [protein, fat, carbs].every((v) => inRange(v, 0, 100, optional: true)) &&
        inRange(portion, 0.1, 20000, optional: true) &&
        (barcode.trim().isEmpty || RegExp(r'^\d{8,14}$').hasMatch(barcode.trim()));
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
              decoration:
                  const InputDecoration(labelText: 'Штрихкод', border: OutlineInputBorder(), isDense: true),
              onChanged: (v) => setState(() => barcode = v),
            ),
          ),
          const SizedBox(height: 16),
          FilledButton(
            onPressed: !valid || app.busy
                ? null
                : () async {
                    final food = Food(
                      id: '',
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
                    final saved = await app.saveFood(food);
                    if (saved != null && context.mounted) Navigator.pop(context, saved);
                  },
            child: const Text('Сохранить'),
          ),
        ],
      ),
    );
  }
}
