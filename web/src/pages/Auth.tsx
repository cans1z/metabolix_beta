import { useState } from 'react';
import { DEFAULT_PROFILE, ProfileForm } from '../components/ProfileForm';
import { Logo } from '../components/icons';
import { Field } from '../components/ui';
import { useStore } from '../store';

type Mode = 'login' | 'register' | 'forgot' | 'reset';

export function Auth() {
  const { login, loginGuest, forgotPassword, resetPassword } = useStore();
  const [mode, setMode] = useState<Mode>('login');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [name, setName] = useState('');
  const [code, setCode] = useState('');
  const [acceptedTerms, setAcceptedTerms] = useState(false);

  const title = mode === 'login' ? 'Вход' : mode === 'register' ? 'Регистрация'
    : mode === 'reset' ? 'Новый пароль' : 'Восстановление доступа';
  const valid = /^\S+@\S+\.\S+$/.test(email.trim()) && (mode === 'forgot' || password.length >= 6) &&
    (mode !== 'register' || acceptedTerms) && (mode !== 'reset' || /^\d{6}$/.test(code));
  const switchMode = (next: Mode) => {
    setPassword('');
    setCode('');
    setAcceptedTerms(false);
    setMode(next);
  };

  return (
    <div className="center-screen">
      <div className="card auth">
        <Logo />
        <p className="muted">Дневник питания: калории, белки, жиры и углеводы.</p>
        <h2>{title}</h2>
        <form
          className="form"
          onSubmit={async (e) => {
            e.preventDefault();
            if (!valid) return;
            const secret = password;
            setPassword('');
            if (mode === 'forgot') {
              if (await forgotPassword(email.trim())) switchMode('reset');
            } else if (mode === 'reset') {
              if (await resetPassword(email.trim(), code, secret)) switchMode('login');
            } else {
              await login(email.trim(), secret, mode === 'register' ? name.trim() : undefined, acceptedTerms);
            }
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
          {mode === 'reset' && (
            <Field label="Код из письма">
              <input className="input" inputMode="numeric" value={code}
                onChange={(e) => setCode(e.target.value.trim())} maxLength={6} />
              <span className="muted small">Если адрес зарегистрирован, на него отправлен код.</span>
            </Field>
          )}
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
          {mode === 'register' && (
            <label>
              <input type="checkbox" checked={acceptedTerms} onChange={(e) => setAcceptedTerms(e.target.checked)} />{' '}
              Согласен на обработку персональных данных
            </label>
          )}
          <button className="btn primary block" disabled={!valid}>
            {mode === 'login' ? 'Войти' : mode === 'register' ? 'Создать аккаунт'
              : mode === 'reset' ? 'Сменить пароль' : 'Отправить код'}
          </button>
        </form>
        <div className="auth-links">
          {mode === 'login' ? (
            <>
              <button className="link" onClick={() => switchMode('register')}>
                Регистрация
              </button>
              <button className="link" onClick={() => switchMode('forgot')}>
                Забыли пароль?
              </button>
            </>
          ) : (
            <button className="link" onClick={() => switchMode('login')}>
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
