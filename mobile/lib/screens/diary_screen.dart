import 'package:flutter/material.dart';

import '../app_state.dart';
import '../models.dart';
import '../nutrition.dart';
import '../util.dart';
import '../widgets/common.dart';
import '../widgets/food_widgets.dart';

const _glass = 250;

const _mealIcons = {
  MealType.breakfast: Icons.wb_twilight,
  MealType.lunch: Icons.soup_kitchen_outlined,
  MealType.dinner: Icons.nightlight_outlined,
  MealType.snack: Icons.apple,
};

class DiaryScreen extends StatefulWidget {
  const DiaryScreen({super.key});

  @override
  State<DiaryScreen> createState() => _DiaryScreenState();
}

class _DiaryScreenState extends State<DiaryScreen> {
  String date = todayIso();

  Future<String?> _pickDate(String initial) async {
    final picked = await showDatePicker(
      context: context,
      initialDate: parseIso(initial),
      firstDate: DateTime(2020),
      lastDate: DateTime.now().add(const Duration(days: 365)),
    );
    return picked == null ? null : iso(picked);
  }

  Future<void> _add(MealType meal) async {
    final food = await showFoodPicker(context, title: mealLabels[meal]!);
    if (food == null || !mounted) return;
    await showFoodCard(context, food, date: date, meal: meal);
  }

  Future<void> _copy(MealType meal) async {
    final to = await _pickDate(addDays(date, 1));
    if (to == null || to == date || !mounted) return;
    AppScope.of(context).copyMeal(date, meal, to);
    ScaffoldMessenger.of(context)
        .showSnackBar(SnackBar(content: Text('${mealLabels[meal]} скопирован на ${fmtShort(to)}')));
  }

  void _edit(Entry entry) {
    var grams = entry.grams;
    showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      useSafeArea: true,
      showDragHandle: true,
      builder: (context) => StatefulBuilder(
        builder: (context, setSheet) => Padding(
          padding: const EdgeInsets.fromLTRB(16, 0, 16, 20),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Text(entry.food.name, style: Theme.of(context).textTheme.titleLarge),
              const SizedBox(height: 12),
              AmountPicker(food: entry.food, grams: grams, onChanged: (g) => setSheet(() => grams = g)),
              const SizedBox(height: 12),
              FilledButton(
                onPressed: () {
                  AppScope.of(context).updateEntry(entry.id, grams);
                  Navigator.pop(context);
                },
                child: const Text('Сохранить'),
              ),
              TextButton(
                onPressed: () {
                  AppScope.of(context).removeEntry(entry.id);
                  Navigator.pop(context);
                },
                child: Text('Удалить', style: TextStyle(color: Theme.of(context).colorScheme.error)),
              ),
            ],
          ),
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final app = AppScope.of(context);
    final theme = Theme.of(context);
    final target = app.targets;
    final day = app.entriesOn(date);
    final total = sumEntries(day);
    final left = (target.kcal - total.kcal).round();
    final water = app.water[date] ?? 0;

    return Scaffold(
      appBar: AppBar(
        centerTitle: true,
        leading: IconButton(
          icon: const Icon(Icons.chevron_left),
          onPressed: () => setState(() => date = addDays(date, -1)),
        ),
        title: TextButton(
          onPressed: () async {
            final d = await _pickDate(date);
            if (d != null) setState(() => date = d);
          },
          child: Text(fmtDate(date), style: theme.textTheme.titleLarge),
        ),
        actions: [
          IconButton(
            icon: const Icon(Icons.chevron_right),
            onPressed: () => setState(() => date = addDays(date, 1)),
          ),
        ],
      ),
      body: ListView(
        padding: const EdgeInsets.only(bottom: 24),
        children: [
          Section(
            children: [
              Center(
                child: Ring(
                  value: total.kcal,
                  max: target.kcal,
                  child: Column(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Text('${left.abs()}',
                          style: theme.textTheme.headlineMedium?.copyWith(fontWeight: FontWeight.bold)),
                      Text(left >= 0 ? 'ккал осталось' : 'ккал перебор', style: theme.textTheme.bodySmall),
                    ],
                  ),
                ),
              ),
              const SizedBox(height: 8),
              Text('Съедено ${f0(total.kcal)} из ${f0(target.kcal)} ккал',
                  textAlign: TextAlign.center, style: theme.textTheme.bodySmall),
              const SizedBox(height: 8),
              MacroBar(label: 'Белки', value: total.protein, max: target.protein, color: proteinColor),
              MacroBar(label: 'Жиры', value: total.fat, max: target.fat, color: fatColor),
              MacroBar(label: 'Углеводы', value: total.carbs, max: target.carbs, color: carbsColor),
            ],
          ),
          for (final meal in MealType.values) _mealCard(app, meal, day.where((e) => e.meal == meal).toList()),
          Section(
            title: 'Вода',
            trailing: Text('$water / ${app.settings.waterGoal} мл'),
            children: [
              ClipRRect(
                borderRadius: BorderRadius.circular(8),
                child: LinearProgressIndicator(
                  value: (water / app.settings.waterGoal).clamp(0, 1).toDouble(),
                  minHeight: 8,
                  color: waterColor,
                ),
              ),
              const SizedBox(height: 8),
              Row(
                children: [
                  Expanded(
                    child: OutlinedButton(
                      onPressed: water == 0 ? null : () => app.addWater(date, -_glass),
                      child: const Text('− $_glass мл'),
                    ),
                  ),
                  const SizedBox(width: 8),
                  Expanded(
                    child: FilledButton.tonal(
                      onPressed: () => app.addWater(date, _glass),
                      child: const Text('+ $_glass мл'),
                    ),
                  ),
                ],
              ),
            ],
          ),
        ],
      ),
    );
  }

  Widget _mealCard(AppState app, MealType meal, List<Entry> entries) {
    return Section(
      title: mealLabels[meal],
      trailing: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Text('${f0(sumEntries(entries).kcal)} ккал'),
          const SizedBox(width: 6),
          Icon(_mealIcons[meal], size: 20),
        ],
      ),
      children: [
        for (final e in entries)
          Dismissible(
            key: ValueKey(e.id),
            direction: DismissDirection.endToStart,
            background: Container(
              alignment: Alignment.centerRight,
              padding: const EdgeInsets.only(right: 16),
              color: Theme.of(context).colorScheme.errorContainer,
              child: const Icon(Icons.delete_outline),
            ),
            onDismissed: (_) => app.removeEntry(e.id),
            child: ListTile(
              dense: true,
              contentPadding: EdgeInsets.zero,
              title: Text(e.food.name),
              subtitle: Text(
                  '${f0(e.grams)} ${e.food.unit} · Б ${f0(e.macros.protein)} · Ж ${f0(e.macros.fat)} · У ${f0(e.macros.carbs)}'),
              trailing: Text(f0(e.macros.kcal), style: Theme.of(context).textTheme.titleMedium),
              onTap: () => _edit(e),
            ),
          ),
        Row(
          children: [
            FilledButton.tonalIcon(
              onPressed: () => _add(meal),
              icon: const Icon(Icons.add),
              label: const Text('Добавить'),
            ),
            if (entries.isNotEmpty) ...[
              const SizedBox(width: 8),
              TextButton.icon(
                onPressed: () => _copy(meal),
                icon: const Icon(Icons.copy_outlined, size: 18),
                label: const Text('Копировать'),
              ),
            ],
          ],
        ),
      ],
    );
  }
}
