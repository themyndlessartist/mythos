export const RASTER_MEDIA_TYPES = [
  "image/png",
  "image/jpeg",
  "image/webp",
] as const;

export interface ImportLimits {
  maxFileBytes: number;
  maxWidth: number;
  maxHeight: number;
  maxDecodedBytes: number;
  maxFileCount: number;
  maxPackageBytes: number;
}

export const MVP_IMPORT_LIMITS: ImportLimits = {
  maxFileBytes: 10 * 1024 * 1024,
  maxWidth: 8192,
  maxHeight: 8192,
  maxDecodedBytes: 64 * 1024 * 1024,
  maxFileCount: 250,
  maxPackageBytes: 250 * 1024 * 1024,
};

const DRIVE_OR_ABSOLUTE = /^(?:[a-zA-Z]:[\\/]|[\\/])/;
// eslint-disable-next-line no-control-regex -- required package-path security range
const CONTROL = /[\u0000-\u001f\u007f]/;
const RESERVED = /^(?:con|prn|aux|nul|com[1-9]|lpt[1-9])(?:\.|$)/i;

export function normalizePackagePath(input: string): string | null {
  if (
    !input ||
    DRIVE_OR_ABSOLUTE.test(input) ||
    CONTROL.test(input) ||
    input.includes("\\")
  )
    return null;
  const segments = input.split("/");
  if (
    segments.some(
      (part) => !part || part === "." || part === ".." || RESERVED.test(part),
    )
  )
    return null;
  return segments.join("/");
}

export function isRemoteReference(value: string): boolean {
  return (
    /^(?:https?|data|blob|file):/i.test(value.trim()) || value.startsWith("//")
  );
}

export function sniffRasterMediaType(
  bytes: Uint8Array,
): (typeof RASTER_MEDIA_TYPES)[number] | null {
  const png = [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a];
  if (png.every((value, index) => bytes[index] === value)) return "image/png";
  if (bytes[0] === 0xff && bytes[1] === 0xd8 && bytes[2] === 0xff)
    return "image/jpeg";
  if (
    String.fromCharCode(...bytes.slice(0, 4)) === "RIFF" &&
    String.fromCharCode(...bytes.slice(8, 12)) === "WEBP"
  )
    return "image/webp";
  return null;
}

export interface MediaCandidate {
  name: string;
  type: string;
  bytes: Uint8Array;
  width: number;
  height: number;
}
export function validateRasterImport(
  candidate: MediaCandidate,
  limits: ImportLimits = MVP_IMPORT_LIMITS,
): string[] {
  const errors: string[] = [];
  const declared = candidate.type.toLowerCase();
  const detected = sniffRasterMediaType(candidate.bytes);
  if (!(RASTER_MEDIA_TYPES as readonly string[]).includes(declared))
    errors.push("media.unsupported");
  if (!detected || detected !== declared)
    errors.push("media.signature-mismatch");
  if (candidate.bytes.byteLength > limits.maxFileBytes)
    errors.push("media.file-too-large");
  if (
    !Number.isInteger(candidate.width) ||
    !Number.isInteger(candidate.height) ||
    candidate.width <= 0 ||
    candidate.height <= 0
  )
    errors.push("media.invalid-dimensions");
  else {
    if (
      candidate.width > limits.maxWidth ||
      candidate.height > limits.maxHeight
    )
      errors.push("media.dimensions-too-large");
    if (candidate.width * candidate.height * 4 > limits.maxDecodedBytes)
      errors.push("media.decoded-too-large");
  }
  return errors;
}
