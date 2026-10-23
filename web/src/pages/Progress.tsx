import { useState } from 'react';
import { Icon } from '../components/icons';
import { Field, Segmented, Stepper } from '../components/ui';
import { ZERO, add, bmi, bmiLabel, sumEntries, targetsFor } from '../lib/nutrition';
import { addDays, clamp, dateRange, fmtShort, n0, parse, r0, r1, today } from '../lib/util';
import { useStore } from '../store';
import type { Macros } from '../types';

type Period = 'week' | 'month' | 'custom';
type Metric = 'kcal' | 'macros';

const W = 640;
const H = 180;
const PAD = 8;

function KcalChart(props: { days: string[]; data: Macros[]; target: number; metric: Metric }) {
  const { days, data, target, metric } = props;
  const value = (m: Macros) => (metric === 'kcal' ? m.kcal : m.protein + m.fat + m.carbs);
  const max = Math.max(metric === 'kcal' ? target : 0, ...data.map(value), 1) * 1.1;
  const slot = (W - PAD * 2) / days.length;
  const bw = clamp(slot * 0.45, 2, 22);
  const y = (v: number) => H - 22 - (v / max) * (H - 30);
  const labelEvery = Math.ceil(days.length / 10);
  return (
    <svg viewBox={`0 0 ${W} ${H}`} className="chart" role="img" aria-label="Потребление по дням">
      {days.map((d, i) => {
        const x = PAD + slot * i + (slot - bw) / 2;
        const m = data[i];
        const parts =
          metric === 'kcal'
            ? [{ v: m.kcal, c: m.kcal > target ? 'var(--danger)' : 'var(--accent)' }]
            : [
                { v: m.protein, c: 'var(--protein)' },
                { v: m.fat, c: 'var(--fat)' },
                { v: m.carbs, c: 'var(--carbs)' },
              ];
        let acc = 0;
        return (
          <g key={d}>
            <title>
              {fmtShort(d)}: {r0(m.kcal)} ккал · Б {r0(m.protein)} · Ж {r0(m.fat)} · У {r0(m.carbs)}
            </title>
            {parts.map((p, k) => {
              const top = y(acc + p.v);
              const h = y(acc) - top;
              acc += p.v;
              return <rect key={k} x={x} y={top} width={bw} height={Math.max(h, 0)} rx={2} fill={p.c} />;
            })}
            {i % labelEvery === 0 && (
              <text x={x + bw / 2} y={H - 6} textAnchor="middle" className="axis">
                {parse(d).getDate()}
              </text>
            )}
          </g>
        );
      })}
      {metric === 'kcal' && (
        <line x1={PAD} x2={W - PAD} y1={y(target)} y2={y(target)} stroke="var(--muted)" strokeDasharray="4 4" />
      )}
    </svg>
  );
}

function WeightChart(props: { points: { date: string; kg: number }[]; target: number }) {
  const { points, target } = props;
  if (points.length < 2) return <div className="empty">Нужно хотя бы две записи веса за период</div>;
  const values = [...points.map((p) => p.kg), target];
  const min = Math.min(...values) - 0.5;
  const max = Math.max(...values) + 0.5;
  const t0 = parse(points[0].date).getTime();
  const span = parse(points[points.length - 1].date).getTime() - t0 || 1;
  const x = (d: string) => PAD + 30 + ((parse(d).getTime() - t0) / span) * (W - PAD * 2 - 40);
  const y = (v: number) => H - 22 - ((v - min) / (max - min)) * (H - 34);
  return (
    <svg viewBox={`0 0 ${W} ${H}`} className="chart" role="img" aria-label="Динамика веса">
      <line x1={PAD} x2={W - PAD} y1={y(target)} y2={y(target)} stroke="var(--muted)" strokeDasharray="4 4" />
      <text x={PAD} y={y(target) - 4} className="axis">
        цель {target}
      </text>
      <polyline
        fill="none"
        stroke="var(--accent)"
        strokeWidth={2.5}
        strokeLinejoin="round"
        points={points.map((p) => `${x(p.date)},${y(p.kg)}`).join(' ')}
      />
      {points.map((p) => (
        <circle key={p.date} cx={x(p.date)} cy={y(p.kg)} r={3.5} fill="var(--accent)">
          <title>
            {fmtShort(p.date)}: {p.kg} кг
          </title>
        </circle>
      ))}
      <text x={x(points[0].date)} y={H - 6} textAnchor="middle" className="axis">
        {fmtShort(points[0].date)}
      </text>
      <text x={x(points[points.length - 1].date)} y={H - 6} textAnchor="middle" className="axis">
        {fmtShort(points[points.length - 1].date)}
      </text>
    </svg>
  );
}

