#!/usr/bin/env node
/**
 * Offline UPM package structure checks (no Unity required).
 * Exit 0 on success; print failures and exit 1 otherwise.
 */
import { existsSync, readFileSync, readdirSync, statSync } from 'node:fs';
import { dirname, join, relative, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const errors = [];

function fail(message) {
  errors.push(message);
}

function readJson(path) {
  try {
    return JSON.parse(readFileSync(path, 'utf8'));
  } catch (error) {
    fail(`${relative(repoRoot, path)}: ${error.message}`);
    return null;
  }
}

function requireFile(path, label = path) {
  if (!existsSync(path)) fail(`missing file: ${label}`);
  return existsSync(path);
}

function requireDir(path, label = path) {
  if (!existsSync(path) || !statSync(path).isDirectory()) fail(`missing directory: ${label}`);
  return existsSync(path) && statSync(path).isDirectory();
}

// UPM rejects non-SemVer dependency values inside package.json (git/file refs
// are only valid in a project's manifest.json). Enforce across all packages.
const SEMVER = /^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?(\+[0-9A-Za-z.-]+)?$/;
function requireSemVerDeps(pkgJsonPath, label) {
  const pkg = readJson(pkgJsonPath);
  if (!pkg) return;
  const deps = pkg.dependencies;
  if (deps && typeof deps === 'object') {
    for (const [dep, value] of Object.entries(deps)) {
      if (typeof value !== 'string' || !SEMVER.test(value)) {
        fail(`${label} dependency ${dep}: "${value}" is not SemVer (git/file refs only work in a project manifest.json)`);
      }
    }
  }
}

// --- root layout ---
if (existsSync(join(repoRoot, 'package.json'))) {
  fail('package.json must NOT live at repo root (use Cascade/package.json + ?path=Cascade)');
}
requireFile(join(repoRoot, 'LICENSE'), 'LICENSE');
requireFile(join(repoRoot, 'README.md'), 'README.md');
requireFile(join(repoRoot, 'CONTEXT.md'), 'CONTEXT.md');

const packageJsonPath = join(repoRoot, 'Cascade', 'package.json');
requireFile(packageJsonPath, 'Cascade/package.json');
requireSemVerDeps(packageJsonPath, 'Cascade');
const pkg = readJson(packageJsonPath);
if (pkg) {
  if (pkg.name !== 'com.source27.cascade') fail(`Cascade package name: expected com.source27.cascade, got ${pkg.name}`);
  if (!pkg.version) fail('Cascade/package.json missing version');
  if (!pkg.unity) fail('Cascade/package.json missing unity');
  if (pkg.license !== 'MIT') fail(`Cascade/package.json license: expected MIT, got ${pkg.license}`);
  if (!pkg.dependencies || typeof pkg.dependencies !== 'object') fail('Cascade/package.json missing dependencies');
  for (const dep of Object.keys(pkg.dependencies)) {
    if (/hybridclr|lit-motion|loopscroll/i.test(dep)) fail(`Cascade must not depend on ${dep}`);
  }
}

const requiredAsmdefs = [
  'Cascade/Runtime/Cascade.Bootstrap/Cascade.Bootstrap.asmdef',
  'Cascade/Runtime/Cascade.Service/Cascade.Service.asmdef',
  'Cascade/Runtime/Cascade.Core/Cascade.Core.asmdef',
  'Cascade/Editor/Cascade.Editor/Cascade.Editor.asmdef',
  'Cascade/Tests/Cascade.Tests/Cascade.Tests.asmdef',
];
for (const rel of requiredAsmdefs) requireFile(join(repoRoot, rel), rel);

requireDir(join(repoRoot, 'Cascade', 'Roslyn'), 'Cascade/Roslyn');
const roslynDlls = existsSync(join(repoRoot, 'Cascade', 'Roslyn'))
  ? readdirSync(join(repoRoot, 'Cascade', 'Roslyn')).filter((name) => name.endsWith('.dll'))
  : [];
if (roslynDlls.length === 0) fail('Cascade/Roslyn must contain at least one source-generator .dll');

requireFile(
  join(repoRoot, 'Cascade', 'Tools~', 'Cascade.SourceGenerator', 'Cascade.SourceGenerator.csproj'),
  'Cascade/Tools~/Cascade.SourceGenerator/Cascade.SourceGenerator.csproj',
);

const yooPath = join(repoRoot, 'Integrations', 'YooAsset', 'package.json');
requireFile(yooPath, 'Integrations/YooAsset/package.json');
requireSemVerDeps(yooPath, 'Integrations/YooAsset');
const yoo = readJson(yooPath);
if (yoo) {
  if (yoo.name !== 'com.source27.cascade.integrations.yooasset') {
    fail(`YooAsset package name mismatch: ${yoo.name}`);
  }
  if (yoo.license !== 'MIT') fail(`Integrations/YooAsset/package.json license: expected MIT, got ${yoo.license}`);
  requireFile(
    join(repoRoot, 'Integrations', 'YooAsset', 'Runtime', 'Cascade.Service.YooAsset.asmdef'),
    'Integrations/YooAsset/Runtime/Cascade.Service.YooAsset.asmdef',
  );
}

const locToolsPath = join(repoRoot, 'Modules', 'LocalizationTools', 'package.json');
requireFile(locToolsPath, 'Modules/LocalizationTools/package.json');
requireSemVerDeps(locToolsPath, 'Modules/LocalizationTools');
const locTools = readJson(locToolsPath);
if (locTools) {
  if (locTools.name !== 'com.source27.cascade.modules.localizationtools') {
    fail(`LocalizationTools package name mismatch: ${locTools.name}`);
  }
  if (locTools.license !== 'MIT') {
    fail(`Modules/LocalizationTools/package.json license: expected MIT, got ${locTools.license}`);
  }
  requireFile(
    join(repoRoot, 'Modules', 'LocalizationTools', 'Editor', 'Cascade.Modules.LocalizationTools.Editor.asmdef'),
    'Modules/LocalizationTools/Editor/Cascade.Modules.LocalizationTools.Editor.asmdef',
  );
}

// --- starters ---
requireFile(
  join(repoRoot, 'Starters', 'Mobile', 'Packages', 'manifest.json'),
  'Starters/Mobile/Packages/manifest.json',
);
requireFile(
  join(repoRoot, 'Starters', 'Mobile', 'ProjectSettings', 'ProjectVersion.txt'),
  'Starters/Mobile/ProjectSettings/ProjectVersion.txt',
);
requireFile(
  join(repoRoot, 'Starters', 'Indie', 'Packages', 'manifest.json'),
  'Starters/Indie/Packages/manifest.json',
);
requireFile(
  join(repoRoot, 'Starters', 'Indie', 'ProjectSettings', 'ProjectVersion.txt'),
  'Starters/Indie/ProjectSettings/ProjectVersion.txt',
);
const projectVersion = readFileSync(
  join(repoRoot, 'Starters', 'Mobile', 'ProjectSettings', 'ProjectVersion.txt'),
  'utf8',
);
if (!/m_EditorVersion:\s*2022\.3\.62f3/.test(projectVersion)) {
  fail('Starters/Mobile ProjectVersion.txt must pin Unity 2022.3.62f3');
}
const indieVersion = readFileSync(
  join(repoRoot, 'Starters', 'Indie', 'ProjectSettings', 'ProjectVersion.txt'),
  'utf8',
);
if (!/m_EditorVersion:\s*2022\.3\.62f3/.test(indieVersion)) {
  fail('Starters/Indie ProjectVersion.txt must pin Unity 2022.3.62f3');
}
requireSemVerDeps(join(repoRoot, 'Integrations', 'Addressables', 'package.json'), 'Integrations/Addressables');

if (errors.length > 0) {
  console.error('validate-upm failed:');
  for (const error of errors) console.error(`  - ${error}`);
  process.exit(1);
}

console.log('validate-upm OK');
console.log(`  package ${pkg?.name}@${pkg?.version} (unity ${pkg?.unity})`);
console.log(`  roslyn dlls: ${roslynDlls.join(', ')}`);
console.log(`  starters editor: 2022.3.62f3`);
