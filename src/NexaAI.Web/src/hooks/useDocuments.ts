import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import toast from 'react-hot-toast';
import { api } from '@services/api';
import type { AccessLevel } from '@/types';

export const documentKeys = {
  all: ['documents'] as const,
  list: () => [...documentKeys.all, 'list'] as const,
  detail: (id: string) => [...documentKeys.all, 'detail', id] as const,
  search: (query: string) => [...documentKeys.all, 'search', query] as const,
};

export function useDocuments(params?: { search?: string; status?: string }) {
  return useQuery({
    queryKey: [...documentKeys.list(), params],
    queryFn: () => api.documents.list(params),
  });
}

export function useDocument(id: string | null) {
  return useQuery({
    queryKey: documentKeys.detail(id ?? 'none'),
    queryFn: () => api.documents.get(id ?? ''),
    enabled: Boolean(id),
  });
}

export function useDocumentSearch(query: string) {
  return useQuery({
    queryKey: documentKeys.search(query),
    queryFn: () => api.documents.search(query),
    enabled: query.trim().length > 0,
  });
}

export function useUploadDocument() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (file: File) => api.documents.upload(file),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: documentKeys.all });
      toast.success('Document uploaded successfully');
    },
    onError: (error: Error) => toast.error(error.message || 'Upload failed'),
  });
}

export function useDeleteDocument() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.documents.delete(id),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: documentKeys.all });
      toast.success('Document deleted');
    },
    onError: (error: Error) => toast.error(error.message || 'Could not delete document'),
  });
}

export function useUpdateDocumentAccess() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, accessLevel }: { id: string; accessLevel: AccessLevel }) =>
      api.documents.updateAccess(id, accessLevel),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: documentKeys.all });
      toast.success('Access level updated');
    },
    onError: (error: Error) => toast.error(error.message || 'Could not update access'),
  });
}
