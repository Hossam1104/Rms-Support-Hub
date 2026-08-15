#!/usr/bin/env node
/**
 * Deterministic frontend build identity.
 *
 * HTTP 200 and the presence of <app-root> do not prove that a served Support
 * Hub origin is running the frontend that was just built: the secure Testing
 * runtime stages a published copy, so an older staged copy can answer both
 * checks. This script writes an immutable, non-secret identity document into
 * the emitted browser output so the staged bytes, the expected repository
 * state, and the bytes actually served can be compared exactly.
 *
 * The document intentionally carries no filesystem path, hostname, credential,
 * or environment variable value.
 *
 * Usage:
 *   node scripts/build-identity.mjs finalize --output <browser-dir> \
 *        --environment <name> [--commit <sha>] [--source-state <clean|modified>]
 *
 * The finalized document is written to <browser-dir>/build-identity.json and
 * echoed to stdout as a single JSON line for the caller to capture.
 */

import { createHash } from 'node:crypto';
import { execFileSync } from 'node:child_process';
import { readdirSync, readFileSync, writeFileSync, statSync } from 'node:fs';
import { join, relative, resolve, sep } from 'node:path';

/** The identity document describes every emitted asset except itself. */
const IDENTITY_FILE = 'build-identity.json';
const SCHEMA_VERSION = 1;

function fail(message) {
  process.stderr.write(`build-identity: ${message}\n`);
  process.exit(1);
}

function parseArguments(argv) {
  const parsed = { _: [] };
  for (let index = 0; index < argv.length; index += 1) {
    const token = argv[index];
    if (!token.startsWith('--')) {
      parsed._.push(token);
      continue;
    }
    const key = token.slice(2);
    const value = argv[index + 1];
    if (value === undefined || value.startsWith('--')) fail(`option --${key} requires a value`);
    parsed[key] = value;
    index += 1;
  }
  return parsed;
}

function sha256(buffer) {
  return createHash('sha256').update(buffer).digest('hex');
}

/** Recursively lists emitted files as repo-independent forward-slash paths. */
function listFiles(root, current = root, accumulator = []) {
  for (const entry of readdirSync(current, { withFileTypes: true })) {
    const absolute = join(current, entry.name);
    if (entry.isDirectory()) {
      listFiles(root, absolute, accumulator);
    } else if (entry.isFile()) {
      accumulator.push(relative(root, absolute).split(sep).join('/'));
    }
  }
  return accumulator;
}

/**
 * buildId = sha256 over "<relative-path> <sha256>\n" lines, ordinal-sorted and
 * excluding the identity document itself. The same algorithm is reimplemented
 * by the Testing startup verifier, so any change here must change both sides.
 */
function computeAssetManifest(root) {
  const files = listFiles(root)
    .filter(file => file !== IDENTITY_FILE)
    .sort();
  if (files.length === 0) fail(`no emitted files found under the requested output directory`);

  let manifest = '';
  const hashes = new Map();
  for (const file of files) {
    const hash = sha256(readFileSync(join(root, file)));
    hashes.set(file, hash);
    manifest += `${file} ${hash}\n`;
  }
  return { files, hashes, buildId: sha256(Buffer.from(manifest, 'utf8')) };
}

function resolveCommit(explicit) {
  if (explicit) return explicit;
  try {
    return execFileSync('git', ['rev-parse', 'HEAD'], { encoding: 'utf8' }).trim();
  } catch {
    return 'unknown';
  }
}

function resolveSourceState(explicit) {
  if (explicit) return explicit;
  try {
    const status = execFileSync('git', ['status', '--porcelain'], { encoding: 'utf8' }).trim();
    return status.length === 0 ? 'clean' : 'modified';
  } catch {
    return 'unknown';
  }
}

/** Exactly one hashed entry bundle must exist; it is the served parity probe. */
function resolveMainBundle(files, hashes) {
  const candidates = files.filter(file => /^main-[A-Za-z0-9]+\.js$/.test(file));
  if (candidates.length !== 1) {
    fail(`expected exactly one hashed main bundle in the build output, found ${candidates.length}`);
  }
  return { file: candidates[0], sha256: hashes.get(candidates[0]) };
}

function finalize(options) {
  if (!options.output) fail('finalize requires --output <browser-dir>');
  if (!options.environment) fail('finalize requires --environment <name>');

  const root = resolve(options.output);
  if (!statSync(root).isDirectory()) fail('the requested output path is not a directory');

  const { files, hashes, buildId } = computeAssetManifest(root);
  if (!hashes.has('index.html')) fail('the build output does not contain index.html');

  const commit = resolveCommit(options.commit);
  const identity = {
    schemaVersion: SCHEMA_VERSION,
    environment: options.environment,
    commit,
    commitShort: commit === 'unknown' ? 'unknown' : commit.slice(0, 7),
    sourceState: resolveSourceState(options['source-state']),
    buildId,
    assetCount: files.length,
    builtAtUtc: new Date().toISOString(),
    indexHtmlSha256: hashes.get('index.html'),
    mainBundle: resolveMainBundle(files, hashes)
  };

  const serialized = `${JSON.stringify(identity, null, 2)}\n`;
  writeFileSync(join(root, IDENTITY_FILE), serialized, 'utf8');
  process.stdout.write(`${JSON.stringify(identity)}\n`);
}

const args = parseArguments(process.argv.slice(2));
const command = args._[0];
if (command !== 'finalize') fail('the only supported command is "finalize"');
finalize(args);
