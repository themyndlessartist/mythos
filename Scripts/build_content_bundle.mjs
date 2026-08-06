#!/usr/bin/env node

import { createHash } from "node:crypto";
import { mkdir, readFile, writeFile } from "node:fs/promises";
import { dirname, resolve, sep } from "node:path";

const [, , sourceDirectoryArgument, outputArgument] = process.argv;
if (!sourceDirectoryArgument || !outputArgument) {
  console.error("Usage: build_content_bundle.mjs <package-directory> <output-file>");
  process.exit(2);
}

const sourceDirectory = resolve(sourceDirectoryArgument);
const outputPath = resolve(outputArgument);
const manifestPath = resolve(sourceDirectory, "package.json");
const manifestBytes = await readFile(manifestPath);
const manifest = JSON.parse(manifestBytes.toString("utf8"));

if (
  manifest.document_kind !== "mythos.content-package" ||
  !["1.0", "1.1"].includes(manifest.schema_version)
) {
  throw new Error("Unsupported content package manifest.");
}
if (!Array.isArray(manifest.entries)) {
  throw new Error("Package entries must be an array.");
}

const normalizePath = (value) => {
  if (
    typeof value !== "string" ||
    value.length === 0 ||
    value.startsWith("/") ||
    value.includes("\\") ||
    value.includes("//") ||
    value.split("/").some((part) => part === "" || part === "." || part === "..")
  ) {
    throw new Error(`Unsafe package path: ${value}`);
  }
  return value;
};

const sha256 = (bytes) => createHash("sha256").update(bytes).digest("hex");
const declaredPaths = new Set();
const files = [];

for (const entry of manifest.entries) {
  const relativePath = normalizePath(entry.path);
  if (declaredPaths.has(relativePath.toLowerCase())) {
    throw new Error(`Duplicate package path: ${relativePath}`);
  }
  declaredPaths.add(relativePath.toLowerCase());

  const sourcePath = resolve(sourceDirectory, relativePath);
  if (!sourcePath.startsWith(`${sourceDirectory}${sep}`)) {
    throw new Error(`Package path escapes source directory: ${relativePath}`);
  }
  const bytes = await readFile(sourcePath);
  const digest = sha256(bytes);
  if (
    entry.size !== bytes.length ||
    entry.integrity?.algorithm !== "sha256" ||
    entry.integrity?.digest !== digest
  ) {
    throw new Error(`Manifest metadata does not match bytes: ${relativePath}`);
  }
  files.push({
    path: relativePath,
    media_type: entry.media_type,
    size: bytes.length,
    integrity: { algorithm: "sha256", digest },
    content_base64: bytes.toString("base64"),
  });
}

files.push({
  path: "package.json",
  media_type: "application/json",
  size: manifestBytes.length,
  integrity: { algorithm: "sha256", digest: sha256(manifestBytes) },
  content_base64: manifestBytes.toString("base64"),
});
files.sort((left, right) => left.path.localeCompare(right.path, "en", { sensitivity: "variant" }));

const bundle = {
  bundle_kind: "mythos.content-bundle",
  bundle_version: "1.0",
  package_id: manifest.package_id,
  files,
};

await mkdir(dirname(outputPath), { recursive: true });
await writeFile(outputPath, `${JSON.stringify(bundle, null, 2)}\n`, { flag: "w" });
console.log(`Wrote ${outputPath}`);
