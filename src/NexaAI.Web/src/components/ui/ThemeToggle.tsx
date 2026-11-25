import { Moon, Sun } from 'lucide-react';
import { useTheme } from '@hooks/useTheme';
import { cn } from '@utils/cn';

export interface ThemeToggleProps {
  className?: string;
  showLabel?: boolean;
}

export function ThemeToggle({ className, showLabel = false }: ThemeToggleProps) {
  const { isDark, toggleTheme } = useTheme();

  return (
    <button
      type="button"
      onClick={toggleTheme}
      aria-label={isDark ? 'Switch to light theme' : 'Switch to dark theme'}
      title={isDark ? 'Light mode' : 'Dark mode'}
      className={cn('btn-icon btn-ghost text-neutral-500 hover:text-primary-600 dark:text-neutral-400', className)}
    >
      {isDark ? <Sun className="h-5 w-5" /> : <Moon className="h-5 w-5" />}
      {showLabel && <span className="text-sm">{isDark ? 'Light mode' : 'Dark mode'}</span>}
    </button>
  );
}
