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

/* все даты от from до to включительно (не больше года) */
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

export function uid(): string {
  const bytes = crypto.getRandomValues(new Uint8Array(16));
  bytes[6] = (bytes[6] & 15) | 64;
  bytes[8] = (bytes[8] & 63) | 128;
  const hex = Array.from(bytes, (byte) => byte.toString(16).padStart(2, '0')).join('');
  return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`;
}

export const r0 = (n: number): number => Math.round(n);
/* разделяем разряды для отображения */
export const n0 = (n: number): string => Math.round(n).toLocaleString('ru-RU');
export const r1 = (n: number): number => Math.round(n * 10) / 10;
export const clamp = (n: number, min: number, max: number): number => Math.min(max, Math.max(min, n));
