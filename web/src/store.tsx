import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { ApiClient, ApiError, type AuthResponse } from './lib/api';
import { today } from './lib/util';
import type { AppState, Entry, Food, MealType, Profile, Recipe, Settings, User } from './types';

export interface FoodPage {
  items: Food[];
  total: number;
  page: number;
  pageSize: number;
}

interface History {
  diary: Entry[];
  weights: { date: string; kg: number }[];
  water: { date: string; ml: number }[];
}

function emptyState(): AppState {
  return {
    user: null, guest: false, profile: null, foods: [], recipes: [], entries: [],
    favorites: [], recents: [], water: {}, weights: {},
    settings: {
      theme: 'system', ratio: { protein: 30, fat: 30, carbs: 40 },
      manualTargets: null, waterGoal: 2000,
      reminders: { meal: true, water: false, weight: false, time: '09:00' },
    },
  };
}

function distinctFoods(foods: Food[]) {
  return [...new Map(foods.map((food) => [food.id, food])).values()];
}

async function loadAccount(api: ApiClient): Promise<AppState> {
  const user = await api.request<User>('auth/me');
  const [profile, settings, history, recipes, favorites, recents] = await Promise.all([
    user.hasProfile ? api.request<Profile>('profile') : Promise.resolve(null),
    api.request<Settings>('settings'),
    api.request<History>('sync/pull'),
    api.request<Recipe[]>('recipes'),
    api.request<{ foods: Food[]; recipes: Recipe[] }>('favorites'),
    api.request<Food[]>('foods/recent'),
  ]);
  return {
    user, guest: false, profile, settings, recipes,
    entries: history.diary.map((entry) => ({ ...entry, food: { ...entry.food, id: entry.food.id ?? '' } })),
    weights: Object.fromEntries(history.weights.map((record) => [record.date, record.kg])),
    water: Object.fromEntries(history.water.map((record) => [record.date, record.ml])),
    foods: distinctFoods([...favorites.foods, ...recents]),
    favorites: [...favorites.foods.map((food) => food.id), ...favorites.recipes.map((recipe) => 'recipe:' + recipe.id)],
    recents: recents.map((food) => food.id),
  };
}

function withEntries(state: AppState, entries: Entry[]): AppState {
  const latest = [...entries].sort((a, b) => b.updatedAt.localeCompare(a.updatedAt));
  const recents = distinctFoods(latest.slice(0, 300).map((entry) => entry.food)).slice(0, 30);
  return {
    ...state, entries, recents: recents.map((food) => food.id),
    foods: distinctFoods([...recents, ...state.foods]),
  };
}

function withWeight(state: AppState, weights: Record<string, number>): AppState {
  const latest = Object.keys(weights).sort().pop();
  return {
    ...state, weights,
    profile: state.profile && latest ? { ...state.profile, weight: weights[latest] } : state.profile,
  };
}

type Update = (change: (state: AppState) => AppState) => void;
type Run = <T>(action: () => Promise<T>) => Promise<T | undefined>;

