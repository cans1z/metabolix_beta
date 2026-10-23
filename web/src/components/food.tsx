import { useMemo, useState } from 'react';
import { MEALS, macrosFor, recipeToFood } from '../lib/nutrition';
import { r0, r1, today, uid } from '../lib/util';
import { useStore } from '../store';
import type { Food, MealType } from '../types';
import { Field, Modal, Segmented, Stepper } from './ui';

type Tab = 'all' | 'favorites' | 'recents' | 'recipes';

const TABS: { value: Tab; label: string }[] = [
  { value: 'all', label: 'Все' },
  { value: 'recents', label: 'Недавние' },
  { value: 'favorites', label: 'Избранное' },
  { value: 'recipes', label: 'Мои блюда' },
];

export const perLabel = (f: Food) => `на 100 ${f.unit === 'ml' ? 'мл' : 'г'}`;

/** Поиск по базе с вкладками «недавние / избранное / блюда». */
export function FoodPicker(props: { onPick: (f: Food) => void; onEditRecipe?: (id: string) => void }) {
  const { state, toggleFavorite } = useStore();
  const [tab, setTab] = useState<Tab>('all');
  const [query, setQuery] = useState('');

  const list = useMemo(() => {
    const byId = new Map(state.foods.map((f) => [f.id, f]));
    const recipes = state.recipes.map(recipeToFood);
    recipes.forEach((f) => byId.set(f.id, f));
    const ids = (xs: string[]) => xs.map((id) => byId.get(id)).filter((f): f is Food => !!f);
    const source =
      tab === 'all'
        ? state.foods
        : tab === 'recipes'
          ? recipes
          : ids(tab === 'favorites' ? state.favorites : state.recents);
    const q = query.trim().toLowerCase();
    return q ? source.filter((f) => f.name.toLowerCase().includes(q)) : source;
  }, [state.foods, state.recipes, state.favorites, state.recents, tab, query]);

  return (
    <div className="picker">
      <input
        className="input"
        placeholder="Поиск продукта…"
        value={query}
        onChange={(e) => setQuery(e.target.value)}
        autoFocus
      />
      <Segmented value={tab} options={TABS} onChange={setTab} />
      <div className="list">
        {list.length === 0 && <div className="empty">Ничего не найдено</div>}
        {list.map((f) => {
          const isRecipe = f.id.startsWith('recipe:');
          return (
            <div className="list-row" key={f.id} onClick={() => props.onPick(f)}>
              <div className="grow">
                <div>{f.name}</div>
                <div className="muted small">
                  {r0(f.kcal)} ккал · Б {r1(f.protein)} · Ж {r1(f.fat)} · У {r1(f.carbs)} · {perLabel(f)}
                </div>
              </div>
              {isRecipe && props.onEditRecipe && (
                <button
                  className="icon-btn"
                  title="Изменить блюдо"
                  onClick={(e) => {
                    e.stopPropagation();
                    props.onEditRecipe!(f.id.slice(7));
                  }}
                >
                  ✎
                </button>
              )}
              <button
                className={'icon-btn star' + (state.favorites.includes(f.id) ? ' on' : '')}
                title="Избранное"
                onClick={(e) => {
                  e.stopPropagation();
                  toggleFavorite(f.id);
                }}
              >
                {state.favorites.includes(f.id) ? '★' : '☆'}
              </button>
            </div>
          );
        })}
      </div>
    </div>
  );
}

/** Ввод количества: граммы или стандартные порции, с пересчётом КБЖУ. */
export function AmountPicker(props: { food: Food; grams: number; onChange: (g: number) => void }) {
  const { food, grams, onChange } = props;
  const [mode, setMode] = useState<'grams' | 'portions'>('grams');
  const unit = food.unit === 'ml' ? 'мл' : 'г';
  const m = macrosFor(food, grams);
  return (
    <div className="amount">
      {food.portion && (
        <Segmented
          value={mode}
          options={[
            { value: 'grams', label: unit === 'мл' ? 'Миллилитры' : 'Граммы' },
            { value: 'portions', label: `Порции (${food.portion} ${unit})` },
          ]}
          onChange={setMode}
        />
      )}
      {mode === 'portions' && food.portion ? (
        <Stepper
          value={r1(grams / food.portion)}
          onChange={(v) => onChange(Math.round(v * food.portion!))}
          step={0.5}
          min={0.5}
          max={20}
          unit="порц."
        />
      ) : (
        <Stepper value={grams} onChange={onChange} step={10} min={1} max={1000} unit={unit} slider />
      )}
      <div className="tiles">
        <div className="tile">
          <b>{r0(m.kcal)}</b>
          <span>ккал</span>
        </div>
        <div className="tile">
          <b>{r1(m.protein)}</b>
          <span>белки</span>
        </div>
        <div className="tile">
          <b>{r1(m.fat)}</b>
          <span>жиры</span>
        </div>
        <div className="tile">
          <b>{r1(m.carbs)}</b>
          <span>углеводы</span>
        </div>
      </div>
    </div>
  );
}

