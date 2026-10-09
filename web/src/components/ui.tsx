import type { ReactNode } from 'react';
import { clamp } from '../lib/util';

export function Modal(props: { title: string; onClose: () => void; children: ReactNode }) {
  return (
    <div className="backdrop" onMouseDown={(e) => e.target === e.currentTarget && props.onClose()}>
      <div className="modal" role="dialog" aria-label={props.title}>
        <div className="modal-head">
          <h3>{props.title}</h3>
          <button className="icon-btn" onClick={props.onClose} aria-label="Закрыть">
            ✕
          </button>
        </div>
        {props.children}
      </div>
    </div>
  );
}

/** Числовой степпер с необязательным слайдером — ввод в минимум касаний. */
export function Stepper(props: {
  value: number;
  onChange: (v: number) => void;
  step?: number;
  min?: number;
  max?: number;
  unit?: string;
  slider?: boolean;
}) {
  const { value, onChange, step = 1, min = 0, max = 9999, unit, slider } = props;
  const set = (v: number) => onChange(clamp(Math.round(v * 10) / 10, min, max));
  return (
    <div className="stepper-wrap">
      <div className="stepper">
        <button type="button" onClick={() => set(value - step)} aria-label="Меньше">
          −
        </button>
        <input
          type="number"
          value={Number.isFinite(value) ? value : ''}
          min={min}
          max={max}
          step={step}
          onChange={(e) => set(Number(e.target.value))}
        />
        {unit && <span className="unit">{unit}</span>}
        <button type="button" onClick={() => set(value + step)} aria-label="Больше">
          +
        </button>
      </div>
      {slider && (
        <input type="range" min={min} max={max} step={step} value={value} onChange={(e) => set(Number(e.target.value))} />
      )}
    </div>
  );
}

export function Segmented<T extends string | number>(props: {
  value: T;
  options: { value: T; label: string }[];
  onChange: (v: T) => void;
}) {
  return (
    <div className="segmented">
      {props.options.map((o) => (
        <button
          type="button"
          key={String(o.value)}
          className={o.value === props.value ? 'active' : ''}
          onClick={() => props.onChange(o.value)}
        >
          {o.label}
        </button>
      ))}
    </div>
  );
}

export function Toggle(props: { checked: boolean; onChange: (v: boolean) => void; label: string }) {
  return (
    <label className="toggle">
      <span>{props.label}</span>
      <input type="checkbox" checked={props.checked} onChange={(e) => props.onChange(e.target.checked)} />
      <i />
    </label>
  );
}

export function Field(props: { label: string; children: ReactNode }) {
  return (
    <div className="field">
      <div className="label">{props.label}</div>
      {props.children}
    </div>
  );
}

export function Ring(props: { value: number; max: number; size?: number; children?: ReactNode }) {
  const size = props.size ?? 160;
  const stroke = 12;
  const r = (size - stroke) / 2;
  const c = 2 * Math.PI * r;
  const ratio = props.max > 0 ? props.value / props.max : 0;
  return (
    <div className="ring" style={{ width: size, height: size }}>
      <svg width={size} height={size}>
        <circle cx={size / 2} cy={size / 2} r={r} fill="none" stroke="var(--track)" strokeWidth={stroke} />
        <circle
          cx={size / 2}
          cy={size / 2}
          r={r}
          fill="none"
          stroke={ratio > 1 ? 'var(--danger)' : 'var(--accent)'}
          strokeWidth={stroke}
          strokeLinecap="round"
          strokeDasharray={c}
          strokeDashoffset={c * (1 - clamp(ratio, 0, 1))}
          transform={`rotate(-90 ${size / 2} ${size / 2})`}
        />
      </svg>
      <div className="ring-center">{props.children}</div>
    </div>
  );
}

export function MacroBar(props: { label: string; value: number; max: number; color: string }) {
  const pct = props.max > 0 ? clamp((props.value / props.max) * 100, 0, 100) : 0;
  const left = Math.round(props.max - props.value);
  return (
    <div className="macro">
      <div className="macro-top">
        <span>{props.label}</span>
        <span>
          <b>{Math.round(props.value)}</b> / {Math.round(props.max)} г
        </span>
      </div>
      <div className="bar">
        <div style={{ width: pct + '%', background: props.color }} />
      </div>
      <div className="muted small">{left >= 0 ? `осталось ${left} г` : `перебор ${-left} г`}</div>
    </div>
  );
}