function actions(api: ApiClient, update: Update, get: () => AppState, run: Run) {
  const change = async (action: () => Promise<void>) =>
    (await run(async () => { await action(); return true; })) ?? false;
  return {
    login: (email: string, password: string, name?: string, acceptedTerms = false) => change(async () => {
      const response = await api.request<AuthResponse>(name === undefined ? 'auth/login' : 'auth/register', {
        method: 'POST', anonymous: true,
        body: { email, password, ...(name === undefined ? {} : { name, acceptedTerms }) },
      });
      api.save(response);
      // сохраняем сессию чтобы повторить загрузку после обрыва сети
      update(() => ({ ...emptyState(), user: response.user }));
    }),
    forgotPassword: (email: string) => change(async () => {
      await api.request('auth/forgot', { method: 'POST', anonymous: true, body: { email } });
    }),
    resetPassword: (email: string, code: string, newPassword: string) => change(async () => {
      await api.request('auth/reset', { method: 'POST', anonymous: true, body: { email, code, newPassword } });
    }),
    loginGuest: () => update((state) => ({ ...state, guest: true })),
    logout: () => change(async () => {
      try { await api.logout(); } finally {
        if (!api.hasSession) update(() => emptyState());
      }
    }),
    deleteAccount: (password: string) => change(async () => {
      await api.request('auth/delete-account', { method: 'POST', body: { password } });
      api.clear();
      update(() => emptyState());
    }),
    saveProfile: (profile: Profile) => change(async () => {
      const date = today();
      const saved = await api.request<Profile>('profile', { method: 'PUT', body: { ...profile, today: date } });
      update((state) => ({
        ...state, profile: saved,
        user: state.user && { ...state.user, name: saved.name, hasProfile: true },
        weights: { ...state.weights, [date]: profile.weight },
      }));
    }),
    updateSettings: (patch: Partial<Settings>) => change(async () => {
      const settings = await api.request<Settings>('settings', {
        method: 'PUT', body: { ...get().settings, ...patch },
      });
      update((state) => ({ ...state, settings }));
    }),
    addEntry: (id: string, date: string, meal: MealType, food: Food, grams: number) => change(async () => {
      const saved = await api.request<Entry>('diary', { method: 'POST', body: { id, date, meal, food, grams } });
      update((state) => withEntries(state, [...state.entries.filter((entry) => entry.id !== saved.id), saved]));
    }),
    updateEntry: (id: string, grams: number) => change(async () => {
      const saved = await api.request<Entry>('diary/' + id, { method: 'PUT', body: { grams } });
      update((state) => withEntries(state, state.entries.map((entry) => entry.id === id ? saved : entry)));
    }),
    removeEntry: (id: string) => change(async () => {
      await api.request('diary/' + id, { method: 'DELETE' });
      update((state) => withEntries(state, state.entries.filter((entry) => entry.id !== id)));
    }),
    copyMeal: (from: string, meal: MealType, to: string) => change(async () => {
      const copied = await api.request<Entry[]>('diary/copy', { method: 'POST', body: { from, meal, to } });
      update((state) => withEntries(state, [...state.entries, ...copied]));
    }),
    searchFoods: (query: string, page: number, signal?: AbortSignal) => api.request<FoodPage>(
      'foods?' + new URLSearchParams({ q: query, page: String(page), pageSize: '30' }), { signal },
    ),
    byBarcode: (code: string) => run(async () => {
      try { return await api.request<Food>('foods/barcode/' + encodeURIComponent(code)); } catch (error) {
        if (error instanceof ApiError && error.status === 404) return null;
        throw error;
      }
    }),
    saveFood: (food: Food) => run(async () => {
      const editing = !!food.id;
      const saved = await api.request<Food>(editing ? 'foods/' + food.id : 'foods', {
        method: editing ? 'PUT' : 'POST', body: food,
      });
      update((state) => ({ ...state, foods: [saved, ...state.foods.filter((item) => item.id !== saved.id)] }));
      return saved;
    }),
    removeFood: (id: string) => change(async () => {
      await api.request('foods/' + id, { method: 'DELETE' });
      update((state) => ({
        ...state, foods: state.foods.filter((food) => food.id !== id),
        favorites: state.favorites.filter((item) => item !== id),
      }));
    }),
    toggleFavorite: (food: Food) => change(async () => {
      const removing = get().favorites.includes(food.id);
      const path = food.id.startsWith('recipe:') ? 'recipes/' + food.id.slice(7) : 'foods/' + food.id;
      await api.request('favorites/' + path, { method: removing ? 'DELETE' : 'PUT' });
      update((state) => ({
        ...state, foods: distinctFoods([...state.foods, food]),
        favorites: removing ? state.favorites.filter((id) => id !== food.id) : [food.id, ...state.favorites],
      }));
    }),
    saveRecipe: (recipe: Recipe) => change(async () => {
      const creating = !recipe.id;
      const saved = await api.request<Recipe>(creating ? 'recipes' : 'recipes/' + recipe.id, {
        method: creating ? 'POST' : 'PUT', body: { name: recipe.name, items: recipe.items },
      });
      update((state) => ({ ...state, recipes: [saved, ...state.recipes.filter((item) => item.id !== saved.id)] }));
    }),
    removeRecipe: (id: string) => change(async () => {
      await api.request('recipes/' + id, { method: 'DELETE' });
      update((state) => ({
        ...state, recipes: state.recipes.filter((recipe) => recipe.id !== id),
        favorites: state.favorites.filter((item) => item !== 'recipe:' + id),
      }));
    }),
    addWater: (date: string, delta: number) => change(async () => {
      const ml = Math.min(30000, Math.max(0, (get().water[date] ?? 0) + delta));
      const saved = await api.request<{ ml: number }>('water/' + date, { method: 'PUT', body: { ml } });
      update((state) => ({ ...state, water: { ...state.water, [date]: saved.ml } }));
    }),
    setWeight: (date: string, kg: number) => change(async () => {
      await api.request('weight/' + date, { method: 'PUT', body: { kg } });
      update((state) => withWeight(state, { ...state.weights, [date]: kg }));
    }),
    removeWeight: (date: string) => change(async () => {
      await api.request('weight/' + date, { method: 'DELETE' });
      update((state) => {
        const weights = { ...state.weights };
        delete weights[date];
        return withWeight(state, weights);
      });
    }),
  };
}

