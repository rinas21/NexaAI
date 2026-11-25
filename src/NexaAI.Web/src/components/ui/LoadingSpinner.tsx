import { cn } from '@utils/cn';

export interface LoadingSpinnerProps {
  size?: 'sm' | 'md' | 'lg';
  className?: string;
  label?: string;
}

const sizeClasses = {
  sm: 'h-4 w-4 border-2',
  md: 'h-6 w-6 border-2',
  lg: 'h-10 w-10 border-[3px]',
} as const;

export function LoadingSpinner({
  size = 'md',
  className,
  label = 'Loading',
}: LoadingSpinnerProps) {
  return (
    <span
      role="status"
      aria-label={label}
      className={cn(
        'inline-block animate-spin rounded-full border-current border-t-transparent text-primary-600 dark:text-primary-400',
        sizeClasses[size],
        className,
      )}
    />
  );
}
