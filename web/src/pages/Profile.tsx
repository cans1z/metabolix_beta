import { useState } from 'react';
import { ProfileForm } from '../components/ProfileForm';
import { Icon } from '../components/icons';
import { Field, Segmented, Stepper, Toggle } from '../components/ui';
import { MEALS, autoTargets, bmr, macrosFor, tdee } from '../lib/nutrition';
import { addDays, r0, r1, today } from '../lib/util';
import { useStore } from '../store';
import type { Entry, Macros, Theme } from '../types';

function exportCsv(entries: Entry[], from: string, to: string) {
  const rows = entries
    .filter((e) => e.date >= from && e.date <= to)
    .sort((a, b) => a.date.localeCompare(b.date))
    .map((e) => {
      const m = macrosFor(e.food, e.grams);
      const meal = MEALS.find((x) => x.id === e.meal)!.label;
      return [e.date, meal, `"${e.food.name.replace(/"/g, '""')}"`, e.grams, r0(m.kcal), r1(m.protein), r1(m.fat), r1(m.carbs)].join(';');
    });
  const csv = '﻿' + ['Дата;Приём пищи;Продукт;Количество;Ккал;Белки;Жиры;Углеводы', ...rows].join('\r\n');
  const a = document.createElement('a');
  a.href = URL.createObjectURL(new Blob([csv], { type: 'text/csv;charset=utf-8' }));
  a.download = `metabolix_${from}_${to}.csv`;
  a.click();
  URL.revokeObjectURL(a.href);
}

export function Profile() {
  const { state, saveProfile, updateSettings, logout, seedDemo, resetAll } = useStore();
  const profile = state.profile!;
  const s = state.settings;
  const auto = autoTargets(profile, s.ratio);
  const [saved, setSaved] = useState(false);
  const [from, setFrom] = useState(addDays(today(), -29));
  const [to, setTo] = useState(today());

  const setRatio = (protein: number, fat: number) => {
    const p = Math.min(protein, 100 - fat);
    updateSettings({ ratio: { protein: p, fat, carbs: 100 - p - fat } });
  };
  const setManual = (k: keyof Macros, v: number) => updateSettings({ manualTargets: { ...(s.manualTargets ?? auto), [k]: v } });
  const remind = (patch: Partial<typeof s.reminders>) => updateSettings({ reminders: { ...s.reminders, ...patch } });

  return (
    <div className="page">
      <header className="page-head">
        <div>
          <div className="eyebrow">{state.user?.email}</div>
          <h1>Профиль</h1>
        </div>
        <button className="btn" onClick={logout}>
          <Icon name="logout" size={18} />
          Выйти
        </button>
      </header>

      <div className="card">
        <h3>Анкета</h3>
        <ProfileForm
          initial={profile}
          submitLabel={saved ? 'Сохранено' : 'Сохранить'}
          onSubmit={(p) => {
            saveProfile(p);
            setSaved(true);
            setTimeout(() => setSaved(false), 1500);
          }}
        />
      </div>

      <div className="card">
        <h3>Норма КБЖУ</h3>
        <div className="tiles">
          <div className="tile">
            <b>{r0(bmr(profile))}</b>
            <span>основной обмен</span>
          </div>
          <div className="tile">
            <b>{r0(tdee(profile))}</b>
            <span>расход (TDEE)</span>
          </div>
          <div className="tile">
            <b>{auto.kcal}</b>
            <span>норма с учётом цели</span>
          </div>
        </div>
        <Field label={`Белки — ${s.ratio.protein}% · ${auto.protein} г`}>
          <input type="range" min={10} max={60} step={5} value={s.ratio.protein} onChange={(e) => setRatio(Number(e.target.value), s.ratio.fat)} />
        </Field>
        <Field label={`Жиры — ${s.ratio.fat}% · ${auto.fat} г`}>
          <input type="range" min={10} max={60} step={5} value={s.ratio.fat} onChange={(e) => setRatio(s.ratio.protein, Number(e.target.value))} />
        </Field>
        <div className="muted small">
          Углеводы — {s.ratio.carbs}% · {auto.carbs} г
        </div>
        <Toggle
          label="Задать норму вручную"
          checked={!!s.manualTargets}
          onChange={(on) => updateSettings({ manualTargets: on ? auto : null })}
        />
        {s.manualTargets && (
          <div className="grid2">
            <Field label="Калории">
              <Stepper value={s.manualTargets.kcal} onChange={(v) => setManual('kcal', v)} step={50} min={800} max={6000} unit="ккал" />
            </Field>
            <Field label="Белки">
              <Stepper value={s.manualTargets.protein} onChange={(v) => setManual('protein', v)} step={5} max={500} unit="г" />
            </Field>
            <Field label="Жиры">
              <Stepper value={s.manualTargets.fat} onChange={(v) => setManual('fat', v)} step={5} max={500} unit="г" />
            </Field>
            <Field label="Углеводы">
              <Stepper value={s.manualTargets.carbs} onChange={(v) => setManual('carbs', v)} step={5} max={900} unit="г" />
            </Field>
          </div>
        )}
      </div>

      <div className="card">
        <h3>Настройки</h3>
        <Field label="Тема оформления">
          <Segmented<Theme>
            value={s.theme}
            options={[
              { value: 'system', label: 'Как в системе' },
              { value: 'light', label: 'Светлая' },
              { value: 'dark', label: 'Тёмная' },
            ]}
            onChange={(theme) => updateSettings({ theme })}
          />
        </Field>
        <Field label="Норма воды в день">
          <Stepper value={s.waterGoal} onChange={(v) => updateSettings({ waterGoal: v })} step={250} min={500} max={6000} unit="мл" />
        </Field>
        <Field label="Напоминания">
          <Toggle label="Внести приём пищи" checked={s.reminders.meal} onChange={(meal) => remind({ meal })} />
          <Toggle label="Выпить воды" checked={s.reminders.water} onChange={(water) => remind({ water })} />
          <Toggle label="Записать вес" checked={s.reminders.weight} onChange={(weight) => remind({ weight })} />
        </Field>
        <Field label="Время напоминания">
          <input className="input narrow" type="time" value={s.reminders.time} onChange={(e) => remind({ time: e.target.value })} />
        </Field>
      </div>

      <div className="card">
        <h3>Данные</h3>
        <div className="grid2">
          <Field label="Экспорт с">
            <input className="input" type="date" value={from} max={to} onChange={(e) => e.target.value && setFrom(e.target.value)} />
          </Field>
          <Field label="по">
            <input className="input" type="date" value={to} min={from} onChange={(e) => e.target.value && setTo(e.target.value)} />
          </Field>
        </div>
        <div className="card-foot">
          <button className="btn" onClick={() => exportCsv(state.entries, from, to)}>
            Экспорт дневника в CSV
          </button>
          <button className="btn" onClick={seedDemo}>
            Заполнить демо-данными
          </button>
          <button className="btn danger" onClick={() => confirm('Удалить все данные приложения?') && resetAll()}>
            Сбросить всё
          </button>
        </div>
      </div>
    </div>
  );
}
