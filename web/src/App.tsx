import { useEffect, useState } from 'react';
import { Icon, Logo, type IconName } from './components/icons';
import { Auth, Onboarding } from './pages/Auth';
import { Diary } from './pages/Diary';
import { Foods } from './pages/Foods';
import { Profile } from './pages/Profile';
import { Progress } from './pages/Progress';
import { useStore } from './store';

type Tab = 'diary' | 'foods' | 'progress' | 'profile';

const TABS: { id: Tab; label: string; icon: IconName }[] = [
  { id: 'diary', label: 'Дневник', icon: 'diary' },
  { id: 'foods', label: 'Продукты', icon: 'foods' },
  { id: 'progress', label: 'Прогресс', icon: 'progress' },
  { id: 'profile', label: 'Профиль', icon: 'profile' },
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
      <div className="app">
        <header className="topbar">
          <Logo />
        </header>
        <main>
          <Foods />
        </main>
      </div>
    ) : (
      <Auth />
    );
  }
  if (!state.profile) return <Onboarding />;

  return (
    <div className="app">
      <header className="topbar">
        <Logo />
        <nav className="nav">
          {TABS.map((t) => (
            <button key={t.id} className={'nav-item' + (tab === t.id ? ' active' : '')} onClick={() => setTab(t.id)}>
              <Icon name={t.icon} />
              <span>{t.label}</span>
            </button>
          ))}
        </nav>
        <span className="topbar-user">{state.profile.name || state.user.name}</span>
      </header>
      <main>
        {tab === 'diary' && <Diary />}
        {tab === 'foods' && <Foods />}
        {tab === 'progress' && <Progress />}
        {tab === 'profile' && <Profile />}
      </main>
    </div>
  );
}
