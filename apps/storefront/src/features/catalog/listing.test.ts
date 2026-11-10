import { describe, expect, it } from 'vitest';
import { listingSearch, parseListing, toProductQuery } from './listing';

describe('listing URL state', () => {
  it('keeps priceBasis=from in the URL and the product query', () => {
    const state = parseListing(new URLSearchParams('color=black&sort=name&page=2'), {
      category: 'tees',
    });
    expect(listingSearch(state, { category: 'tees' })).toBe(
      '?color=black&priceBasis=from&sort=name&page=2',
    );
    expect(toProductQuery(state).priceBasis).toBe('from');
    expect(toProductQuery(state).category).toBe('tees');
  });

  it('does not list a search shorter than the page gate', () => {
    expect(parseListing(new URLSearchParams('q=a')).q).toBe('a');
    expect(listingSearch(parseListing(new URLSearchParams('q=jacket')), { q: 'jacket' })).toContain(
      'q=jacket',
    );
  });
});
