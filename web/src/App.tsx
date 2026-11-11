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
  const { busy, loading, loadError, error, dismissError, reload, logout } = useStore();
  return (
    <>
      {error && (
        <div className="network-error" role="alert">
          <span>{error}</span>
          <button className="link" onClick={dismissError}>Закрыть</button>
        </div>
      )}
      {loading ? <div className="center-screen" role="status">Загрузка данных…</div> : loadError ? (
        <div className="center-screen">
          <div className="card auth">
            <p role="alert">{loadError}</p>
            <button className="btn primary" onClick={() => void reload()}>Повторить</button>
            <button className="btn" disabled={busy} onClick={() => void logout()}>Выйти</button>
          </div>
        </div>
      ) : (
        <fieldset className="app-controls" disabled={busy} aria-busy={busy}>
          <AppView />
        </fieldset>
      )}
    </>
  );
}

function AppView() {
  const { state, refresh } = useStore();
  const [tab, setTab] = useState<Tab>('diary');

  useEffect(() => {
    const t = state.settings.theme;
    if (t === 'system') document.documentElement.removeAttribute('data-theme');
    else document.documentElement.setAttribute('data-theme', t);
  }, [state.settings.theme]);

  if (!state.user) {
    // гостю доступен только справочник продуктов
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
            <button key={t.id} className={'nav-item' + (tab === t.id ? ' active' : '')} onClick={() => {
              setTab(t.id);
              void refresh();
            }}>
              <Icon name={t.icon} />
              <span>{t.label}</span>
            </button>
          ))}
        </nav>
        <button className="btn small" onClick={() => void refresh()}>Обновить</button>
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
