import type { User } from '../types';

interface Session {
  accessToken: string;
  refreshToken: string;
}

export interface AuthResponse extends Session {
  user: User;
}

export class ApiError extends Error {
  constructor(message: string, public readonly status?: number) {
    super(message);
  }
}

interface RequestOptions {
  method?: string;
  body?: unknown;
  anonymous?: boolean;
  signal?: AbortSignal;
}

const SESSION_KEY = 'metabolix:api-session';

export class ApiClient {
  private session: Session | null = null;
  private renewing: Promise<void> | null = null;
  onExpired?: () => void;

  restore() {
    const saved = sessionStorage.getItem(SESSION_KEY);
    if (!saved) return;
    try {
      const session = JSON.parse(saved) as Session;
      if (!session.accessToken || !session.refreshToken) throw new Error();
      this.session = session;
    } catch {
      this.clear();
    }
  }

  get hasSession() {
    return this.session !== null;
  }

  save(response: Session) {
    const session = { accessToken: response.accessToken, refreshToken: response.refreshToken };
    sessionStorage.setItem(SESSION_KEY, JSON.stringify(session));
    this.session = session;
  }

  clear() {
    sessionStorage.removeItem(SESSION_KEY);
    this.session = null;
  }

  private expired() {
    try {
      const payload = this.session!.accessToken.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
      const { exp } = JSON.parse(atob(payload)) as { exp: number };
      return exp * 1000 <= Date.now();
    } catch {
      return true;
    }
  }

  private renew(): Promise<void> {
    if (this.renewing) return this.renewing;
    const refreshToken = this.session?.refreshToken;
    const task = (async () => {
      try {
        const response = await this.send<AuthResponse>('auth/refresh', {
          method: 'POST', body: { refreshToken }, anonymous: true,
        });
        this.save(response);
      } catch (error) {
        if (error instanceof ApiError && error.status === 401) {
          this.clear();
          this.onExpired?.();
        }
        throw error;
      }
    })();
    this.renewing = task.finally(() => { this.renewing = null; });
    return this.renewing;
  }

  async request<T>(path: string, options: RequestOptions = {}): Promise<T> {
    if (!options.anonymous && this.session && this.expired()) await this.renew();
    const token = options.anonymous ? undefined : this.session?.accessToken;
    try {
      return await this.send<T>(path, options, token);
    } catch (error) {
      if (!(error instanceof ApiError) || error.status !== 401 || !token || !this.session) throw error;
      if (this.renewing || token === this.session.accessToken) await this.renew();
      return this.send<T>(path, options, this.session?.accessToken);
    }
  }

  async logout() {
    if (this.renewing) {
      try { await this.renewing; } catch { /* выход доступен и при обрыве соединения */ }
    }
    const refreshToken = this.session?.refreshToken;
    this.clear();
    if (refreshToken) {
      await this.request('auth/logout', { method: 'POST', body: { refreshToken }, anonymous: true });
    }
  }

  private async send<T>(path: string, options: RequestOptions, token?: string): Promise<T> {
    const controller = new AbortController();
    const abort = () => controller.abort();
    options.signal?.addEventListener('abort', abort, { once: true });
    if (options.signal?.aborted) controller.abort();
    const timeout = setTimeout(abort, 12000);
    try {
      const response = await fetch('/api/' + path, {
        method: options.method ?? 'GET',
        headers: {
          ...(token ? { Authorization: 'Bearer ' + token } : {}),
          ...(options.body === undefined ? {} : { 'Content-Type': 'application/json' }),
        },
        body: options.body === undefined ? undefined : JSON.stringify(options.body),
        signal: controller.signal,
        redirect: 'error',
        cache: 'no-store',
      });
      if (!response.ok) {
        const problem = await response.json().catch(() => null) as {
          detail?: string; errors?: Record<string, string[]>;
        } | null;
        const message = problem?.errors
          ? Object.values(problem.errors).flat().join('\n')
          : problem?.detail ?? (response.status === 401 ? 'Войдите в аккаунт заново' : 'Ошибка сервера (' + response.status + ')');
        throw new ApiError(message, response.status);
      }
      if (response.status === 204) return undefined as T;
      return await response.json() as T;
    } catch (error) {
      if (error instanceof ApiError) throw error;
      if (options.signal?.aborted) throw error;
      throw new ApiError(controller.signal.aborted
        ? 'Сервер не ответил вовремя. Повторите попытку'
        : 'Не удалось связаться с сервером. Проверьте соединение');
    } finally {
      clearTimeout(timeout);
      options.signal?.removeEventListener('abort', abort);
    }
  }
}
