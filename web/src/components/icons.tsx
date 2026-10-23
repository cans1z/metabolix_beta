import type { ReactNode } from 'react';

const PATHS: Record<string, ReactNode> = {
  diary: (
    <>
      <path d="M5 4.5A1.5 1.5 0 0 1 6.5 3H19v15H6.5A1.5 1.5 0 0 0 5 19.5z" />
      <path d="M5 19.5A1.5 1.5 0 0 0 6.5 21H19v-3" />
      <path d="M9 7.5h6M9 11h4" />
    </>
  ),
  foods: (
    <>
      <path d="M12 7.5c-1.6-1.3-4.2-1.3-5.7.4-1.9 2.1-1.4 5.8.2 8.6 1.2 2.1 2.8 3.9 4.3 3.5.5-.1.8-.4 1.2-.4s.7.3 1.2.4c1.5.4 3.1-1.4 4.3-3.5 1.6-2.8 2.1-6.5.2-8.6-1.5-1.7-4.1-1.7-5.7-.4z" />
      <path d="M12 7.5c0-2 .8-3.6 2.8-4.5" />
    </>
  ),
  progress: (
    <>
      <path d="M4 20h16" />
      <path d="M5 15.5l4-4.5 3.5 2.5L19 6" />
      <path d="M15 6h4v4" />
    </>
  ),
  profile: (
    <>
      <circle cx="12" cy="8" r="3.5" />
      <path d="M5 20c1.2-3.5 3.8-5.5 7-5.5s5.8 2 7 5.5" />
    </>
  ),
  plus: <path d="M12 5v14M5 12h14" />,
  close: <path d="M6.5 6.5l11 11M17.5 6.5l-11 11" />,
  left: <path d="M14.5 6l-6 6 6 6" />,
  right: <path d="M9.5 6l6 6-6 6" />,
  star: <path d="M12 4.2l2.4 4.9 5.4.8-3.9 3.8.9 5.4L12 16.6l-4.8 2.5.9-5.4-3.9-3.8 5.4-.8z" />,
  edit: (
    <>
      <path d="M4.5 19.5h4l10-10-4-4-10 10z" />
      <path d="M13 7l4 4" />
    </>
  ),
  copy: (
    <>
      <rect x="8.5" y="8.5" width="11" height="11" rx="2" />
      <path d="M15.5 8.5V6a1.5 1.5 0 0 0-1.5-1.5H6A1.5 1.5 0 0 0 4.5 6v8A1.5 1.5 0 0 0 6 15.5h2.5" />
    </>
  ),
  search: (
    <>
      <circle cx="11" cy="11" r="6" />
      <path d="M19.5 19.5l-4-4" />
    </>
  ),
  barcode: <path d="M4 6v12M7.5 6v12M10 6v12M13.5 6v12M16 6v12M20 6v12" />,
  calendar: (
    <>
      <rect x="4" y="5.5" width="16" height="14.5" rx="2" />
      <path d="M4 10h16M8.5 3.5v4M15.5 3.5v4" />
    </>
  ),
  cup: <path d="M6 4.5h12l-1.6 15H7.6z" />,
  logout: (
    <>
      <path d="M14 4.5h3.5A1.5 1.5 0 0 1 19 6v12a1.5 1.5 0 0 1-1.5 1.5H14" />
      <path d="M10 16l-4-4 4-4M6 12h9" />
    </>
  ),
  trash: <path d="M5 7h14M10 7V5h4v2M7 7l.9 12.5h8.2L17 7" />,
};

export type IconName = keyof typeof PATHS;

export function Icon(props: { name: IconName; size?: number; filled?: boolean }) {
  const size = props.size ?? 20;
  return (
    <svg
      className="icon"
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill={props.filled ? 'currentColor' : 'none'}
      stroke="currentColor"
      strokeWidth={1.7}
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
    >
      {PATHS[props.name]}
    </svg>
  );
}

/** Знак — тарелка с отрезанной порцией. */
export function Logo() {
  return (
    <span className="logo">
      <svg width="22" height="22" viewBox="0 0 24 24" aria-hidden="true">
        <circle cx="12" cy="12" r="8.5" fill="none" stroke="currentColor" strokeWidth="2.2" />
        <path d="M12 12V3.5A8.5 8.5 0 0 1 20.5 12z" fill="currentColor" />
      </svg>
      metabolix
    </span>
  );
}
