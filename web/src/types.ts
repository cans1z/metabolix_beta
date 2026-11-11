export type MealType = 'breakfast' | 'lunch' | 'dinner' | 'snack';
export type Sex = 'male' | 'female';
export type Goal = 'lose' | 'maintain' | 'gain';
export type Theme = 'system' | 'light' | 'dark';

/* пищевая ценность на 100 г или 100 мл */
export interface Food {
  id: string;
  name: string;
  kcal: number;
  protein: number;
  fat: number;
  carbs: number;
  unit: 'g' | 'ml';
  portion?: number; // размер стандартной порции
  barcode?: string;
  custom?: boolean;
}

export interface RecipeItem {
  food: Food;
  grams: number;
}

export interface Recipe {
  id: string;
  name: string;
  items: RecipeItem[];
}

/* снимок продукта сохраняет историю при изменении справочника */
export interface Entry {
  id: string;
  date: string; // yyyy-mm-dd
  meal: MealType;
  food: Food;
  grams: number;
  updatedAt: string;
}

export interface Profile {
  name: string;
  sex: Sex;
  birthDate: string; // yyyy-mm-dd
  height: number; // см
  weight: number; // кг
  targetWeight: number; // кг
  activity: number; // коэффициент активности 1.2–1.9
  goal: Goal;
}

export interface Macros {
  kcal: number;
  protein: number;
  fat: number;
  carbs: number;
}

export interface Settings {
  theme: Theme;
  /* доли калорий в сумме дают 100 процентов */
  ratio: { protein: number; fat: number; carbs: number };
  /* ручная норма заменяет расчётную */
  manualTargets: Macros | null;
  waterGoal: number; // мл
  reminders: { meal: boolean; water: boolean; weight: boolean; time: string };
}

export interface User {
  id: string;
  email: string;
  name: string;
  hasProfile: boolean;
}

export interface AppState {
  user: User | null;
  guest: boolean;
  profile: Profile | null;
  foods: Food[];
  recipes: Recipe[];
  entries: Entry[];
  favorites: string[];
  recents: string[];
  water: Record<string, number>; // дата -> мл
  weights: Record<string, number>; // дата -> кг
  settings: Settings;
}
