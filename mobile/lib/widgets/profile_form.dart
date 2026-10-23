import 'package:flutter/material.dart';

import '../models.dart';
import '../nutrition.dart';
import '../util.dart';
import 'common.dart';

class ProfileForm extends StatefulWidget {
  final Profile initial;
  final String submitLabel;
  final ValueChanged<Profile> onSubmit;
  const ProfileForm({super.key, required this.initial, required this.submitLabel, required this.onSubmit});

  @override
  State<ProfileForm> createState() => _ProfileFormState();
}

class _ProfileFormState extends State<ProfileForm> {
  late final Profile p = widget.initial.copy();

  Future<void> _pickBirthDate() async {
    final now = DateTime.now();
    final picked = await showDatePicker(
      context: context,
      initialDate: parseIso(p.birthDate),
      firstDate: DateTime(1930),
      lastDate: DateTime(now.year - 10, now.month, now.day),
    );
    if (picked != null) setState(() => p.birthDate = iso(picked));
  }

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        TextFormField(
          initialValue: p.name,
          decoration: const InputDecoration(labelText: 'Имя', border: OutlineInputBorder()),
          onChanged: (v) => p.name = v.trim(),
        ),
        const FieldLabel('Пол'),
        SegmentedButton<bool>(
          showSelectedIcon: false,
          segments: const [
            ButtonSegment(value: true, label: Text('Мужской')),
            ButtonSegment(value: false, label: Text('Женский')),
          ],
          selected: {p.male},
          onSelectionChanged: (s) => setState(() => p.male = s.first),
        ),
        const FieldLabel('Дата рождения'),
        OutlinedButton.icon(
          onPressed: _pickBirthDate,
          icon: const Icon(Icons.cake_outlined),
          label: Text('${p.birthDate.split('-').reversed.join('.')} · ${ageFrom(p.birthDate)} лет'),
        ),
        const FieldLabel('Рост'),
        NumberStepper(
          value: p.height,
          min: 120,
          max: 220,
          unit: 'см',
          slider: true,
          onChanged: (v) => setState(() => p.height = v),
        ),
        const FieldLabel('Текущий вес'),
        NumberStepper(
          value: p.weight,
          min: 30,
          max: 250,
          step: 0.5,
          unit: 'кг',
          slider: true,
          onChanged: (v) => setState(() => p.weight = v),
        ),
        const FieldLabel('Целевой вес'),
        NumberStepper(
          value: p.targetWeight,
          min: 30,
          max: 250,
          step: 0.5,
          unit: 'кг',
          slider: true,
          onChanged: (v) => setState(() => p.targetWeight = v),
        ),
        const FieldLabel('Цель'),
        SegmentedButton<String>(
          showSelectedIcon: false,
          segments: [for (final g in goalLabels.entries) ButtonSegment(value: g.key, label: Text(g.value))],
          selected: {p.goal},
          onSelectionChanged: (s) => setState(() => p.goal = s.first),
        ),
        const FieldLabel('Физическая активность'),
        for (final a in activityLevels)
          Card(
            margin: const EdgeInsets.symmetric(vertical: 3),
            color: p.activity == a.value ? Theme.of(context).colorScheme.primaryContainer : null,
            child: ListTile(
              dense: true,
              title: Text(a.label),
              subtitle: Text(a.hint),
              trailing: Text('×${a.value}'),
              onTap: () => setState(() => p.activity = a.value),
            ),
          ),
        const SizedBox(height: 16),
        FilledButton(onPressed: () => widget.onSubmit(p), child: Text(widget.submitLabel)),
      ],
    );
  }
}
