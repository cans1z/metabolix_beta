import type { ReactNode } from 'react';
import { clamp } from '../lib/util';
import { Icon } from './icons';

export function Modal(props: { title: string; onClose: () => void; children: ReactNode }) {
  return (
    <div className="backdrop" onMouseDown={(e) => e.target === e.currentTarget && props.onClose()}>
      <div className="modal" role="dialog" aria-label={props.title}>
        <div className="modal-head">
          <h3>{props.title}</h3>
          <button className="icon-btn" onClick={props.onClose} aria-label="Закрыть">
            <Icon name="close" />
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

export function Meter(props: { value: number; max: number; color?: string; thin?: boolean }) {
  const pct = props.max > 0 ? clamp((props.value / props.max) * 100, 0, 100) : 0;
  const over = props.value > props.max;
  return (
    <div className={'meter' + (props.thin ? ' thin' : '')}>
      <div style={{ width: pct + '%', background: over ? 'var(--danger)' : props.color }} />
    </div>
  );
}

export function MacroBar(props: { label: string; value: number; max: number; color: string }) {
  const left = Math.round(props.max - props.value);
  return (
    <div className="macro" title={left >= 0 ? `Осталось ${left} г` : `Сверх нормы на ${-left} г`}>
      <div className="macro-name">
        <i className="dot" style={{ background: props.color }} />
        {props.label}
      </div>
      <div className="macro-val">
        <b>{Math.round(props.value)}</b> / {Math.round(props.max)} г
      </div>
      <Meter value={props.value} max={props.max} color={props.color} thin />
    </div>
  );
}
