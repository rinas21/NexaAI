import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import toast from 'react-hot-toast';
import { api } from '@services/api';
import type { CreateConversationRequest, SendMessageRequest } from '@/types';

export const chatKeys = {
  all: ['chat'] as const,
  conversations: () => [...chatKeys.all, 'conversations'] as const,
  conversation: (id: string) => [...chatKeys.all, 'conversation', id] as const,
};

export function useConversations() {
  return useQuery({
    queryKey: chatKeys.conversations(),
    queryFn: () => api.chat.conversations.list(),
  });
}

export function useConversation(id: string | null) {
  return useQuery({
    queryKey: chatKeys.conversation(id ?? 'none'),
    queryFn: () => api.chat.conversations.get(id ?? ''),
    enabled: Boolean(id),
  });
}

export function useCreateConversation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: CreateConversationRequest = {}) => api.chat.conversations.create(data),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: chatKeys.conversations() });
    },
    onError: (error: Error) => toast.error(error.message || 'Could not start a new chat'),
  });
}

export function useDeleteConversation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.chat.conversations.delete(id),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: chatKeys.conversations() });
      toast.success('Conversation deleted');
    },
    onError: (error: Error) => toast.error(error.message || 'Could not delete conversation'),
  });
}

export function useSendMessage() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: SendMessageRequest) => api.chat.send(data),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: chatKeys.conversations() });
    },
    onError: (error: Error) => toast.error(error.message || 'Could not send message'),
  });
}
