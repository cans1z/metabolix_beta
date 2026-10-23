import 'dart:math';

import 'package:flutter/material.dart';

import '../util.dart';

const proteinColor = Color(0xFF3B82F6);
const fatColor = Color(0xFFF59E0B);
const carbsColor = Color(0xFF8B5CF6);
const waterColor = Color(0xFF0EA5E9);

/// Числовой степпер с необязательным слайдером — ввод в минимум касаний.
class NumberStepper extends StatelessWidget {
  final double value, min, max, step;
  final String unit;
  final bool slider;
  final ValueChanged<double> onChanged;

  const NumberStepper({
    super.key,
    required this.value,
    required this.onChanged,
    this.min = 0,
    this.max = 9999,
    this.step = 1,
    this.unit = '',
    this.slider = false,
  });

  void _set(double v) => onChanged((v.clamp(min, max) * 10).round() / 10);

  @override
  Widget build(BuildContext context) {
    return Column(
      mainAxisSize: MainAxisSize.min,
      children: [
        Row(
          children: [
            IconButton.filledTonal(onPressed: () => _set(value - step), icon: const Icon(Icons.remove)),
            Expanded(
              child: Text(
                '${f1(value)} $unit',
                textAlign: TextAlign.center,
                style: Theme.of(context).textTheme.titleMedium,
              ),
            ),
            IconButton.filledTonal(onPressed: () => _set(value + step), icon: const Icon(Icons.add)),
          ],
        ),
        if (slider) Slider(value: value.clamp(min, max).toDouble(), min: min, max: max, onChanged: (v) => _set((v / step).round() * step)),
      ],
    );
  }
}

class Section extends StatelessWidget {
  final String? title;
  final Widget? trailing;
  final List<Widget> children;
  const Section({super.key, this.title, this.trailing, required this.children});

  @override
  Widget build(BuildContext context) {
    return Card(
      margin: const EdgeInsets.fromLTRB(12, 6, 12, 6),
      child: Padding(
        padding: const EdgeInsets.all(14),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            if (title != null)
              Padding(
                padding: const EdgeInsets.only(bottom: 8),
                child: Row(
                  children: [
                    Expanded(child: Text(title!, style: Theme.of(context).textTheme.titleMedium)),
                    if (trailing != null) trailing!,
                  ],
                ),
              ),
            ...children,
          ],
        ),
      ),
    );
  }
}

class FieldLabel extends StatelessWidget {
  final String text;
  const FieldLabel(this.text, {super.key});

  @override
  Widget build(BuildContext context) => Padding(
        padding: const EdgeInsets.only(top: 12, bottom: 6),
        child: Text(text, style: Theme.of(context).textTheme.labelLarge?.copyWith(color: Theme.of(context).hintColor)),
      );
}

class StatTile extends StatelessWidget {
  final String value, label;
  const StatTile(this.value, this.label, {super.key});

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Expanded(
      child: Container(
        margin: const EdgeInsets.all(3),
        padding: const EdgeInsets.symmetric(vertical: 10, horizontal: 8),
        decoration: BoxDecoration(
          color: theme.colorScheme.surfaceContainerHighest,
          borderRadius: BorderRadius.circular(10),
        ),
        child: Column(
          children: [
            Text(value, style: theme.textTheme.titleMedium?.copyWith(fontWeight: FontWeight.bold)),
            Text(label, style: theme.textTheme.bodySmall, textAlign: TextAlign.center),
          ],
        ),
      ),
    );
  }
}

class MacroBar extends StatelessWidget {
  final String label;
  final double value, max;
  final Color color;
  const MacroBar({super.key, required this.label, required this.value, required this.max, required this.color});

  @override
  Widget build(BuildContext context) {
    final left = (max - value).round();
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 4),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [Text(label), Text('${f0(value)} / ${f0(max)} г')],
          ),
          const SizedBox(height: 4),
          ClipRRect(
            borderRadius: BorderRadius.circular(8),
            child: LinearProgressIndicator(
              value: max > 0 ? (value / max).clamp(0, 1).toDouble() : 0,
              minHeight: 8,
              color: color,
            ),
          ),
          Text(left >= 0 ? 'осталось $left г' : 'перебор ${-left} г', style: Theme.of(context).textTheme.bodySmall),
        ],
      ),
    );
  }
}

class Ring extends StatelessWidget {
  final double value, max, size;
  final Widget child;
  const Ring({super.key, required this.value, required this.max, this.size = 150, required this.child});

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final ratio = max > 0 ? value / max : 0.0;
    return SizedBox(
      width: size,
      height: size,
      child: CustomPaint(
        painter: _RingPainter(ratio, ratio > 1 ? scheme.error : scheme.primary, scheme.surfaceContainerHighest),
        child: Center(child: child),
      ),
    );
  }
}

class _RingPainter extends CustomPainter {
  final double ratio;
  final Color color, track;
  _RingPainter(this.ratio, this.color, this.track);

  @override
  void paint(Canvas canvas, Size size) {
    const stroke = 12.0;
    final rect = Rect.fromLTWH(stroke / 2, stroke / 2, size.width - stroke, size.height - stroke);
    final paint = Paint()
      ..style = PaintingStyle.stroke
      ..strokeWidth = stroke
      ..strokeCap = StrokeCap.round
      ..color = track;
    canvas.drawArc(rect, 0, 2 * pi, false, paint);
    canvas.drawArc(rect, -pi / 2, 2 * pi * ratio.clamp(0, 1), false, paint..color = color);
  }

  @override
  bool shouldRepaint(_RingPainter old) => old.ratio != ratio || old.color != color || old.track != track;
}
