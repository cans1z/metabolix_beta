export const iso = (d: Date): string => {
  const m = String(d.getMonth() + 1).padStart(2, '0');
  const day = String(d.getDate()).padStart(2, '0');
  return `${d.getFullYear()}-${m}-${day}`;
};

export const parse = (s: string): Date => new Date(s + 'T00:00:00');
export const today = (): string => iso(new Date());

export const addDays = (s: string, n: number): string => {
  const d = parse(s);
  d.setDate(d.getDate() + n);
  return iso(d);
};

/** Все даты от from до to включительно (не больше года). */
export const dateRange = (from: string, to: string): string[] => {
  const out: string[] = [];
  for (let d = from; d <= to && out.length < 366; d = addDays(d, 1)) out.push(d);
  return out;
};

export const fmtDate = (s: string): string => {
  if (s === today()) return 'Сегодня';
  if (s === addDays(today(), -1)) return 'Вчера';
  if (s === addDays(today(), 1)) return 'Завтра';
  return parse(s).toLocaleDateString('ru-RU', { weekday: 'short', day: 'numeric', month: 'long' });
};

export const fmtShort = (s: string): string =>
  parse(s).toLocaleDateString('ru-RU', { day: 'numeric', month: 'short' });

export const uid = (): string =>
  typeof crypto !== 'undefined' && 'randomUUID' in crypto
    ? crypto.randomUUID()
    : Math.random().toString(36).slice(2) + Date.now().toString(36);

export const r0 = (n: number): number => Math.round(n);
export const r1 = (n: number): number => Math.round(n * 10) / 10;
export const clamp = (n: number, min: number, max: number): number => Math.min(max, Math.max(min, n));
