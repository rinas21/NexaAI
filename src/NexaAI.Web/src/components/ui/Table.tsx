import type { ReactNode } from 'react';
import { cn } from '@utils/cn';
import { EmptyState } from './EmptyState';

export interface Column<T> {
  key: string;
  header: string;
  render: (row: T) => ReactNode;
  className?: string;
  headerClassName?: string;
}

export interface TableProps<T> {
  columns: Column<T>[];
  data: T[];
  keyExtractor: (row: T) => string;
  isLoading?: boolean;
  emptyTitle?: string;
  emptyDescription?: string;
  emptyIcon?: ReactNode;
  onRowClick?: (row: T) => void;
  className?: string;
}

export function Table<T>({
  columns,
  data,
  keyExtractor,
  isLoading = false,
  emptyTitle = 'Nothing here yet',
  emptyDescription,
  emptyIcon,
  onRowClick,
  className,
}: TableProps<T>) {
  if (isLoading) {
    return (
      <div className={cn('table-container', className)} aria-busy="true" aria-label="Loading table">
        <table className="table">
          <thead>
            <tr>
              {columns.map((column) => (
                <th key={column.key} className={column.headerClassName}>
                  {column.header}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {Array.from({ length: 4 }).map((_, index) => (
              <tr key={index}>
                {columns.map((column) => (
                  <td key={column.key}>
                    <span className="skeleton block h-4 w-3/4" />
                  </td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    );
  }

  if (data.length === 0) {
    return (
      <div className={cn('card', className)}>
        <EmptyState title={emptyTitle} description={emptyDescription} icon={emptyIcon} />
      </div>
    );
  }

  return (
    <div className={cn('table-container glass', className)}>
      <table className="table">
        <thead>
          <tr>
            {columns.map((column) => (
              <th key={column.key} className={column.headerClassName}>
                {column.header}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {data.map((row) => (
            <tr
              key={keyExtractor(row)}
              onClick={onRowClick ? () => onRowClick(row) : undefined}
              className={onRowClick ? 'cursor-pointer' : undefined}
            >
              {columns.map((column) => (
                <td key={column.key} className={column.className}>
                  {column.render(row)}
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
