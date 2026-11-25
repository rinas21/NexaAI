import { useEffect, useRef, useState, type ReactNode } from 'react';
import { ChevronDown } from 'lucide-react';
import { cn } from '@utils/cn';

export interface DropdownItem {
  label: string;
  icon?: ReactNode;
  onClick: () => void;
  danger?: boolean;
  disabled?: boolean;
}

export interface DropdownProps {
  trigger: ReactNode;
  items: DropdownItem[];
  align?: 'left' | 'right';
  className?: string;
  ariaLabel?: string;
}

export function Dropdown({
  trigger,
  items,
  align = 'right',
  className,
  ariaLabel = 'Open menu',
}: DropdownProps) {
  const [isOpen, setIsOpen] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!isOpen) return;
    const onPointerDown = (event: MouseEvent) => {
      if (!containerRef.current?.contains(event.target as Node)) setIsOpen(false);
    };
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') setIsOpen(false);
    };
    document.addEventListener('mousedown', onPointerDown);
    document.addEventListener('keydown', onKeyDown);
    return () => {
      document.removeEventListener('mousedown', onPointerDown);
      document.removeEventListener('keydown', onKeyDown);
    };
  }, [isOpen]);

  return (
    <div ref={containerRef} className={cn('relative', className)}>
      <button
        type="button"
        aria-haspopup="menu"
        aria-expanded={isOpen}
        aria-label={ariaLabel}
        onClick={() => setIsOpen((open) => !open)}
        className="inline-flex items-center gap-1.5 rounded-lg transition-colors"
      >
        {trigger}
        <ChevronDown
          className={cn('h-3.5 w-3.5 text-neutral-400 transition-transform', isOpen && 'rotate-180')}
          aria-hidden="true"
        />
      </button>
      {isOpen && (
        <div
          role="menu"
          className={cn(
            'absolute top-full z-40 mt-2 min-w-[190px] overflow-hidden rounded-xl border border-neutral-200 bg-white p-1.5 shadow-soft animate-slide-down dark:border-neutral-700 dark:bg-neutral-900',
            align === 'right' ? 'right-0' : 'left-0',
          )}
        >
          {items.map((item) => (
            <button
              key={item.label}
              type="button"
              role="menuitem"
              disabled={item.disabled}
              onClick={() => {
                setIsOpen(false);
                item.onClick();
              }}
              className={cn(
                'flex w-full items-center gap-2.5 rounded-lg px-3 py-2 text-left text-sm transition-colors hover:bg-neutral-100 disabled:opacity-50 dark:hover:bg-neutral-800',
                item.danger
                  ? 'text-red-600 hover:bg-red-50 dark:text-red-400 dark:hover:bg-red-900/20'
                  : 'text-neutral-700 dark:text-neutral-200',
              )}
            >
              {item.icon}
              {item.label}
            </button>
          ))}
        </div>
      )}
    </div>
  );
}
