import { describe, expect, it } from 'vitest';
import {
  combinationExists,
  defaultSelection,
  resolveVariant,
  selectionFromSearch,
  type SellableVariant,
} from './variants';

const variants: SellableVariant[] = [
  {
    id: '1',
    sku: 'A',
    price: '100.00',
    currency: 'TRY',
    available: false,
    availability: 'out_of_stock',
    options: [
      { name: 'Color', value: 'Black' },
      { name: 'Size', value: 'S' },
    ],
  },
  {
    id: '2',
    sku: 'B',
    price: '120.00',
    currency: 'TRY',
    available: true,
    availability: 'in_stock',
    options: [
      { name: 'Color', value: 'Black' },
      { name: 'Size', value: 'M' },
    ],
  },
];

const options = [
  { name: 'Color', values: [{ value: 'Black' }, { value: 'White' }] },
  { name: 'Size', values: [{ value: 'S' }, { value: 'M' }] },
];

describe('variant selection', () => {
  it('defaults to the first in-stock variant', () => {
    expect(defaultSelection(variants)).toEqual({ Color: 'Black', Size: 'M' });
  });

  it('restores a selection from the query string and rejects unknown values', () => {
    expect(selectionFromSearch(new URLSearchParams('color=Black&size=S'), options)).toEqual({
      Color: 'Black',
      Size: 'S',
    });
    expect(selectionFromSearch(new URLSearchParams('color=Red&size=S'), options)).toBeNull();
  });

  it('disables combinations that are not real variants', () => {
    expect(combinationExists(variants, { Color: 'Black', Size: 'M' }, 'Size', 'S')).toBe(true);
    expect(combinationExists(variants, { Color: 'White', Size: 'M' }, 'Color', 'White')).toBe(
      false,
    );
    expect(resolveVariant(variants, { Color: 'Black', Size: 'M' })?.price).toBe('120.00');
  });
});
