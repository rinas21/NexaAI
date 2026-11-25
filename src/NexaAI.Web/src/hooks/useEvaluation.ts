import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import toast from 'react-hot-toast';
import { api } from '@services/api';
import type { StartEvaluationRequest } from '@/types';

export const evaluationKeys = {
  all: ['evaluation'] as const,
  runs: () => [...evaluationKeys.all, 'runs'] as const,
  summary: () => [...evaluationKeys.all, 'summary'] as const,
};

export function useEvaluationSummary() {
  return useQuery({
    queryKey: evaluationKeys.summary(),
    queryFn: () => api.evaluation.summary(),
  });
}

export function useEvaluationRuns() {
  return useQuery({
    queryKey: evaluationKeys.runs(),
    queryFn: () => api.evaluation.runs(),
  });
}

export function useStartEvaluation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: StartEvaluationRequest = {}) => api.evaluation.start(data),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: evaluationKeys.all });
      toast.success('Evaluation started');
    },
    onError: (error: Error) => toast.error(error.message || 'Could not start evaluation'),
  });
}