type Store = ReturnType<typeof actions> & {
  state: AppState;
  busy: boolean;
  loading: boolean;
  loadError: string | null;
  error: string | null;
  dismissError: () => void;
  reload: () => Promise<void>;
  refresh: () => Promise<void>;
  catalogRevision: number;
};

const Ctx = createContext<Store | null>(null);
const message = (error: unknown) => error instanceof Error ? error.message : 'Не удалось загрузить данные';

export function StoreProvider({ children }: { children: ReactNode }) {
  const [api] = useState(() => new ApiClient());
  const [state, setState] = useState<AppState>(emptyState);
  const current = useRef(state);
  const [busy, setBusy] = useState(false);
  const working = useRef(false);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [catalogRevision, setCatalogRevision] = useState(0);
  const refreshing = useRef<Promise<void> | null>(null);
  const booting = useRef<Promise<void> | null>(null);

  const update = useCallback<Update>((change) => {
    current.current = change(current.current);
    setState(current.current);
  }, []);

  const readAccount = useCallback(async () => {
    const account = await loadAccount(api);
    update(() => account);
    setCatalogRevision((revision) => revision + 1);
  }, [api, update]);

  const reload = useCallback(async () => {
    if (booting.current) return booting.current;
    setLoading(true);
    setLoadError(null);
    const task = (async () => {
      try {
        api.restore();
        if (api.hasSession) await readAccount();
      } catch (reason) {
        if (api.hasSession) setLoadError(message(reason));
        else setError(message(reason));
      } finally { setLoading(false); }
    })();
    booting.current = task;
    try { await task; } finally { booting.current = null; }
  }, [api, readAccount]);

  const refresh = useCallback(async () => {
    if (!api.hasSession || working.current || booting.current) return;
    if (refreshing.current) return refreshing.current;
    const task = (async () => {
      try { await readAccount(); } catch (reason) { setError(message(reason)); }
    })();
    refreshing.current = task;
    try { await task; } finally { refreshing.current = null; }
  }, [api, readAccount]);

  const run = useCallback<Run>(async (action) => {
    if (working.current) return undefined;
    working.current = true;
    setBusy(true);
    setError(null);
    try {
      await refreshing.current;
      const result = await action();
      setCatalogRevision((revision) => revision + 1);
      return result;
    } catch (reason) {
      setError(message(reason));
      return undefined;
    } finally {
      working.current = false;
      setBusy(false);
    }
  }, []);

  const methods = useMemo(() => actions(api, update, () => current.current, run), [api, update, run]);
  const login = useCallback(async (...args: Parameters<typeof methods.login>) => {
    if (!await methods.login(...args)) return false;
    await reload();
    return true;
  }, [methods, reload]);
  const logout = useCallback(async () => {
    const result = await methods.logout();
    if (!api.hasSession) setLoadError(null);
    return result;
  }, [api, methods]);

  useEffect(() => {
    api.onExpired = () => { update(() => emptyState()); setLoadError(null); };
    void reload();
    return () => { api.onExpired = undefined; };
  }, [api, reload, update]);

  useEffect(() => {
    if (!state.user || loading || loadError) return;
    const visibleRefresh = () => {
      if (document.visibilityState === 'visible') void refresh();
    };
    window.addEventListener('focus', visibleRefresh);
    document.addEventListener('visibilitychange', visibleRefresh);
    const interval = setInterval(visibleRefresh, 15000);
    return () => {
      window.removeEventListener('focus', visibleRefresh);
      document.removeEventListener('visibilitychange', visibleRefresh);
      clearInterval(interval);
    };
  }, [state.user?.id, loading, loadError, refresh]);

  const store = {
    state, busy, loading, loadError, error, catalogRevision,
    ...methods, login, logout, reload, refresh, dismissError: () => setError(null),
  };
  return <Ctx.Provider value={store}>{children}</Ctx.Provider>;
}

export function useStore(): Store {
  const store = useContext(Ctx);
  if (!store) throw new Error('useStore вне StoreProvider');
  return store;
}
