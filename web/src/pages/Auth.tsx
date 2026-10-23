import { useState } from 'react';
import { DEFAULT_PROFILE, ProfileForm } from '../components/ProfileForm';
import { Logo } from '../components/icons';
import { Field } from '../components/ui';
import { useStore } from '../store';

type Mode = 'login' | 'register' | 'forgot';

// Сервера пока нет: вход имитируется локально, пароль никуда не сохраняется.
export function Auth() {
  const { login, loginGuest } = useStore();
  const [mode, setMode] = useState<Mode>('login');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [name, setName] = useState('');
  const [sent, setSent] = useState(false);

  const title = mode === 'login' ? 'Вход' : mode === 'register' ? 'Регистрация' : 'Восстановление доступа';
  const valid = /\S+@\S+\.\S+/.test(email) && (mode === 'forgot' || password.length >= 6);

  return (
    <div className="center-screen">
      <div className="card auth">
        <Logo />
        <p className="muted">Дневник питания: калории, белки, жиры и углеводы.</p>
        <h2>{title}</h2>
        <form
          className="form"
          onSubmit={(e) => {
            e.preventDefault();
            if (!valid) return;
            if (mode === 'forgot') setSent(true);
            else login(email.trim(), name.trim());
          }}
        >
          {mode === 'register' && (
            <Field label="Имя">
              <input className="input" value={name} onChange={(e) => setName(e.target.value)} />
            </Field>
          )}
          <Field label="Электронная почта">
            <input
              className="input"
              type="email"
              autoComplete="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
            />
          </Field>
          {mode !== 'forgot' && (
            <Field label="Пароль (не короче 6 символов)">
              <input
                className="input"
                type="password"
                autoComplete={mode === 'login' ? 'current-password' : 'new-password'}
                value={password}
                onChange={(e) => setPassword(e.target.value)}
              />
            </Field>
          )}
          {sent && mode === 'forgot' && <div className="notice">Если адрес зарегистрирован, письмо уже в пути.</div>}
          <button className="btn primary block" disabled={!valid}>
            {mode === 'login' ? 'Войти' : mode === 'register' ? 'Создать аккаунт' : 'Отправить ссылку'}
          </button>
        </form>
        {mode !== 'forgot' && (
          <button className="btn block" onClick={() => login('demo@gmail.com', 'Demo')}>
            Войти через Google
          </button>
        )}
        <div className="auth-links">
          {mode === 'login' ? (
            <>
              <button className="link" onClick={() => setMode('register')}>
                Регистрация
              </button>
              <button className="link" onClick={() => setMode('forgot')}>
                Забыли пароль?
              </button>
            </>
          ) : (
            <button className="link" onClick={() => setMode('login')}>
              Уже есть аккаунт — войти
            </button>
          )}
        </div>
        <button className="link muted" onClick={loginGuest}>
          Посмотреть справочник продуктов без входа
        </button>
      </div>
    </div>
  );
}

export function Onboarding() {
  const { state, saveProfile, logout } = useStore();
  return (
    <div className="center-screen">
      <div className="card onboarding">
        <h2>Анкета</h2>
        <div className="muted">По этим данным рассчитаем вашу суточную норму КБЖУ. Их можно изменить позже.</div>
        <ProfileForm
          initial={{ ...DEFAULT_PROFILE, name: state.user?.name ?? '' }}
          submitLabel="Рассчитать норму"
          onSubmit={saveProfile}
        />
        <button className="link muted" onClick={logout}>
          Выйти
        </button>
      </div>
    </div>
  );
}
