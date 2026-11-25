import { create } from 'zustand';
import { createJSONStorage, persist } from 'zustand/middleware';
import {
  api,
  AUTH_STORAGE_KEY,
  normalizeError,
  onUnauthorized,
  tokenStorage,
} from '@services/api';
import type { ApiError, AuthResponse, LoginRequest, RegisterRequest, User } from '@/types';

interface AuthState {
  user: User | null;
  token: string | null;
  refreshToken: string | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  error: ApiError | null;
  login: (credentials: LoginRequest) => Promise<void>;
  register: (data: RegisterRequest) => Promise<void>;
  logout: () => Promise<void>;
  updateUser: (user: Partial<User>) => void;
  initialize: () => Promise<void>;
  clearAuth: () => void;
}

function extractTokens(response: AuthResponse): { accessToken: string; refreshToken: string } {
  return {
    accessToken: response.accessToken ?? response.token ?? '',
    refreshToken: response.refreshToken ?? '',
  };
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set, get) => ({
      user: null,
      token: null,
      refreshToken: null,
      isAuthenticated: false,
      isLoading: true,
      error: null,

      login: async (credentials) => {
        set({ isLoading: true, error: null });
        try {
          const response = await api.auth.login(credentials);
          const { accessToken, refreshToken } = extractTokens(response);
          tokenStorage.setTokens(accessToken, refreshToken);
          set({
            user: response.user,
            token: accessToken,
            refreshToken,
            isAuthenticated: Boolean(accessToken),
            isLoading: false,
          });
        } catch (error) {
          const normalized = normalizeError(error);
          set({ error: normalized, isLoading: false });
          throw normalized;
        }
      },

      register: async (data) => {
        set({ isLoading: true, error: null });
        try {
          const response = await api.auth.register(data);
          const { accessToken, refreshToken } = extractTokens(response);
          tokenStorage.setTokens(accessToken, refreshToken);
          set({
            user: response.user,
            token: accessToken,
            refreshToken,
            isAuthenticated: Boolean(accessToken),
            isLoading: false,
          });
        } catch (error) {
          const normalized = normalizeError(error);
          set({ error: normalized, isLoading: false });
          throw normalized;
        }
      },

      logout: async () => {
        try {
          await api.auth.logout();
        } catch {
          // A failed logout should never trap the user in the app.
        } finally {
          get().clearAuth();
        }
      },

      updateUser: (user) => {
        const current = get().user;
        if (current) set({ user: { ...current, ...user } });
      },

      initialize: async () => {
        const token = get().token ?? tokenStorage.getAccessToken();
        if (!token) {
          set({ isLoading: false, isAuthenticated: false });
          return;
        }
        set({ isLoading: true });
        try {
          const user = await api.auth.me();
          set({ user, token, isAuthenticated: true, isLoading: false });
        } catch {
          get().clearAuth();
          set({ isLoading: false });
        }
      },

      clearAuth: () => {
        tokenStorage.clear();
        storageCleanup();
        set({
          user: null,
          token: null,
          refreshToken: null,
          isAuthenticated: false,
          isLoading: false,
          error: null,
        });
      },
    }),
    {
      name: AUTH_STORAGE_KEY,
      storage: createJSONStorage(() => localStorage),
      partialize: (state) => ({
        user: state.user,
        token: state.token,
        refreshToken: state.refreshToken,
        isAuthenticated: state.isAuthenticated,
      }),
      onRehydrateStorage: () => (state) => {
        if (!state) return;
        if (state.token) tokenStorage.setTokens(state.token, state.refreshToken ?? undefined);
        void state.initialize();
      },
    },
  ),
);

function storageCleanup(): void {
  if (typeof window !== 'undefined') window.localStorage.removeItem(AUTH_STORAGE_KEY);
}

onUnauthorized(() => useAuthStore.getState().clearAuth());
