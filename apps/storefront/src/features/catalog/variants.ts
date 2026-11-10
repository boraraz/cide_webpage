export interface OptionValue {
  value: string;
  code?: string | null;
}

export interface OptionGroup {
  name: string;
  values: OptionValue[];
}

export interface SelectedOption {
  name: string;
  value: string;
  code?: string | null;
}

export interface SellableVariant {
  id: string;
  sku: string;
  price: string;
  compareAtPrice?: string | null;
  currency: string;
  available: boolean;
  availability: string;
  options: SelectedOption[];
}

export function optionKey(name: string): string {
  return name
    .trim()
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-|-$/g, '');
}

export function defaultSelection(variants: SellableVariant[]): Record<string, string> {
  const preferred = variants.find((variant) => variant.available) ?? variants[0];
  return Object.fromEntries(
    (preferred?.options ?? []).map((option) => [option.name, option.value]),
  );
}

export function selectionFromSearch(
  params: URLSearchParams,
  options: OptionGroup[],
): Record<string, string> | null {
  const selected: Record<string, string> = {};
  for (const option of options) {
    const raw = params.get(optionKey(option.name));
    if (!raw) return null;
    const match = option.values.find(
      (value) =>
        value.value.toLowerCase() === raw.toLowerCase() ||
        value.code?.toLowerCase() === raw.toLowerCase(),
    );
    if (!match) return null;
    selected[option.name] = match.value;
  }
  return options.length === 0 ? {} : selected;
}

export function resolveVariant(
  variants: SellableVariant[],
  selected: Record<string, string>,
): SellableVariant | null {
  return (
    variants.find(
      (variant) =>
        variant.options.length === Object.keys(selected).length &&
        variant.options.every((option) => selected[option.name] === option.value),
    ) ?? null
  );
}

export function combinationExists(
  variants: SellableVariant[],
  selected: Record<string, string>,
  name: string,
  value: string,
): boolean {
  const next = { ...selected, [name]: value };
  return variants.some((variant) =>
    variant.options.every(
      (option) => next[option.name] === undefined || next[option.name] === option.value,
    ),
  );
}

export function writeSelection(
  params: URLSearchParams,
  options: OptionGroup[],
  selected: Record<string, string>,
) {
  for (const option of options) {
    const key = optionKey(option.name);
    const value = selected[option.name];
    if (value) params.set(key, value);
    else params.delete(key);
  }
}