export function Progress() {
  const { state, setWeight, removeWeight } = useStore();
  const profile = state.profile!;
  const [period, setPeriod] = useState<Period>('week');
  const [metric, setMetric] = useState<Metric>('kcal');
  const [from, setFrom] = useState(addDays(today(), -13));
  const [to, setTo] = useState(today());
  const [kg, setKg] = useState(profile.weight);

  const start = period === 'week' ? addDays(today(), -6) : period === 'month' ? addDays(today(), -29) : from;
  const end = period === 'custom' ? to : today();
  const days = dateRange(start, end);
  const target = targetsFor(profile, state.settings);

  const data = days.map((d) => sumEntries(state.entries.filter((e) => e.date === d)));
  const logged = data.filter((m) => m.kcal > 0);
  const total = logged.reduce(add, ZERO);
  const n = logged.length || 1;

  const weights = Object.entries(state.weights)
    .map(([date, v]) => ({ date, kg: v }))
    .sort((a, b) => a.date.localeCompare(b.date));
  const inPeriod = weights.filter((w) => w.date >= start && w.date <= end);
  const index = bmi(profile.weight, profile.height);
  const h2 = (profile.height / 100) ** 2;

  return (
    <div className="page">
      <header className="page-head">
        <h1>Прогресс</h1>
        <Segmented
          value={period}
          options={[
            { value: 'week', label: 'Неделя' },
            { value: 'month', label: 'Месяц' },
            { value: 'custom', label: 'Период' },
          ]}
          onChange={setPeriod}
        />
      </header>
      {period === 'custom' && (
        <div className="card grid2">
          <Field label="С">
            <input className="input" type="date" value={from} max={to} onChange={(e) => e.target.value && setFrom(e.target.value)} />
          </Field>
          <Field label="По">
            <input className="input" type="date" value={to} min={from} onChange={(e) => e.target.value && setTo(e.target.value)} />
          </Field>
        </div>
      )}

      <div className="card">
        <div className="card-head">
          <h3>Питание</h3>
          <Segmented
            value={metric}
            options={[
              { value: 'kcal', label: 'Калории' },
              { value: 'macros', label: 'БЖУ' },
            ]}
            onChange={setMetric}
          />
        </div>
        <KcalChart days={days} data={data} target={target.kcal} metric={metric} />
        {metric === 'macros' && (
          <div className="legend">
            <span>
              <i className="dot" style={{ background: 'var(--protein)' }} />
              белки
            </span>
            <span>
              <i className="dot" style={{ background: 'var(--fat)' }} />
              жиры
            </span>
            <span>
              <i className="dot" style={{ background: 'var(--carbs)' }} />
              углеводы
            </span>
          </div>
        )}
        <div className="muted small">
          В среднем за день (дней с записями: {logged.length} из {days.length})
        </div>
        <div className="tiles">
          <div className="tile">
            <b>{n0(total.kcal / n)}</b>
            <span>ккал · норма {n0(target.kcal)}</span>
          </div>
          <div className="tile">
            <b>{r0(total.protein / n)}</b>
            <span>белки · {target.protein} г</span>
          </div>
          <div className="tile">
            <b>{r0(total.fat / n)}</b>
            <span>жиры · {target.fat} г</span>
          </div>
          <div className="tile">
            <b>{r0(total.carbs / n)}</b>
            <span>углеводы · {target.carbs} г</span>
          </div>
        </div>
      </div>

      <div className="card">
        <div className="card-head">
          <h3>Вес</h3>
          <span className="muted">
            {profile.weight} кг → цель {profile.targetWeight} кг
          </span>
        </div>
        <WeightChart points={inPeriod} target={profile.targetWeight} />
        <div className="weight-add">
          <Stepper value={kg} onChange={setKg} step={0.1} min={30} max={250} unit="кг" />
          <button className="btn primary" onClick={() => setWeight(today(), kg)}>
            Записать вес за сегодня
          </button>
        </div>
        <div className="list">
          {[...weights]
            .reverse()
            .slice(0, 5)
            .map((w) => (
              <div className="list-row static" key={w.date}>
                <div className="grow">{fmtShort(w.date)}</div>
                <b>{w.kg} кг</b>
                <button className="icon-btn" title="Удалить" onClick={() => removeWeight(w.date)}>
                  <Icon name="close" size={18} />
                </button>
              </div>
            ))}
        </div>
      </div>

      <div className="card">
        <div className="card-head">
          <h3>Индекс массы тела</h3>
          <span className="muted">норма 18,5–24,9</span>
        </div>
        <div className="bmi">
          <b className="ring-num">{r1(index)}</b>
          <span>{bmiLabel(index)}</span>
        </div>
        <div className="bmi-scale">
          <i style={{ left: clamp(((index - 15) / 20) * 100, 0, 100) + '%' }} />
        </div>
        <div className="muted small">
          Для вашего роста норма — от {r1(18.5 * h2)} до {r1(24.9 * h2)} кг
        </div>
      </div>
    </div>
  );
}
