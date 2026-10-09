import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../app_state.dart';
import '../models.dart';
import '../nutrition.dart';
import '../util.dart';
import '../widgets/common.dart';
import '../widgets/profile_form.dart';

class ProfileScreen extends StatelessWidget {
  const ProfileScreen({super.key});

  void _editProfile(BuildContext context, AppState app) {
    Navigator.push(
      context,
      MaterialPageRoute<void>(
        builder: (context) => Scaffold(
          appBar: AppBar(title: const Text('Анкета')),
          body: ListView(
            padding: const EdgeInsets.all(16),
            children: [
              ProfileForm(
                initial: app.profile!,
                submitLabel: 'Сохранить',
                onSubmit: (p) {
                  app.saveProfile(p);
                  Navigator.pop(context);
                },
              ),
            ],
          ),
        ),
      ),
    );
  }

  /// Экспорт дневника за 30 дней. Файл появится вместе с пакетом для
  /// сохранения/шаринга; пока CSV копируется в буфер обмена.
  Future<void> _exportCsv(BuildContext context, AppState app) async {
    final from = addDays(todayIso(), -29);
    final rows = app.entries.where((e) => e.date.compareTo(from) >= 0).toList()
      ..sort((a, b) => a.date.compareTo(b.date));
    final csv = [
      'Дата;Приём пищи;Продукт;Количество;Ккал;Белки;Жиры;Углеводы',
      for (final e in rows)
        [
          e.date,
          mealLabels[e.meal],
          '"${e.food.name.replaceAll('"', '""')}"',
          f0(e.grams),
          f0(e.macros.kcal),
          f1(e.macros.protein),
          f1(e.macros.fat),
          f1(e.macros.carbs),
        ].join(';'),
    ].join('\n');
    await Clipboard.setData(ClipboardData(text: csv));
    if (!context.mounted) return;
    ScaffoldMessenger.of(context)
        .showSnackBar(SnackBar(content: Text('CSV за 30 дней скопирован в буфер (${rows.length} записей)')));
  }

