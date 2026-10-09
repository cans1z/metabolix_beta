import 'dart:math';

import 'package:flutter/material.dart';

import '../app_state.dart';
import '../models.dart';
import '../nutrition.dart';
import '../util.dart';
import '../widgets/common.dart';

enum _Period { week, month, custom }

class ProgressScreen extends StatefulWidget {
  const ProgressScreen({super.key});

  @override
  State<ProgressScreen> createState() => _ProgressScreenState();
}

class _ProgressScreenState extends State<ProgressScreen> {
  _Period period = _Period.week;
  bool macros = false;
  String from = addDays(todayIso(), -13), to = todayIso();
  double? kg;

  Future<void> _pickRange() async {
    final range = await showDateRangePicker(
      context: context,
      firstDate: DateTime(2020),
      lastDate: DateTime.now(),
      initialDateRange: DateTimeRange(start: parseIso(from), end: parseIso(to)),
    );
    if (range != null) {
      setState(() {
        from = iso(range.start);
        to = iso(range.end);
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final app = AppScope.of(context);
    final theme = Theme.of(context);
    final profile = app.profile!;
    final target = app.targets;
    final today = todayIso();
    final start = switch (period) {
      _Period.week => addDays(today, -6),
      _Period.month => addDays(today, -29),
      _Period.custom => from,
    };
    final end = period == _Period.custom ? to : today;
    final days = dateRange(start, end);

    final byDay = <String, Macros>{};
    for (final e in app.entries) {
      byDay[e.date] = (byDay[e.date] ?? Macros.zero) + e.macros;
    }
    final data = [for (final d in days) byDay[d] ?? Macros.zero];
    final logged = data.where((m) => m.kcal > 0).toList();
    final total = logged.fold(Macros.zero, (a, b) => a + b);
    final n = max(logged.length, 1);

    final weightDates = app.weights.keys.toList()..sort();
    final inPeriod = weightDates.where((d) => d.compareTo(start) >= 0 && d.compareTo(end) <= 0).toList();
    final index = bmi(profile.weight, profile.height);
    final h2 = (profile.height / 100) * (profile.height / 100);
    final weightInput = kg ?? profile.weight;

    return Scaffold(
      appBar: AppBar(title: const Text('Прогресс')),
      body: ListView(
        padding: const EdgeInsets.only(bottom: 24),
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(12, 4, 12, 4),
            child: SegmentedButton<_Period>(
              showSelectedIcon: false,
              segments: const [
                ButtonSegment(value: _Period.week, label: Text('Неделя')),
                ButtonSegment(value: _Period.month, label: Text('Месяц')),
                ButtonSegment(value: _Period.custom, label: Text('Период')),
              ],
              selected: {period},
              onSelectionChanged: (s) {
                setState(() => period = s.first);
                if (s.first == _Period.custom) _pickRange();
              },
            ),
          ),
          if (period == _Period.custom)
            TextButton.icon(
              onPressed: _pickRange,
              icon: const Icon(Icons.date_range),
              label: Text('${fmtShort(from)} — ${fmtShort(to)}'),
            ),
          Section(
            title: 'Питание',
            trailing: SegmentedButton<bool>(
              showSelectedIcon: false,
              style: const ButtonStyle(visualDensity: VisualDensity.compact),
              segments: const [
                ButtonSegment(value: false, label: Text('Ккал')),
                ButtonSegment(value: true, label: Text('БЖУ')),
              ],
              selected: {macros},
              onSelectionChanged: (s) => setState(() => macros = s.first),
            ),
            children: [
              SizedBox(
                height: 170,
                child: CustomPaint(
                  painter: _BarsPainter(
                    days: days,
                    data: data,
                    target: target.kcal,
                    macros: macros,
                    accent: theme.colorScheme.primary,
                    over: theme.colorScheme.error,
                    muted: theme.hintColor,
                  ),
                ),
              ),
              if (macros)
                const Padding(
                  padding: EdgeInsets.only(top: 4),
                  child: Row(
                    children: [
                      _Legend(proteinColor, 'белки'),
                      _Legend(fatColor, 'жиры'),
                      _Legend(carbsColor, 'углеводы'),
                    ],
                  ),
                ),
              const SizedBox(height: 8),
              Text('В среднем за день (дней с записями: ${logged.length} из ${days.length})',
                  style: theme.textTheme.bodySmall),
              const SizedBox(height: 4),
              Row(children: [
                StatTile(f0(total.kcal / n), 'ккал\nиз ${f0(target.kcal)}'),
                StatTile(f0(total.protein / n), 'белки\nиз ${f0(target.protein)} г'),
                StatTile(f0(total.fat / n), 'жиры\nиз ${f0(target.fat)} г'),
                StatTile(f0(total.carbs / n), 'углев.\nиз ${f0(target.carbs)} г'),
              ]),
            ],
          ),
          Section(
            title: 'Вес',
            trailing: Text('${f1(profile.weight)} кг → цель ${f1(profile.targetWeight)} кг'),
            children: [
              if (inPeriod.length < 2)
                const Padding(
                  padding: EdgeInsets.symmetric(vertical: 20),
                  child: Text('Нужно хотя бы две записи веса за период', textAlign: TextAlign.center),
                )
              else
                SizedBox(
                  height: 160,
                  child: CustomPaint(
                    painter: _WeightPainter(
                      dates: inPeriod,
                      values: [for (final d in inPeriod) app.weights[d]!],
                      target: profile.targetWeight,
                      accent: theme.colorScheme.primary,
                      muted: theme.hintColor,
                    ),
                  ),
                ),
              const SizedBox(height: 8),
              NumberStepper(
                value: weightInput,
                min: 30,
                max: 250,
                step: 0.1,
                unit: 'кг',
                onChanged: (v) => setState(() => kg = v),
              ),
              const SizedBox(height: 8),
              FilledButton.tonal(
                onPressed: () => app.setWeight(today, weightInput),
                child: const Text('Записать вес за сегодня'),
              ),
              for (final d in weightDates.reversed.take(5))
                ListTile(
                  dense: true,
                  contentPadding: EdgeInsets.zero,
                  title: Text(fmtShort(d)),
                  trailing: Row(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Text('${f1(app.weights[d]!)} кг', style: theme.textTheme.titleSmall),
                      IconButton(icon: const Icon(Icons.close, size: 18), onPressed: () => app.removeWeight(d)),
                    ],
                  ),
                ),
            ],
          ),
          Section(
            title: 'Индекс массы тела',
            trailing: const Text('норма 18,5–24,9'),
            children: [
              Row(
                crossAxisAlignment: CrossAxisAlignment.baseline,
                textBaseline: TextBaseline.alphabetic,
                children: [
                  Text(f1(index), style: theme.textTheme.headlineMedium?.copyWith(fontWeight: FontWeight.bold)),
                  const SizedBox(width: 10),
                  Text(bmiLabel(index)),
                ],
              ),
              const SizedBox(height: 8),
              SizedBox(
                height: 18,
                child: CustomPaint(painter: _BmiPainter(index, theme.colorScheme.primary, theme.colorScheme.onSurface)),
              ),
              const SizedBox(height: 8),
              Text('Для вашего роста норма — от ${f1(18.5 * h2)} до ${f1(24.9 * h2)} кг',
                  style: theme.textTheme.bodySmall),
            ],
          ),
        ],
      ),
    );
  }
}

