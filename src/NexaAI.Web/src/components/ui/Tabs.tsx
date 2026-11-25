import type { ReactNode } from 'react';
import { cn } from '@utils/cn';

export interface TabItem {
  id: string;
  label: string;
  icon?: ReactNode;
  content: ReactNode;
}

export interface TabsProps {
  tabs: TabItem[];
  activeId?: string;
  onChange?: (id: string) => void;
  className?: string;
}

export function Tabs({ tabs, activeId, onChange, className }: TabsProps) {
  const resolvedId = activeId ?? tabs[0]?.id;
  const activeTab = tabs.find((tab) => tab.id === resolvedId);

  return (
    <div className={className}>
      <div
        role="tablist"
        aria-label="Sections"
        className="flex gap-1 overflow-x-auto border-b border-neutral-200 dark:border-neutral-700"
      >
        {tabs.map((tab) => (
          <button
            key={tab.id}
            type="button"
            role="tab"
            id={`tab-${tab.id}`}
            aria-selected={tab.id === resolvedId}
            aria-controls={`panel-${tab.id}`}
            onClick={() => onChange?.(tab.id)}
            className={cn(
              'flex items-center gap-2 whitespace-nowrap border-b-2 px-4 py-2.5 text-sm font-medium transition-colors',
              tab.id === resolvedId
                ? 'border-primary-600 text-primary-700 dark:border-primary-400 dark:text-primary-300'
                : 'border-transparent text-neutral-500 hover:text-neutral-800 dark:text-neutral-400 dark:hover:text-neutral-100',
            )}
          >
            {tab.icon}
            {tab.label}
          </button>
        ))}
      </div>
      {activeTab && (
        <div
          role="tabpanel"
          id={`panel-${activeTab.id}`}
          aria-labelledby={`tab-${activeTab.id}`}
          className="pt-5"
        >
          {activeTab.content}
        </div>
      )}
    </div>
  );
}
