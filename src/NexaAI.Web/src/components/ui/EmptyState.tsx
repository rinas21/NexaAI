import type { ReactNode } from 'react';
import { Inbox } from 'lucide-react';

export interface EmptyStateProps {
  title: string;
  description?: string;
  icon?: ReactNode;
  action?: ReactNode;
  className?: string;
}

export function EmptyState({ title, description, icon, action, className }: EmptyStateProps) {
  return (
    <div className={`flex flex-col items-center justify-center px-6 py-12 text-center ${className ?? ''}`}>
      <div className="mb-4 flex h-12 w-12 items-center justify-center rounded-2xl bg-neutral-100 text-neutral-400 dark:bg-neutral-800 dark:text-neutral-500">
        {icon ?? <Inbox className="h-6 w-6" />}
      </div>
      <p className="font-medium text-neutral-800 dark:text-neutral-100">{title}</p>
      {description && (
        <p className="mt-1 max-w-sm text-sm text-neutral-500 dark:text-neutral-400">{description}</p>
      )}
      {action && <div className="mt-5">{action}</div>}
    </div>
  );
}
