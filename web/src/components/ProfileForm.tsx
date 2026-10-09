import { useState } from 'react';
import { ACTIVITY, GOALS } from '../lib/nutrition';
import type { Profile } from '../types';
import { Field, Segmented, Stepper } from './ui';

export const DEFAULT_PROFILE: Profile = {
  name: '',
  sex: 'male',
  birthDate: '2000-01-01',
  height: 175,
  weight: 75,
  targetWeight: 70,
  activity: 1.375,
  goal: 'lose',
};

export function ProfileForm(props: { initial: Profile; submitLabel: string; onSubmit: (p: Profile) => void }) {
  const [p, setP] = useState<Profile>(props.initial);
  const set = <K extends keyof Profile>(k: K, v: Profile[K]) => setP((prev) => ({ ...prev, [k]: v }));
  return (
    <form
      className="form"
      onSubmit={(e) => {
        e.preventDefault();
        props.onSubmit(p);
      }}
    >
      <div className="grid2">
        <Field label="Имя">
          <input className="input" value={p.name} onChange={(e) => set('name', e.target.value)} />
        </Field>
        <Field label="Дата рождения">
          <input
            className="input"
            type="date"
            value={p.birthDate}
            max="2020-12-31"
            onChange={(e) => e.target.value && set('birthDate', e.target.value)}
          />
        </Field>
      </div>
      <Field label="Пол">
        <Segmented
          value={p.sex}
          options={[
            { value: 'male', label: 'Мужской' },
            { value: 'female', label: 'Женский' },
          ]}
          onChange={(v) => set('sex', v)}
        />
      </Field>
      <Field label="Рост">
        <Stepper value={p.height} onChange={(v) => set('height', v)} min={120} max={220} unit="см" slider />
      </Field>
      <div className="grid2">
        <Field label="Текущий вес">
          <Stepper value={p.weight} onChange={(v) => set('weight', v)} step={0.5} min={30} max={250} unit="кг" />
        </Field>
        <Field label="Целевой вес">
          <Stepper
            value={p.targetWeight}
            onChange={(v) => set('targetWeight', v)}
            step={0.5}
            min={30}
            max={250}
            unit="кг"
          />
        </Field>
      </div>
      <Field label="Цель">
        <Segmented value={p.goal} options={GOALS} onChange={(v) => set('goal', v)} />
      </Field>
      <Field label="Физическая активность">
        <div className="options">
          {ACTIVITY.map((a) => (
            <button
              type="button"
              key={a.value}
              className={'option' + (p.activity === a.value ? ' active' : '')}
              onClick={() => set('activity', a.value)}
            >
              <b>{a.label}</b>
              <span className="muted small">
                {a.hint} · ×{a.value}
              </span>
            </button>
          ))}
        </div>
      </Field>
      <div className="actions">
        <button className="btn primary">{props.submitLabel}</button>
      </div>
    </form>
  );
}
