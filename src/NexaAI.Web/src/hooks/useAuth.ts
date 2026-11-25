import toast from 'react-hot-toast';
import { useAuthStore } from '@store/authStore';
import type { LoginRequest, RegisterRequest } from '@/types';

export function useAuth() {
  const user = useAuthStore((state) => state.user);
  const isAuthenticated = useAuthStore((state) => state.isAuthenticated);
  const isLoading = useAuthStore((state) => state.isLoading);
  const error = useAuthStore((state) => state.error);

  const signIn = async (credentials: LoginRequest): Promise<void> => {
    await useAuthStore.getState().login(credentials);
    toast.success('Welcome back!');
  };

  const signUp = async (data: RegisterRequest): Promise<void> => {
    await useAuthStore.getState().register(data);
    toast.success('Account created successfully');
  };

  const signOut = async (): Promise<void> => {
    await useAuthStore.getState().logout();
    toast.success('Signed out');
  };

  return {
    user,
    isAuthenticated,
    isLoading,
    error,
    signIn,
    signUp,
    signOut,
    updateUser: useAuthStore.getState().updateUser,
    initialize: useAuthStore.getState().initialize,
  };
}
