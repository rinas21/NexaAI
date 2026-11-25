import type { HTMLAttributes, ReactNode } from 'react';
import { cn } from '@utils/cn';

export type BadgeVariant = 'primary' | 'success' | 'warning' | 'danger' | 'neutral';

export interface BadgeProps extends HTMLAttributes<HTMLSpanElement> {
  variant?: BadgeVariant;
  dot?: boolean;
  children: ReactNode;
}

const variantClasses: Record<BadgeVariant, string> = {
  primary: 'badge-primary',
  success: 'badge-success',
  warning: 'badge-warning',
  danger: 'badge-danger',
  neutral: 'badge-neutral',
};

const dotClasses: Record<BadgeVariant, string> = {
  primary: 'bg-primary-500',
  success: 'bg-green-500',
  warning: 'bg-amber-500',
  danger: 'bg-red-500',
  neutral: 'bg-neutral-400',
};

export function Badge({ variant = 'neutral', dot = false, className, children, ...props }: BadgeProps) {
  return (
    <span className={cn(variantClasses[variant], 'gap-1.5', className)} {...props}>
      {dot && <span className={cn('h-1.5 w-1.5 rounded-full', dotClasses[variant])} aria-hidden="true" />}
      {children}
    </span>
  );
}