  Future<void> _reset(BuildContext context, AppState app) async {
    final ok = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Удалить все данные приложения?'),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context, false), child: const Text('Отмена')),
          FilledButton(onPressed: () => Navigator.pop(context, true), child: const Text('Удалить')),
        ],
      ),
    );
    if (ok == true) app.resetAll();
  }

  @override
  Widget build(BuildContext context) {
    final app = AppScope.of(context);
    final profile = app.profile!;
    final s = app.settings;
    final auto = autoTargets(profile, s);
    final manual = s.manualTargets;

    void setManual(Macros m) => app.updateSettings((s) => s.manualTargets = m);
    final time = s.remindTime.split(':').map(int.parse).toList();

    return Scaffold(
      appBar: AppBar(
        title: const Text('Профиль'),
        actions: [TextButton(onPressed: app.logout, child: const Text('Выйти'))],
      ),
      body: ListView(
        padding: const EdgeInsets.only(bottom: 24),
        children: [
          Section(
            children: [
              ListTile(
                contentPadding: EdgeInsets.zero,
                leading: CircleAvatar(child: Text(profile.name.isEmpty ? '?' : profile.name[0].toUpperCase())),
                title: Text(profile.name.isEmpty ? app.userName : profile.name),
                subtitle: Text(
                    '${app.userEmail ?? ''}\n${profile.male ? 'М' : 'Ж'} · ${ageFrom(profile.birthDate)} лет · ${f0(profile.height)} см · ${f1(profile.weight)} кг · ${goalLabels[profile.goal]}'),
                isThreeLine: true,
                trailing: const Icon(Icons.edit_outlined),
                onTap: () => _editProfile(context, app),
              ),
            ],
          ),
          Section(
            title: 'Норма КБЖУ',
            children: [
              Row(children: [
                StatTile(f0(bmr(profile)), 'основной\nобмен'),
                StatTile(f0(tdee(profile)), 'расход\n(TDEE)'),
                StatTile(f0(auto.kcal), 'норма\nпо цели'),
              ]),
              FieldLabel('Белки — ${s.ratioProtein}% · ${f0(auto.protein)} г'),
              Slider(
                value: s.ratioProtein.toDouble(),
                min: 10,
                max: 60,
                divisions: 10,
                onChanged: (v) => app.updateSettings((s) {
                  s.ratioProtein = v.round();
                  if (s.ratioProtein + s.ratioFat > 90) s.ratioFat = 90 - s.ratioProtein;
                }),
              ),
              FieldLabel('Жиры — ${s.ratioFat}% · ${f0(auto.fat)} г'),
              Slider(
                value: s.ratioFat.toDouble(),
                min: 10,
                max: 60,
                divisions: 10,
                onChanged: (v) => app.updateSettings((s) {
                  s.ratioFat = v.round();
                  if (s.ratioProtein + s.ratioFat > 90) s.ratioProtein = 90 - s.ratioFat;
                }),
              ),
              Text('Углеводы — ${s.ratioCarbs}% · ${f0(auto.carbs)} г'),
              SwitchListTile(
                contentPadding: EdgeInsets.zero,
                title: const Text('Задать норму вручную'),
                value: manual != null,
                onChanged: (on) => app.updateSettings((s) => s.manualTargets = on ? auto : null),
              ),
              if (manual != null) ...[
                const FieldLabel('Калории'),
                NumberStepper(
                  value: manual.kcal,
                  min: 800,
                  max: 6000,
                  step: 50,
                  unit: 'ккал',
                  onChanged: (v) => setManual(Macros(v, manual.protein, manual.fat, manual.carbs)),
                ),
                const FieldLabel('Белки'),
                NumberStepper(
                  value: manual.protein,
                  max: 500,
                  step: 5,
                  unit: 'г',
                  onChanged: (v) => setManual(Macros(manual.kcal, v, manual.fat, manual.carbs)),
                ),
                const FieldLabel('Жиры'),
                NumberStepper(
                  value: manual.fat,
                  max: 500,
                  step: 5,
                  unit: 'г',
                  onChanged: (v) => setManual(Macros(manual.kcal, manual.protein, v, manual.carbs)),
                ),
                const FieldLabel('Углеводы'),
                NumberStepper(
                  value: manual.carbs,
                  max: 900,
                  step: 5,
                  unit: 'г',
                  onChanged: (v) => setManual(Macros(manual.kcal, manual.protein, manual.fat, v)),
                ),
              ],
            ],
          ),
          Section(
            title: 'Настройки',
            children: [
              const FieldLabel('Тема оформления'),
              SegmentedButton<String>(
                showSelectedIcon: false,
                segments: const [
                  ButtonSegment(value: 'system', label: Text('Системная')),
                  ButtonSegment(value: 'light', label: Text('Светлая')),
                  ButtonSegment(value: 'dark', label: Text('Тёмная')),
                ],
                selected: {s.theme},
                onSelectionChanged: (v) => app.updateSettings((s) => s.theme = v.first),
              ),
              const FieldLabel('Норма воды в день'),
              NumberStepper(
                value: s.waterGoal.toDouble(),
                min: 500,
                max: 6000,
                step: 250,
                unit: 'мл',
                onChanged: (v) => app.updateSettings((s) => s.waterGoal = v.round()),
              ),
              const FieldLabel('Напоминания'),
              SwitchListTile(
                contentPadding: EdgeInsets.zero,
                title: const Text('Внести приём пищи'),
                value: s.remindMeal,
                onChanged: (v) => app.updateSettings((s) => s.remindMeal = v),
              ),
              SwitchListTile(
                contentPadding: EdgeInsets.zero,
                title: const Text('Выпить воды'),
                value: s.remindWater,
                onChanged: (v) => app.updateSettings((s) => s.remindWater = v),
              ),
              SwitchListTile(
                contentPadding: EdgeInsets.zero,
                title: const Text('Записать вес'),
                value: s.remindWeight,
                onChanged: (v) => app.updateSettings((s) => s.remindWeight = v),
              ),
              ListTile(
                contentPadding: EdgeInsets.zero,
                title: const Text('Время напоминания'),
                trailing: Text(s.remindTime, style: Theme.of(context).textTheme.titleMedium),
                onTap: () async {
                  final picked = await showTimePicker(
                    context: context,
                    initialTime: TimeOfDay(hour: time[0], minute: time[1]),
                  );
                  if (picked != null) {
                    app.updateSettings((s) => s.remindTime =
                        '${picked.hour.toString().padLeft(2, '0')}:${picked.minute.toString().padLeft(2, '0')}');
                  }
                },
              ),
            ],
          ),
          Section(
            title: 'Данные',
            children: [
              OutlinedButton.icon(
                onPressed: () => _exportCsv(context, app),
                icon: const Icon(Icons.file_download_outlined),
                label: const Text('Экспорт дневника в CSV'),
              ),
              OutlinedButton.icon(
                onPressed: app.seedDemo,
                icon: const Icon(Icons.auto_awesome_outlined),
                label: const Text('Заполнить демо-данными'),
              ),
              TextButton(
                onPressed: () => _reset(context, app),
                child: Text('Сбросить всё', style: TextStyle(color: Theme.of(context).colorScheme.error)),
              ),
            ],
          ),
        ],
      ),
    );
  }
}
