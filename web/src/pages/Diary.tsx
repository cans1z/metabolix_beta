import { useState } from 'react';
import { AmountPicker, FoodCardModal, FoodPicker } from '../components/food';
import { Icon } from '../components/icons';
import { Field, MacroBar, Meter, Modal } from '../components/ui';
import { MEALS, macrosFor, sumEntries, targetsFor } from '../lib/nutrition';
import { addDays, clamp, n0, parse, r0, today } from '../lib/util';
import { useStore } from '../store';
import type { Entry, Food, MealType } from '../types';

const GLASS = 250;

function dayTitle(date: string) {
  const d = parse(date);
  const weekday = d.toLocaleDateString('ru-RU', { weekday: 'long' });
  const label = date === today() ? 'Сегодня' : date === addDays(today(), -1) ? 'Вчера' : '';
  return {
    eyebrow: label ? `${label}, ${weekday}` : weekday[0].toUpperCase() + weekday.slice(1),
    title: d.toLocaleDateString('ru-RU', { day: 'numeric', month: 'long' }),
  };
}

export function Diary() {
  const { state, removeEntry, updateEntry, copyMeal, addWater } = useStore();
  const [date, setDate] = useState(today());
  const [adding, setAdding] = useState<MealType | null>(null);
  const [picked, setPicked] = useState<Food | null>(null);
  const [editing, setEditing] = useState<Entry | null>(null);
  const [copying, setCopying] = useState<MealType | null>(null);
  const [copyTo, setCopyTo] = useState(addDays(today(), 1));

  const target = targetsFor(state.profile!, state.settings);
  const dayEntries = state.entries.filter((e) => e.date === date);
  const total = sumEntries(dayEntries);
  const left = target.kcal - total.kcal;
  const water = state.water[date] ?? 0;
  const goal = state.settings.waterGoal;
  const filled = Math.floor(water / GLASS);
  const cups = Math.max(filled, clamp(Math.ceil(goal / GLASS), 1, 16));
  const head = dayTitle(date);

  return (
    <div className="page">
      <header className="page-head">
        <div>
          <div className="eyebrow">{head.eyebrow}</div>
          <h1>{head.title}</h1>
        </div>
        <div className="day-nav">
          {date !== today() && (
            <button className="btn small" onClick={() => setDate(today())}>
              К сегодня
            </button>
          )}
          <button className="icon-btn" onClick={() => setDate(addDays(date, -1))} aria-label="Предыдущий день">
            <Icon name="left" />
          </button>
          <label className="icon-btn date-pick" title="Выбрать дату">
            <Icon name="calendar" />
            <input type="date" value={date} onChange={(e) => e.target.value && setDate(e.target.value)} />
          </label>
          <button className="icon-btn" onClick={() => setDate(addDays(date, 1))} aria-label="Следующий день">
            <Icon name="right" />
          </button>
        </div>
      </header>

      <div className="diary-grid">
        <aside className="side">
          <section className="panel">
            <div className="panel-title">Калории</div>
            <div className="big-number">
              {n0(Math.abs(left))}
              <span>{left >= 0 ? 'ккал осталось' : 'ккал сверх нормы'}</span>
            </div>
            <Meter value={total.kcal} max={target.kcal} color="var(--accent)" />
            <div className="equation">
              <span>
                <b>{n0(target.kcal)}</b>
                норма
              </span>
              <span className="op">−</span>
              <span>
                <b>{n0(total.kcal)}</b>
                съедено
              </span>
              <span className="op">=</span>
              <span>
                <b>{n0(left)}</b>
                остаток
              </span>
            </div>
            <div className="macros">
              <MacroBar label="Белки" value={total.protein} max={target.protein} color="var(--protein)" />
              <MacroBar label="Жиры" value={total.fat} max={target.fat} color="var(--fat)" />
              <MacroBar label="Углеводы" value={total.carbs} max={target.carbs} color="var(--carbs)" />
            </div>
          </section>

          <section className="panel">
            <div className="panel-title">
              Вода
              <span className="muted">
                {n0(water)} из {n0(goal)} мл
              </span>
            </div>
            <div className="cups">
              {Array.from({ length: cups }, (_, i) => (
                <button
                  key={i}
                  className={'cup' + (i < filled ? ' full' : '')}
                  // нажатие на последний полный стакан снимает его
                  onClick={() => addWater(date, (i === filled - 1 ? i : i + 1) * GLASS - water)}
                  aria-label={`${(i + 1) * GLASS} мл`}
                >
                  <Icon name="cup" size={22} />
                </button>
              ))}
            </div>
            <div className="muted small">Один стакан — {GLASS} мл</div>
          </section>
        </aside>

        <div className="meals">
          {MEALS.map((meal) => {
            const entries = dayEntries.filter((e) => e.meal === meal.id);
            return (
              <section className="meal" key={meal.id}>
                <header className="meal-head">
                  <h2>{meal.label}</h2>
                  {entries.length > 0 && <span className="muted">{n0(sumEntries(entries).kcal)} ккал</span>}
                  <div className="grow" />
                  {entries.length > 0 && (
                    <button className="icon-btn" title="Копировать на другой день" onClick={() => setCopying(meal.id)}>
                      <Icon name="copy" size={18} />
                    </button>
                  )}
                  <button className="btn small" onClick={() => setAdding(meal.id)}>
                    <Icon name="plus" size={16} />
                    Добавить
                  </button>
                </header>
                {entries.length === 0 ? (
                  <p className="meal-empty">Ничего не записано</p>
                ) : (
                  <ul className="entries">
                    {entries.map((e) => {
                      const m = macrosFor(e.food, e.grams);
                      return (
                        <li key={e.id} onClick={() => setEditing(e)}>
                          <div className="grow">
                            <div className="entry-name">{e.food.name}</div>
                            <div className="entry-meta">
                              {e.grams} {e.food.unit === 'ml' ? 'мл' : 'г'} · Б {r0(m.protein)} · Ж {r0(m.fat)} · У{' '}
                              {r0(m.carbs)}
                            </div>
                          </div>
                          <span className="entry-kcal">{n0(m.kcal)}</span>
                          <button
                            className="icon-btn row-action"
                            title="Удалить"
                            onClick={(ev) => {
                              ev.stopPropagation();
                              removeEntry(e.id);
                            }}
                          >
                            <Icon name="close" size={18} />
                          </button>
                        </li>
                      );
                    })}
                  </ul>
                )}
              </section>
            );
          })}
        </div>
      </div>

      {adding && !picked && (
        <Modal title={MEALS.find((m) => m.id === adding)!.label} onClose={() => setAdding(null)}>
          <FoodPicker onPick={setPicked} />
        </Modal>
      )}
      {adding && picked && (
        <FoodCardModal
          food={picked}
          date={date}
          meal={adding}
          onClose={() => {
            setPicked(null);
            setAdding(null);
          }}
        />
      )}
      {editing && (
        <Modal title={editing.food.name} onClose={() => setEditing(null)}>
          <AmountPicker food={editing.food} grams={editing.grams} onChange={(g) => setEditing({ ...editing, grams: g })} />
          <div className="actions">
            <button
              className="btn danger"
              onClick={() => {
                removeEntry(editing.id);
                setEditing(null);
              }}
            >
              Удалить
            </button>
            <button
              className="btn primary"
              onClick={() => {
                updateEntry(editing.id, editing.grams);
                setEditing(null);
              }}
            >
              Сохранить
            </button>
          </div>
        </Modal>
      )}
      {copying && (
        <Modal title="Копировать приём пищи" onClose={() => setCopying(null)}>
          <Field label="На какую дату">
            <input
              className="input"
              type="date"
              value={copyTo}
              onChange={(e) => e.target.value && setCopyTo(e.target.value)}
            />
          </Field>
          <div className="actions">
            <button
              className="btn primary"
              disabled={copyTo === date}
              onClick={() => {
                copyMeal(date, copying, copyTo);
                setCopying(null);
                setDate(copyTo);
              }}
            >
              Скопировать
            </button>
          </div>
        </Modal>
      )}
    </div>
  );
}