class _Legend extends StatelessWidget {
  final Color color;
  final String label;
  const _Legend(this.color, this.label);

  @override
  Widget build(BuildContext context) => Padding(
        padding: const EdgeInsets.only(right: 12),
        child: Row(children: [
          Container(width: 10, height: 10, decoration: BoxDecoration(color: color, shape: BoxShape.circle)),
          const SizedBox(width: 4),
          Text(label, style: Theme.of(context).textTheme.bodySmall),
        ]),
      );
}

void _text(Canvas canvas, String s, Offset center, Color color) {
  final tp = TextPainter(
    text: TextSpan(text: s, style: TextStyle(color: color, fontSize: 11)),
    textDirection: TextDirection.ltr,
  )..layout();
  tp.paint(canvas, center - Offset(tp.width / 2, tp.height / 2));
}

void _dashed(Canvas canvas, double y, double width, Color color) {
  final paint = Paint()
    ..color = color
    ..strokeWidth = 1;
  for (double x = 0; x < width; x += 8) {
    canvas.drawLine(Offset(x, y), Offset(min(x + 4, width), y), paint);
  }
}

class _BarsPainter extends CustomPainter {
  final List<String> days;
  final List<Macros> data;
  final double target;
  final bool macros;
  final Color accent, over, muted;
  _BarsPainter({
    required this.days,
    required this.data,
    required this.target,
    required this.macros,
    required this.accent,
    required this.over,
    required this.muted,
  });

