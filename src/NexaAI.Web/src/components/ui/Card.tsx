import type { HTMLAttributes, ReactNode } from 'react';
import { cn } from '@utils/cn';

interface CardProps extends HTMLAttributes<HTMLDivElement> {
  interactive?: boolean;
}

export function Card({ interactive = false, className, children, ...props }: CardProps) {
  return (
    <div className={cn(interactive ? 'card-hover' : 'card', className)} {...props}>
      {children}
    </div>
  );
}

export function CardHeader({ className, children, ...props }: HTMLAttributes<HTMLDivElement>) {
  return (
    <div className={cn('flex items-start justify-between gap-4 px-5 py-4', className)} {...props}>
      {children}
    </div>
  );
}

export function CardTitle({
  className,
  children,
  ...props
}: HTMLAttributes<HTMLHeadingElement>) {
  return (
    <h3 className={cn('text-base font-semibold tracking-tight text-neutral-900 dark:text-neutral-50', className)} {...props}>
      {children}
    </h3>
  );
}

export function CardDescription({
  className,
  children,
  ...props
}: HTMLAttributes<HTMLParagraphElement>) {
  return (
    <p className={cn('mt-1 text-sm text-neutral-500 dark:text-neutral-400', className)} {...props}>
      {children}
    </p>
  );
}

export function CardContent({ className, children, ...props }: HTMLAttributes<HTMLDivElement>) {
  return (
    <div className={cn('px-5 pb-5', className)} {...props}>
      {children}
    </div>
  );
}

export function CardFooter({ className, children, ...props }: HTMLAttributes<HTMLDivElement>) {
  return (
    <div
      className={cn(
        'flex items-center gap-3 border-t border-neutral-200 px-5 py-4 dark:border-neutral-700',
        className,
      )}
      {...props}
    >
      {children}
    </div>
  );
}

export interface StatCardProps {
  label: string;
  value: ReactNode;
  icon: ReactNode;
  helper?: string;
  tone?: 'primary' | 'success' | 'warning' | 'purple';
}

const toneClasses = {
  primary: 'bg-primary-50 text-primary-600 dark:bg-primary-900/30 dark:text-primary-300',
  success: 'bg-green-50 text-green-600 dark:bg-green-900/30 dark:text-green-300',
  warning: 'bg-amber-50 text-amber-600 dark:bg-amber-900/30 dark:text-amber-300',
  purple: 'bg-purple-50 text-purple-600 dark:bg-purple-900/30 dark:text-purple-300',
} as const;

export function StatCard({ label, value, icon, helper, tone = 'primary' }: StatCardProps) {
  return (
    <Card className="relative overflow-hidden p-5" interactive>
      <div className="pointer-events-none absolute -right-8 -top-10 h-28 w-28 rounded-full bg-gradient-to-br from-primary-100/60 to-transparent dark:from-primary-500/10" />
      <div className="relative flex items-start justify-between gap-4">
        <div>
          <p className="text-sm font-medium text-neutral-500 dark:text-neutral-400">{label}</p>
          <p className="mt-2 text-3xl font-semibold tracking-tight text-neutral-900 dark:text-neutral-50">
            {value}
          </p>
          {helper && <p className="mt-1.5 text-xs text-neutral-500 dark:text-neutral-400">{helper}</p>}
        </div>
        <span className={cn('flex h-11 w-11 shrink-0 items-center justify-center rounded-xl', toneClasses[tone])}>
          {icon}
        </span>
      </div>
    </Card>
  );
}
