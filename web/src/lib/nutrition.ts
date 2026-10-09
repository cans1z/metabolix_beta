import type { Entry, Food, Goal, Macros, MealType, Profile, Recipe, Settings } from '../types';
import { parse } from './util';

export const MEALS: { id: MealType; label: string; icon: string }[] = [
  { id: 'breakfast', label: 'Завтрак', icon: '🌅' },
  { id: 'lunch', label: 'Обед', icon: '🍲' },
  { id: 'dinner', label: 'Ужин', icon: '🌙' },
  { id: 'snack', label: 'Перекусы', icon: '🍎' },
];

export const ACTIVITY: { value: number; label: string; hint: string }[] = [
  { value: 1.2, label: 'Минимальная', hint: 'сидячая работа, нет тренировок' },
  { value: 1.375, label: 'Лёгкая', hint: '1–3 тренировки в неделю' },
  { value: 1.55, label: 'Средняя', hint: '3–5 тренировок в неделю' },
  { value: 1.725, label: 'Высокая', hint: '6–7 тренировок в неделю' },
  { value: 1.9, label: 'Очень высокая', hint: 'физический труд и спорт' },
];

export const GOALS: { value: Goal; label: string }[] = [
  { value: 'lose', label: 'Похудение' },
  { value: 'maintain', label: 'Поддержание' },
  { value: 'gain', label: 'Набор массы' },
];

/** Поправка к TDEE по цели: −15% похудение, +10% набор. */
const GOAL_ADJ: Record<Goal, number> = { lose: -0.15, maintain: 0, gain: 0.1 };

export const ZERO: Macros = { kcal: 0, protein: 0, fat: 0, carbs: 0 };

export function ageFrom(birthDate: string): number {
  const b = parse(birthDate);
  const now = new Date();
  let age = now.getFullYear() - b.getFullYear();
  if (now.getMonth() < b.getMonth() || (now.getMonth() === b.getMonth() && now.getDate() < b.getDate())) age--;
  return age;
}

/** Основной обмен по формуле Миффлина — Сан Жеора. */
export function bmr(p: Profile): number {
  return 10 * p.weight + 6.25 * p.height - 5 * ageFrom(p.birthDate) + (p.sex === 'male' ? 5 : -161);
}

export const tdee = (p: Profile): number => bmr(p) * p.activity;

export function autoTargets(p: Profile, ratio: Settings['ratio']): Macros {
  const kcal = tdee(p) * (1 + GOAL_ADJ[p.goal]);
  return {
    kcal: Math.round(kcal),
    protein: Math.round((kcal * ratio.protein) / 100 / 4),
    fat: Math.round((kcal * ratio.fat) / 100 / 9),
    carbs: Math.round((kcal * ratio.carbs) / 100 / 4),
  };
}

export const targetsFor = (p: Profile, s: Settings): Macros => s.manualTargets ?? autoTargets(p, s.ratio);

export function macrosFor(food: Food, grams: number): Macros {
  const k = grams / 100;
  return { kcal: food.kcal * k, protein: food.protein * k, fat: food.fat * k, carbs: food.carbs * k };
}

export const add = (a: Macros, b: Macros): Macros => ({
  kcal: a.kcal + b.kcal,
  protein: a.protein + b.protein,
  fat: a.fat + b.fat,
  carbs: a.carbs + b.carbs,
});

export const sumEntries = (entries: Entry[]): Macros =>
  entries.reduce((acc, e) => add(acc, macrosFor(e.food, e.grams)), ZERO);

export const recipeWeight = (r: Recipe): number => r.items.reduce((s, i) => s + i.grams, 0);

export const recipeTotal = (r: Recipe): Macros =>
  r.items.reduce((acc, i) => add(acc, macrosFor(i.food, i.grams)), ZERO);

/** Блюдо как продукт: удельная ценность на 100 г, порция — всё блюдо. */
export function recipeToFood(r: Recipe): Food {
  const w = recipeWeight(r) || 1;
  const t = recipeTotal(r);
  const per = (n: number) => Math.round((n / w) * 1000) / 10;
  return {
    id: 'recipe:' + r.id,
    name: r.name,
    kcal: per(t.kcal),
    protein: per(t.protein),
    fat: per(t.fat),
    carbs: per(t.carbs),
    unit: 'g',
    portion: recipeWeight(r),
  };
}

export const bmi = (weight: number, height: number): number => weight / (height / 100) ** 2;

export function bmiLabel(v: number): string {
  if (v < 18.5) return 'Дефицит массы';
  if (v < 25) return 'Норма';
  if (v < 30) return 'Избыточная масса';
  return 'Ожирение';
}
