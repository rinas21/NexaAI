import { cn } from '@utils/cn';
import type { HTMLAttributes } from 'react';

export function Skeleton({ className, ...props }: HTMLAttributes<HTMLSpanElement>) {
  return <span aria-hidden="true" className={cn('skeleton block', className)} {...props} />;
}

export function SkeletonText({ className }: { className?: string }) {
  return (
    <span className={cn('block space-y-2', className)} aria-hidden="true">
      <Skeleton className="h-4 w-full" />
      <Skeleton className="h-4 w-5/6" />
      <Skeleton className="h-4 w-2/3" />
    </span>
  );
}

export function SkeletonCard() {
  return (
    <div className="card space-y-4 p-5" aria-hidden="true">
      <Skeleton className="h-6 w-1/3" />
      <SkeletonText />
    </div>
  );
}