/** Карточка продукта с добавлением в дневник. */
export function FoodCardModal(props: { food: Food; date?: string; meal?: MealType; onClose: () => void }) {
  const { state, addEntry, removeFood } = useStore();
  const { food } = props;
  const [grams, setGrams] = useState(food.portion ?? 100);
  const [meal, setMeal] = useState<MealType>(props.meal ?? 'breakfast');
  const canLog = !!state.user && !!state.profile;
  return (
    <Modal title={food.name} onClose={props.onClose}>
      <div className="muted small">
        {perLabel(food)}: {r0(food.kcal)} ккал · Б {r1(food.protein)} · Ж {r1(food.fat)} · У {r1(food.carbs)}
        {food.barcode && <> · штрихкод {food.barcode}</>}
      </div>
      {canLog ? (
        <>
          <AmountPicker food={food} grams={grams} onChange={setGrams} />
          <Field label="Приём пищи">
            <Segmented value={meal} options={MEALS.map((m) => ({ value: m.id, label: m.label }))} onChange={setMeal} />
          </Field>
          <div className="actions">
            {food.custom && (
              <button
                className="btn danger"
                onClick={() => {
                  removeFood(food.id);
                  props.onClose();
                }}
              >
                Удалить продукт
              </button>
            )}
            <button
              className="btn primary"
              onClick={() => {
                addEntry(props.date ?? today(), meal, food, grams);
                props.onClose();
              }}
            >
              Добавить в дневник
            </button>
          </div>
        </>
      ) : (
        <div className="empty">Войдите в аккаунт, чтобы добавлять продукты в дневник.</div>
      )}
    </Modal>
  );
}

const num = (s: string) => Number(s.replace(',', '.')) || 0;

/** Создание карточки продукта вручную (в т.ч. когда штрихкод не найден). */
export function FoodFormModal(props: { barcode?: string; onClose: () => void; onSaved?: (f: Food) => void }) {
  const { saveFood } = useStore();
  const [f, setF] = useState({
    name: '',
    kcal: '',
    protein: '',
    fat: '',
    carbs: '',
    portion: '',
    barcode: props.barcode ?? '',
    unit: 'g' as Food['unit'],
  });
  const upd = (k: keyof typeof f) => (e: React.ChangeEvent<HTMLInputElement>) => setF({ ...f, [k]: e.target.value });
  const valid = f.name.trim() !== '' && f.kcal !== '';
  return (
    <Modal title="Новый продукт" onClose={props.onClose}>
      <form
        className="form"
        onSubmit={(e) => {
          e.preventDefault();
          if (!valid) return;
          const food: Food = {
            id: uid(),
            name: f.name.trim(),
            kcal: num(f.kcal),
            protein: num(f.protein),
            fat: num(f.fat),
            carbs: num(f.carbs),
            unit: f.unit,
            portion: num(f.portion) || undefined,
            barcode: f.barcode.trim() || undefined,
            custom: true,
          };
          saveFood(food);
          props.onSaved?.(food);
          props.onClose();
        }}
      >
        <Field label="Название">
          <input className="input" value={f.name} onChange={upd('name')} autoFocus />
        </Field>
        <Field label="Единица">
          <Segmented
            value={f.unit}
            options={[
              { value: 'g', label: 'на 100 г' },
              { value: 'ml', label: 'на 100 мл' },
            ]}
            onChange={(unit) => setF({ ...f, unit })}
          />
        </Field>
        <div className="grid4">
          <Field label="Ккал">
            <input className="input" inputMode="decimal" value={f.kcal} onChange={upd('kcal')} />
          </Field>
          <Field label="Белки">
            <input className="input" inputMode="decimal" value={f.protein} onChange={upd('protein')} />
          </Field>
          <Field label="Жиры">
            <input className="input" inputMode="decimal" value={f.fat} onChange={upd('fat')} />
          </Field>
          <Field label="Углеводы">
            <input className="input" inputMode="decimal" value={f.carbs} onChange={upd('carbs')} />
          </Field>
        </div>
        <div className="grid2">
          <Field label="Порция (необязательно)">
            <input className="input" inputMode="decimal" value={f.portion} onChange={upd('portion')} />
          </Field>
          <Field label="Штрихкод (необязательно)">
            <input className="input" inputMode="numeric" value={f.barcode} onChange={upd('barcode')} />
          </Field>
        </div>
        <div className="actions">
          <button className="btn primary" disabled={!valid}>
            Сохранить
          </button>
        </div>
      </form>
    </Modal>
  );
}

/** Поиск по штрихкоду. В вебе код вводится вручную; в мобильном приложении — камерой. */
export function BarcodeModal(props: { onClose: () => void; onFound: (f: Food) => void; onCreate: (code: string) => void }) {
  const { state } = useStore();
  const [code, setCode] = useState('');
  const [missing, setMissing] = useState(false);
  const demo = state.foods.filter((f) => f.barcode).slice(0, 3);
  const search = (c: string) => {
    const found = state.foods.find((f) => f.barcode === c.trim());
    if (found) props.onFound(found);
    else setMissing(true);
  };
  return (
    <Modal title="Штрихкод" onClose={props.onClose}>
      <form
        className="form"
        onSubmit={(e) => {
          e.preventDefault();
          search(code);
        }}
      >
        <Field label="Код с упаковки">
          <input
            className="input"
            inputMode="numeric"
            placeholder="4600000000011"
            value={code}
            onChange={(e) => {
              setCode(e.target.value);
              setMissing(false);
            }}
            autoFocus
          />
        </Field>
        <div className="muted small">
          Для проверки:{' '}
          {demo.map((f) => (
            <button type="button" className="chip" key={f.id} onClick={() => setCode(f.barcode!)}>
              {f.barcode}
            </button>
          ))}
        </div>
        {missing && (
          <div className="notice">
            Продукт не найден.{' '}
            <button type="button" className="link" onClick={() => props.onCreate(code.trim())}>
              Создать карточку
            </button>
          </div>
        )}
        <div className="actions">
          <button className="btn primary" disabled={!code.trim()}>
            Найти
          </button>
        </div>
      </form>
    </Modal>
  );
}
