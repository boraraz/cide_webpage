export interface AxisValue {
  id: string;
  label: string;
}

export function cartesian<T>(axes: T[][]): T[][] {
  if (axes.length === 0) return [];
  return axes.reduce<T[][]>(
    (rows, axis) => rows.flatMap((row) => axis.map((value) => [...row, value])),
    [[]],
  );
}

export function matrixPreview(
  axes: AxisValue[][],
): Array<{ key: string; label: string; optionValueIds: string[] }> {
  return cartesian(axes).map((row) => ({
    key: row.map((value) => value.id).join('.'),
    label: row.map((value) => value.label).join(' / '),
    optionValueIds: row.map((value) => value.id),
  }));
}
