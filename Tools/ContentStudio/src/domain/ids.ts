const ID_PATTERN = /^[a-z][a-z0-9-]*(?:\.[a-z][a-z0-9-]*)+$/;
export const isAuthoringId = (value: string): boolean => ID_PATTERN.test(value);

export function createAuthoringId(
  namespace: string,
  random: () => string = () => crypto.randomUUID(),
): string {
  const safeNamespace =
    namespace
      .toLowerCase()
      .replace(/[^a-z0-9-]/g, "-")
      .replace(/^-+|--+/g, "") || "studio";
  const suffix = random()
    .toLowerCase()
    .replace(/[^a-z0-9]/g, "")
    .slice(0, 12);
  return `${safeNamespace}.record-${suffix}`;
}

/** Duplicates authoring content while deliberately issuing a new content identity. */
export function duplicateWithNewId<
  T extends Record<string, unknown>,
  K extends keyof T,
>(record: T, idKey: K, id: T[K]): T {
  return structuredClone({ ...record, [idKey]: id });
}
