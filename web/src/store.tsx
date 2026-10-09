import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { SEED_FOODS } from './data/foods';
import { addDays, today, uid } from './lib/util';
import type { AppState, Entry, Food, MealType, Profile, Recipe, Settings } from './types';

const KEY = 'metabolix:v1';

const initial: AppState = {
  user: null,
  guest: false,
  profile: null,
  foods: SEED_FOODS,
  recipes: [],
  entries: [],
  favorites: [],
  recents: [],
  water: {},
  weights: {},
  settings: {
    theme: 'system',
    ratio: { protein: 30, fat: 30, carbs: 40 },
    manualTargets: null,
    waterGoal: 2000,
    reminders: { meal: true, water: false, weight: false, time: '09:00' },
  },
};

function load(): AppState {
  try {
    const raw = localStorage.getItem(KEY);
    if (raw) {
      const saved = JSON.parse(raw) as Partial<AppState>;
      return { ...initial, ...saved, settings: { ...initial.settings, ...saved.settings } };
    }
  } catch {
    // повреждённые данные — начинаем с чистого состояния
  }
  return initial;
}

type Updater = (fn: (s: AppState) => AppState) => void;

function actions(set: Updater) {
  return {
    login: (email: string, name?: string) =>
      set((s) => ({ ...s, guest: false, user: { email, name: name || s.profile?.name || email.split('@')[0] } })),
    loginGuest: () => set((s) => ({ ...s, guest: true })),
    logout: () => set((s) => ({ ...s, user: null, guest: false })),

    saveProfile: (profile: Profile) =>
      set((s) => ({
        ...s,
        profile,
        weights: { ...s.weights, [today()]: profile.weight },
      })),
    updateSettings: (patch: Partial<Settings>) => set((s) => ({ ...s, settings: { ...s.settings, ...patch } })),

    addEntry: (date: string, meal: MealType, food: Food, grams: number) =>
      set((s) => ({
        ...s,
        entries: [...s.entries, { id: uid(), date, meal, food, grams }],
        recents: [food.id, ...s.recents.filter((id) => id !== food.id)].slice(0, 30),
      })),
    updateEntry: (id: string, grams: number) =>
      set((s) => ({ ...s, entries: s.entries.map((e) => (e.id === id ? { ...e, grams } : e)) })),
    removeEntry: (id: string) => set((s) => ({ ...s, entries: s.entries.filter((e) => e.id !== id) })),
    copyMeal: (from: string, meal: MealType, to: string) =>
      set((s) => ({
        ...s,
        entries: [
          ...s.entries,
          ...s.entries
            .filter((e) => e.date === from && e.meal === meal)
            .map((e): Entry => ({ ...e, id: uid(), date: to })),
        ],
      })),

    saveFood: (food: Food) =>
      set((s) => ({
        ...s,
        foods: s.foods.some((f) => f.id === food.id)
          ? s.foods.map((f) => (f.id === food.id ? food : f))
          : [food, ...s.foods],
      })),
    removeFood: (id: string) =>
      set((s) => ({
        ...s,
        foods: s.foods.filter((f) => f.id !== id),
        favorites: s.favorites.filter((x) => x !== id),
        recents: s.recents.filter((x) => x !== id),
      })),
    toggleFavorite: (id: string) =>
      set((s) => ({
        ...s,
        favorites: s.favorites.includes(id) ? s.favorites.filter((x) => x !== id) : [id, ...s.favorites],
      })),

    saveRecipe: (recipe: Recipe) =>
      set((s) => ({
        ...s,
        recipes: s.recipes.some((r) => r.id === recipe.id)
          ? s.recipes.map((r) => (r.id === recipe.id ? recipe : r))
          : [recipe, ...s.recipes],
      })),
    removeRecipe: (id: string) => set((s) => ({ ...s, recipes: s.recipes.filter((r) => r.id !== id) })),

    addWater: (date: string, ml: number) =>
      set((s) => ({ ...s, water: { ...s.water, [date]: Math.max(0, (s.water[date] ?? 0) + ml) } })),

    setWeight: (date: string, kg: number) =>
      set((s) => {
        const weights = { ...s.weights, [date]: kg };
        const latest = Object.keys(weights).sort().pop();
        return {
          ...s,
          weights,
          profile: s.profile && latest === date ? { ...s.profile, weight: kg } : s.profile,
        };
      }),
    removeWeight: (date: string) =>
      set((s) => {
        const weights = { ...s.weights };
        delete weights[date];
        return { ...s, weights };
      }),

    /** Заполняет две недели дневника и веса случайными данными для демонстрации. */
    seedDemo: () =>
      set((s) => {
        const pick = () => s.foods[Math.floor(Math.random() * s.foods.length)];
        const entries: Entry[] = [];
        const weights = { ...s.weights };
        const water = { ...s.water };
        const base = s.profile?.weight ?? 75;
        for (let i = 13; i >= 0; i--) {
          const date = addDays(today(), -i);
          (['breakfast', 'lunch', 'dinner', 'snack'] as MealType[]).forEach((meal) => {
            const n = meal === 'snack' ? 1 : 3;
            for (let k = 0; k < n; k++) {
              const food = pick();
              entries.push({ id: uid(), date, meal, food, grams: food.portion ?? 150 });
            }
          });
          weights[date] = Math.round((base + i * 0.12 + (Math.random() - 0.5) * 0.6) * 10) / 10;
          water[date] = 1000 + Math.round(Math.random() * 5) * 250;
        }
        const profile = s.profile && { ...s.profile, weight: weights[today()] };
        return { ...s, entries: [...s.entries, ...entries], weights, water, profile };
      }),
    resetAll: () => set(() => initial),
  };
}

type Store = { state: AppState } & ReturnType<typeof actions>;

const Ctx = createContext<Store | null>(null);

export function StoreProvider({ children }: { children: ReactNode }) {
  const [state, setState] = useState<AppState>(load);
  useEffect(() => localStorage.setItem(KEY, JSON.stringify(state)), [state]);
  const api = useMemo(() => actions(setState), []);
  const store = useMemo(() => ({ state, ...api }), [state, api]);
  return <Ctx.Provider value={store}>{children}</Ctx.Provider>;
}

export function useStore(): Store {
  const store = useContext(Ctx);
  if (!store) throw new Error('useStore вне StoreProvider');
  return store;
}
