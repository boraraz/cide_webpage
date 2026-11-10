import { describe, expect, it } from 'vitest';
import { discountPreview, formatTry, toApiMoney } from './money';

describe('TRY money', () => {
  it('accepts API decimals and Turkish display input', () => {
    expect(toApiMoney('1299.00')).toBe('1299.00');
    expect(toApiMoney('1299')).toBe('1299.00');
    expect(toApiMoney('1.299,00')).toBe('1299.00');
    expect(toApiMoney('10,5')).toBe('10.50');
    expect(toApiMoney('10.555')).toBeNull();
    expect(toApiMoney('-1')).toBeNull();
  });

  it('formats kuruş for display and previews a discount', () => {
    expect(formatTry('1299.00')).toContain('1.299,00');
    expect(discountPreview('100.00', '80.00')).toBeNull();
    expect(discountPreview('80.00', '100.00')).toBe('20% off 100,00 TL');
  });
});
