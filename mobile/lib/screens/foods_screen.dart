import 'package:flutter/material.dart';

import '../app_state.dart';
import '../models.dart';
import '../util.dart';
import '../widgets/common.dart';
import '../widgets/food_widgets.dart';

class FoodsScreen extends StatelessWidget {
  const FoodsScreen({super.key});

  Future<void> _newFood(BuildContext context, {String? barcode}) async {
    final food = await Navigator.push<Food>(
      context,
      MaterialPageRoute(builder: (_) => FoodFormScreen(barcode: barcode)),
    );
    if (food != null && context.mounted) showFoodCard(context, food);
  }

  /// Поиск по штрихкоду. Код пока вводится вручную — камера подключится
  /// вместе с пакетом сканера (например, mobile_scanner).
  Future<void> _scan(BuildContext context) async {
    final app = AppScope.of(context);
    final demo = app.foods.where((f) => f.barcode != null).take(3).toList();
    final controller = TextEditingController();
    final code = await showDialog<String>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Штрихкод'),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            TextField(
              controller: controller,
              autofocus: true,
              keyboardType: TextInputType.number,
              decoration: const InputDecoration(labelText: 'Код с упаковки', border: OutlineInputBorder()),
            ),
            const SizedBox(height: 8),
            const Text('Для проверки:'),
            Wrap(
              spacing: 6,
              children: [
                for (final f in demo)
                  ActionChip(label: Text(f.barcode!), onPressed: () => controller.text = f.barcode!),
              ],
            ),
          ],
        ),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context), child: const Text('Отмена')),
          FilledButton(onPressed: () => Navigator.pop(context, controller.text.trim()), child: const Text('Найти')),
        ],
      ),
    );
    if (code == null || code.isEmpty || !context.mounted) return;
    final found = app.byBarcode(code);
    if (found != null) {
      showFoodCard(context, found);
      return;
    }
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(
      content: const Text('Продукт не найден'),
      action: app.authed ? SnackBarAction(label: 'Создать', onPressed: () => _newFood(context, barcode: code)) : null,
    ));
  }

  void _editRecipe(BuildContext context, [Recipe? recipe]) {
    Navigator.push(context, MaterialPageRoute<void>(builder: (_) => RecipeScreen(recipe: recipe)));
  }

  @override
  Widget build(BuildContext context) {
    final app = AppScope.of(context);
    return Scaffold(
      appBar: AppBar(
        title: const Text('Продукты'),
        actions: [
          IconButton(
            tooltip: 'Штрихкод',
            icon: const Icon(Icons.qr_code_scanner),
            onPressed: () => _scan(context),
          ),
          if (app.authed) ...[
            IconButton(
              tooltip: 'Новое блюдо',
              icon: const Icon(Icons.restaurant_menu),
              onPressed: () => _editRecipe(context),
            ),
            IconButton(
              tooltip: 'Новый продукт',
              icon: const Icon(Icons.add),
              onPressed: () => _newFood(context),
            ),
          ] else
            TextButton(onPressed: app.logout, child: const Text('Войти')),
        ],
      ),
      body: Column(
        children: [
          if (!app.authed)
            const Padding(
              padding: EdgeInsets.fromLTRB(16, 4, 16, 4),
              child: Text('Вы смотрите справочник без входа. Дневник и свои продукты доступны после авторизации.'),
            ),
          Expanded(
            child: FoodPicker(
              onPick: (f) => showFoodCard(context, f),
              onEditRecipe: app.authed
                  ? (id) => _editRecipe(context, app.recipes.firstWhere((r) => r.id == id))
                  : null,
            ),
          ),
        ],
      ),
    );
  }
}

/// Создание и редактирование пользовательского блюда.
class RecipeScreen extends StatefulWidget {
  final Recipe? recipe;
  const RecipeScreen({super.key, this.recipe});

  @override
  State<RecipeScreen> createState() => _RecipeScreenState();
}

class _RecipeScreenState extends State<RecipeScreen> {
  // Работаем с копией, чтобы «назад» без сохранения ничего не меняло.
  late final Recipe recipe =
      widget.recipe == null ? Recipe(id: uid()) : Recipe.fromJson(widget.recipe!.toJson());

  Future<void> _addItem() async {
    final food = await showFoodPicker(context, title: 'Ингредиент');
    if (food != null) setState(() => recipe.items.add(RecipeItem(food, food.portion ?? 100)));
  }

  @override
  Widget build(BuildContext context) {
    final app = AppScope.of(context);
    final total = recipe.total;
    final per = recipe.toFood();
    final valid = recipe.name.trim().isNotEmpty && recipe.items.isNotEmpty;
    return Scaffold(
      appBar: AppBar(
        title: Text(widget.recipe == null ? 'Новое блюдо' : 'Блюдо'),
        actions: [
          if (widget.recipe != null)
            IconButton(
              icon: const Icon(Icons.delete_outline),
              onPressed: () {
                app.removeRecipe(recipe.id);
                Navigator.pop(context);
              },
            ),
        ],
      ),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          TextFormField(
            initialValue: recipe.name,
            decoration: const InputDecoration(
              labelText: 'Название',
              hintText: 'Например, омлет с сыром',
              border: OutlineInputBorder(),
            ),
            onChanged: (v) => setState(() => recipe.name = v),
          ),
          const FieldLabel('Состав'),
          if (recipe.items.isEmpty) const Text('Добавьте хотя бы один продукт'),
          for (final item in recipe.items)
            Card(
              child: Padding(
                padding: const EdgeInsets.fromLTRB(12, 6, 4, 8),
                child: Column(
                  children: [
                    Row(
                      children: [
                        Expanded(child: Text(item.food.name)),
                        IconButton(
                          icon: const Icon(Icons.close),
                          onPressed: () => setState(() => recipe.items.remove(item)),
                        ),
                      ],
                    ),
                    Padding(
                      padding: const EdgeInsets.only(right: 8),
                      child: NumberStepper(
                        value: item.grams,
                        min: 5,
                        max: 2000,
                        step: 10,
                        unit: item.food.unit,
                        onChanged: (g) => setState(() => item.grams = g),
                      ),
                    ),
                  ],
                ),
              ),
            ),
          const SizedBox(height: 8),
          OutlinedButton.icon(onPressed: _addItem, icon: const Icon(Icons.add), label: const Text('Добавить продукт')),
          if (recipe.items.isNotEmpty) ...[
            FieldLabel('Всё блюдо · ${f0(recipe.weight)} г'),
            Row(children: [
              StatTile(f0(total.kcal), 'ккал'),
              StatTile(f1(total.protein), 'белки'),
              StatTile(f1(total.fat), 'жиры'),
              StatTile(f1(total.carbs), 'углеводы'),
            ]),
            const FieldLabel('На 100 г'),
            Row(children: [
              StatTile(f0(per.kcal), 'ккал'),
              StatTile(f1(per.protein), 'белки'),
              StatTile(f1(per.fat), 'жиры'),
              StatTile(f1(per.carbs), 'углеводы'),
            ]),
          ],
          const SizedBox(height: 20),
          FilledButton(
            onPressed: !valid
                ? null
                : () {
                    recipe.name = recipe.name.trim();
                    app.saveRecipe(recipe);
                    Navigator.pop(context);
                  },
            child: const Text('Сохранить'),
          ),
        ],
      ),
    );
  }
}
