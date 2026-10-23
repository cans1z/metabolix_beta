import 'dart:math';

String iso(DateTime d) =>
    '${d.year}-${d.month.toString().padLeft(2, '0')}-${d.day.toString().padLeft(2, '0')}';

DateTime parseIso(String s) => DateTime.parse(s);
String todayIso() => iso(DateTime.now());

String addDays(String s, int n) {
  final d = parseIso(s);
  return iso(DateTime(d.year, d.month, d.day + n));
}

/// Все даты от [from] до [to] включительно (не больше года).
List<String> dateRange(String from, String to) {
  final out = <String>[];
  for (var d = from; d.compareTo(to) <= 0 && out.length < 366; d = addDays(d, 1)) {
    out.add(d);
  }
  return out;
}

const _months = ['янв', 'фев', 'мар', 'апр', 'мая', 'июн', 'июл', 'авг', 'сен', 'окт', 'ноя', 'дек'];
const _weekdays = ['пн', 'вт', 'ср', 'чт', 'пт', 'сб', 'вс'];

String fmtShort(String s) {
  final d = parseIso(s);
  return '${d.day} ${_months[d.month - 1]}';
}

String fmtDate(String s) {
  final t = todayIso();
  if (s == t) return 'Сегодня';
  if (s == addDays(t, -1)) return 'Вчера';
  if (s == addDays(t, 1)) return 'Завтра';
  return '${_weekdays[parseIso(s).weekday - 1]}, ${fmtShort(s)}';
}

final _rnd = Random();
String uid() => '${DateTime.now().microsecondsSinceEpoch.toRadixString(36)}${_rnd.nextInt(1 << 32).toRadixString(36)}';

String f0(num n) => n.round().toString();

String f1(num n) {
  final v = (n * 10).round() / 10;
  return v == v.roundToDouble() ? v.round().toString() : v.toStringAsFixed(1);
}

double parseNum(String s) => double.tryParse(s.trim().replaceAll(',', '.')) ?? 0;
