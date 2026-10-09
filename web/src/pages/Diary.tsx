import { useState } from 'react';
import { AmountPicker, FoodCardModal, FoodPicker } from '../components/food';
import { Field, MacroBar, Modal, Ring } from '../components/ui';
import { MEALS, macrosFor, sumEntries, targetsFor } from '../lib/nutrition';
import { addDays, fmtDate, r0, today } from '../lib/util';
import { useStore } from '../store';
import type { Entry, Food, MealType } from '../types';

const GLASS = 250;

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
  const left = r0(target.kcal - total.kcal);
  const water = state.water[date] ?? 0;

  return (
    <div className="page">
      <div className="date-nav">
        <button className="icon-btn" onClick={() => setDate(addDays(date, -1))} aria-label="Предыдущий день">
          ‹
        </button>
        <label className="date-label">
          {fmtDate(date)}
          <input type="date" value={date} onChange={(e) => e.target.value && setDate(e.target.value)} />
        </label>
        <button className="icon-btn" onClick={() => setDate(addDays(date, 1))} aria-label="Следующий день">
          ›
        </button>
      </div>

      <div className="card summary">
        <Ring value={total.kcal} max={target.kcal}>
          <b className="ring-num">{Math.abs(left)}</b>
          <span className="muted small">{left >= 0 ? 'ккал осталось' : 'ккал перебор'}</span>
        </Ring>
        <div className="grow">
          <div className="muted small">
            Съедено <b>{r0(total.kcal)}</b> из {target.kcal} ккал
          </div>
          <MacroBar label="Белки" value={total.protein} max={target.protein} color="var(--protein)" />
          <MacroBar label="Жиры" value={total.fat} max={target.fat} color="var(--fat)" />
          <MacroBar label="Углеводы" value={total.carbs} max={target.carbs} color="var(--carbs)" />
        </div>
      </div>

      {MEALS.map((meal) => {
        const entries = dayEntries.filter((e) => e.meal === meal.id);
        return (
          <div className="card" key={meal.id}>
            <div className="card-head">
              <h3>
                {meal.icon} {meal.label}
              </h3>
              <span className="muted">{r0(sumEntries(entries).kcal)} ккал</span>
            </div>
            <div className="list">
              {entries.map((e) => {
                const m = macrosFor(e.food, e.grams);
                return (
                  <div className="list-row" key={e.id} onClick={() => setEditing(e)}>
                    <div className="grow">
                      <div>{e.food.name}</div>
                      <div className="muted small">
                        {e.grams} {e.food.unit === 'ml' ? 'мл' : 'г'} · Б {r0(m.protein)} · Ж {r0(m.fat)} · У{' '}
                        {r0(m.carbs)}
                      </div>
                    </div>
                    <b>{r0(m.kcal)}</b>
                    <button
                      className="icon-btn"
                      title="Удалить"
                      onClick={(ev) => {
                        ev.stopPropagation();
                        removeEntry(e.id);
                      }}
                    >
                      ✕
                    </button>
                  </div>
                );
              })}
            </div>
            <div className="card-foot">
              <button className="btn primary" onClick={() => setAdding(meal.id)}>
                + Добавить
              </button>
              {entries.length > 0 && (
                <button className="btn" onClick={() => setCopying(meal.id)}>
                  Копировать на другой день
                </button>
              )}
            </div>
          </div>
        );
      })}

      <div className="card">
        <div className="card-head">
          <h3>💧 Вода</h3>
          <span className="muted">
            {water} / {state.settings.waterGoal} мл
          </span>
        </div>
        <div className="bar">
          <div
            style={{ width: Math.min(100, (water / state.settings.waterGoal) * 100) + '%', background: 'var(--water)' }}
          />
        </div>
        <div className="card-foot">
          <button className="btn" onClick={() => addWater(date, -GLASS)} disabled={water === 0}>
            − {GLASS} мл
          </button>
          <button className="btn primary" onClick={() => addWater(date, GLASS)}>
            + {GLASS} мл
          </button>
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
