import { useEffect, useState } from 'react';
import { Auth, Onboarding } from './pages/Auth';
import { Diary } from './pages/Diary';
import { Foods } from './pages/Foods';
import { Profile } from './pages/Profile';
import { Progress } from './pages/Progress';
import { useStore } from './store';

type Tab = 'diary' | 'foods' | 'progress' | 'profile';

const TABS: { id: Tab; label: string; icon: string }[] = [
  { id: 'diary', label: 'Дневник', icon: '📖' },
  { id: 'foods', label: 'Продукты', icon: '🥦' },
  { id: 'progress', label: 'Прогресс', icon: '📈' },
  { id: 'profile', label: 'Профиль', icon: '👤' },
];

export function App() {
  const { state } = useStore();
  const [tab, setTab] = useState<Tab>('diary');

  useEffect(() => {
    const t = state.settings.theme;
    if (t === 'system') document.documentElement.removeAttribute('data-theme');
    else document.documentElement.setAttribute('data-theme', t);
  }, [state.settings.theme]);

  if (!state.user) {
    // Гостю доступен только справочник продуктов.
    return state.guest ? (
      <main className="guest">
        <Foods />
      </main>
    ) : (
      <Auth />
    );
  }
  if (!state.profile) return <Onboarding />;

  return (
    <div className="app">
      <nav className="nav">
        <div className="brand">
          <span className="logo">M</span> <span className="brand-name">Metabolix</span>
        </div>
        {TABS.map((t) => (
          <button key={t.id} className={'nav-item' + (tab === t.id ? ' active' : '')} onClick={() => setTab(t.id)}>
            <span className="nav-icon">{t.icon}</span>
            <span>{t.label}</span>
          </button>
        ))}
      </nav>
      <main>
        {tab === 'diary' && <Diary />}
        {tab === 'foods' && <Foods />}
        {tab === 'progress' && <Progress />}
        {tab === 'profile' && <Profile />}
      </main>
    </div>
  );
}