  @override
  void paint(Canvas canvas, Size size) {
    if (days.isEmpty) return;
    final h = size.height - 18;
    double value(Macros m) => macros ? m.protein + m.fat + m.carbs : m.kcal;
    final top = max(data.map(value).fold(macros ? 1.0 : target, max), 1.0) * 1.1;
    final slot = size.width / days.length;
    final bw = (slot * 0.6).clamp(2.0, 32.0);
    final labelEvery = (days.length / 8).ceil();
    for (var i = 0; i < days.length; i++) {
      final m = data[i];
      final x = slot * i + (slot - bw) / 2;
      final parts = macros
          ? [(m.protein, proteinColor), (m.fat, fatColor), (m.carbs, carbsColor)]
          : [(m.kcal, m.kcal > target ? over : accent)];
      var acc = 0.0;
      for (final (v, color) in parts) {
        final y0 = h - acc / top * h;
        final y1 = h - (acc + v) / top * h;
        acc += v;
        canvas.drawRRect(
          RRect.fromRectAndRadius(Rect.fromLTRB(x, y1, x + bw, y0), const Radius.circular(2)),
          Paint()..color = color,
        );
      }
      if (i % labelEvery == 0) {
        _text(canvas, '${parseIso(days[i]).day}', Offset(x + bw / 2, size.height - 6), muted);
      }
    }
    if (!macros) _dashed(canvas, h - target / top * h, size.width, muted);
  }

  @override
  bool shouldRepaint(_BarsPainter old) => true;
}

class _WeightPainter extends CustomPainter {
  final List<String> dates;
  final List<double> values;
  final double target;
  final Color accent, muted;
  _WeightPainter({
    required this.dates,
    required this.values,
    required this.target,
    required this.accent,
    required this.muted,
  });

  @override
  void paint(Canvas canvas, Size size) {
    final h = size.height - 18;
    final lo = min(values.reduce(min), target) - 0.5;
    final hi = max(values.reduce(max), target) + 0.5;
    final t0 = parseIso(dates.first).millisecondsSinceEpoch;
    final span = max(parseIso(dates.last).millisecondsSinceEpoch - t0, 1);
    double x(String d) => 16 + (parseIso(d).millisecondsSinceEpoch - t0) / span * (size.width - 32);
    double y(double v) => h - (v - lo) / (hi - lo) * (h - 8);

    _dashed(canvas, y(target), size.width, muted);
    final path = Path()..moveTo(x(dates.first), y(values.first));
    for (var i = 1; i < dates.length; i++) {
      path.lineTo(x(dates[i]), y(values[i]));
    }
    canvas.drawPath(
      path,
      Paint()
        ..color = accent
        ..style = PaintingStyle.stroke
        ..strokeWidth = 2.5
        ..strokeJoin = StrokeJoin.round,
    );
    for (var i = 0; i < dates.length; i++) {
      canvas.drawCircle(Offset(x(dates[i]), y(values[i])), 3.5, Paint()..color = accent);
    }
    _text(canvas, fmtShort(dates.first), Offset(28, size.height - 6), muted);
    _text(canvas, fmtShort(dates.last), Offset(size.width - 28, size.height - 6), muted);
  }

  @override
  bool shouldRepaint(_WeightPainter old) => true;
}

/// Шкала ИМТ 15–35 с границами 18,5 / 25 / 30.
class _BmiPainter extends CustomPainter {
  final double value;
  final Color normal, marker;
  _BmiPainter(this.value, this.normal, this.marker);

  @override
  void paint(Canvas canvas, Size size) {
    const bounds = [15.0, 18.5, 25.0, 30.0, 35.0];
    final colors = [waterColor, normal, fatColor, const Color(0xFFD64545)];
    double x(double v) => ((v - 15) / 20).clamp(0.0, 1.0) * size.width;
    canvas.save();
    canvas.clipRRect(RRect.fromRectAndRadius(Rect.fromLTWH(0, 4, size.width, 10), const Radius.circular(5)));
    for (var i = 0; i < 4; i++) {
      canvas.drawRect(Rect.fromLTRB(x(bounds[i]), 4, x(bounds[i + 1]), 14), Paint()..color = colors[i]);
    }
    canvas.restore();
    canvas.drawRRect(
      RRect.fromRectAndRadius(Rect.fromLTWH(x(value) - 2, 0, 4, 18), const Radius.circular(2)),
      Paint()..color = marker,
    );
  }

  @override
  bool shouldRepaint(_BmiPainter old) => old.value != value;
}
