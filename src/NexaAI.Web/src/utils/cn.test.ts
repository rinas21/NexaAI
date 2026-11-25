import { describe, expect, it } from 'vitest';
import { cn } from './cn';

describe('cn', () => {
  it('joins conditional class names', () => {
    expect(cn('btn', false && 'hidden', 'active')).toBe('btn active');
  });

  it('lets later Tailwind utilities override earlier ones', () => {
    expect(cn('px-4', 'px-2', 'text-sm')).toBe('px-2 text-sm');
  });
});
